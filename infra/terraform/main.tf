# Distributed Job Orchestrator — reference AWS deployment (ADR-008).
#
# Provisions a single EC2 instance that runs the same docker-compose.yml topology used for local
# development (MongoDB single-node replica set, RabbitMQ, Api, Worker) via cloud-init. This is a
# deliberately simple reference/demo target, not a hardened production deployment — see
# ARCHITECTURE.md ADR-008 for the reasoning (in particular, why this avoids Amazon DocumentDB's
# MongoDB-API transaction-compatibility gaps that would threaten the transactional Outbox).
#
# Not intended to be `terraform apply`'d against a real account as part of grading — the bar is
# `terraform init && terraform validate` (specs/007-deliverables/spec.md, AC-007-3). Apply steps
# are documented in README.md for an operator who does want to stand it up.

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

locals {
  name_prefix = "${var.project_name}-${var.environment}"

  common_tags = merge(
    {
      Project     = var.project_name
      Environment = var.environment
      ManagedBy   = "terraform"
    },
    var.tags,
  )
}

# ---------------------------------------------------------------------------
# Networking — a minimal VPC with one public subnet. The reference host needs
# only inbound HTTP(S)/SSH and outbound internet for image pulls.
# ---------------------------------------------------------------------------

resource "aws_vpc" "this" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-vpc"
  })
}

resource "aws_internet_gateway" "this" {
  vpc_id = aws_vpc.this.id

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-igw"
  })
}

data "aws_availability_zones" "available" {
  state = "available"
}

resource "aws_subnet" "public" {
  vpc_id                   = aws_vpc.this.id
  cidr_block               = var.public_subnet_cidr
  availability_zone        = coalesce(var.availability_zone, data.aws_availability_zones.available.names[0])
  map_public_ip_on_launch  = true

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-public-subnet"
  })
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.this.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.this.id
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-public-rt"
  })
}

resource "aws_route_table_association" "public" {
  subnet_id      = aws_subnet.public.id
  route_table_id = aws_route_table.public.id
}

# ---------------------------------------------------------------------------
# Security group — mirrors the ports docker-compose.yml exposes:
#   - api_port (default 8080): Ingestion API
#   - rabbitmq_management_port (default 15672): RabbitMQ management UI (opt-in)
#   - 22: SSH (opt-in, empty by default — prefer SSM Session Manager)
# ---------------------------------------------------------------------------

resource "aws_security_group" "host" {
  name        = "${local.name_prefix}-host-sg"
  description = "Distributed Job Orchestrator reference host — API, optional RabbitMQ UI, optional SSH"
  vpc_id      = aws_vpc.this.id

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-host-sg"
  })
}

resource "aws_vpc_security_group_ingress_rule" "api" {
  for_each = toset(var.allowed_api_cidr_blocks)

  security_group_id = aws_security_group.host.id
  description       = "Ingestion API"
  from_port         = var.api_port
  to_port           = var.api_port
  ip_protocol       = "tcp"
  cidr_ipv4         = each.value
}

resource "aws_vpc_security_group_ingress_rule" "rabbitmq_management" {
  for_each = var.expose_rabbitmq_management ? toset(var.allowed_ssh_cidr_blocks) : toset([])

  security_group_id = aws_security_group.host.id
  description       = "RabbitMQ management UI (restricted CIDR, opt-in)"
  from_port         = var.rabbitmq_management_port
  to_port           = var.rabbitmq_management_port
  ip_protocol       = "tcp"
  cidr_ipv4         = each.value
}

resource "aws_vpc_security_group_ingress_rule" "ssh" {
  for_each = toset(var.allowed_ssh_cidr_blocks)

  security_group_id = aws_security_group.host.id
  description       = "SSH (restricted CIDR, opt-in)"
  from_port         = 22
  to_port           = 22
  ip_protocol       = "tcp"
  cidr_ipv4         = each.value
}

resource "aws_vpc_security_group_egress_rule" "all_outbound" {
  security_group_id = aws_security_group.host.id
  description       = "Allow all outbound (image pulls, package installs)"
  ip_protocol       = "-1"
  cidr_ipv4         = "0.0.0.0/0"
}

# ---------------------------------------------------------------------------
# Compute — one EC2 instance running Docker + Docker Compose, bootstrapped via
# cloud-init to clone this repository and bring the compose stack up.
# ---------------------------------------------------------------------------

data "aws_ami" "al2023" {
  most_recent = true
  owners      = ["amazon"]

  filter {
    name   = "name"
    values = ["al2023-ami-*-x86_64"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

resource "aws_iam_role" "host" {
  name = "${local.name_prefix}-host-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "ec2.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })

  tags = local.common_tags
}

# Enables AWS Systems Manager Session Manager as the preferred operator access
# path, so allowed_ssh_cidr_blocks can safely stay empty in most environments.
resource "aws_iam_role_policy_attachment" "ssm" {
  role       = aws_iam_role.host.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

resource "aws_iam_instance_profile" "host" {
  name = "${local.name_prefix}-host-profile"
  role = aws_iam_role.host.name
}

resource "aws_eip" "host" {
  domain = "vpc"

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-host-eip"
  })
}

resource "aws_eip_association" "host" {
  instance_id   = aws_instance.host.id
  allocation_id = aws_eip.host.id
}

resource "aws_instance" "host" {
  ami                         = data.aws_ami.al2023.id
  instance_type               = var.instance_type
  subnet_id                   = aws_subnet.public.id
  vpc_security_group_ids      = [aws_security_group.host.id]
  iam_instance_profile        = aws_iam_instance_profile.host.name
  key_name                    = var.ssh_key_name
  associate_public_ip_address = true

  root_block_device {
    volume_size = var.root_volume_size_gb
    volume_type = "gp3"
    encrypted   = true
  }

  metadata_options {
    http_tokens   = "required" # IMDSv2 only
    http_endpoint = "enabled"
  }

  user_data = templatefile("${path.module}/templates/cloud-init.yaml.tpl", {
    git_repository_url     = var.git_repository_url
    git_ref                = var.git_ref
    container_image_api    = var.container_image_api
    container_image_worker = var.container_image_worker
    api_key_secret         = var.api_key_secret
    mongo_root_password    = var.mongo_root_password
    rabbitmq_password      = var.rabbitmq_password
  })

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-host"
  })
}
