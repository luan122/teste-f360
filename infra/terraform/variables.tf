variable "aws_region" {
  description = "AWS region to deploy the reference host into."
  type        = string
  default     = "us-east-1"
}

variable "project_name" {
  description = "Short name used to tag and name every resource (e.g. \"job-orchestrator\")."
  type        = string
  default     = "job-orchestrator"
}

variable "environment" {
  description = "Deployment environment label (e.g. \"demo\", \"staging\"). Used in resource names/tags."
  type        = string
  default     = "demo"
}

variable "vpc_cidr" {
  description = "CIDR block for the reference VPC."
  type        = string
  default     = "10.42.0.0/16"
}

variable "public_subnet_cidr" {
  description = "CIDR block for the single public subnet the host runs in."
  type        = string
  default     = "10.42.1.0/24"
}

variable "availability_zone" {
  description = "Availability zone for the public subnet. Leave null to let AWS pick the first AZ in the region."
  type        = string
  default     = null
}

variable "instance_type" {
  description = "EC2 instance type for the Docker Compose host. Must have enough memory for Mongo + RabbitMQ + Api + Worker containers."
  type        = string
  default     = "t3.large"
}

variable "root_volume_size_gb" {
  description = "Root EBS volume size (GB) for the host — holds container images, Mongo data, and RabbitMQ data."
  type        = number
  default     = 40
}

variable "ssh_key_name" {
  description = "Name of an existing EC2 key pair to associate with the instance for operator SSH access. Leave null to disable key-based SSH (use SSM Session Manager instead, recommended)."
  type        = string
  default     = null
}

variable "allowed_ssh_cidr_blocks" {
  description = "CIDR blocks allowed to reach the host on port 22. Empty list disables SSH ingress entirely (recommended: use SSM instead of opening 22)."
  type        = list(string)
  default     = []
}

variable "allowed_api_cidr_blocks" {
  description = "CIDR blocks allowed to reach the Ingestion API port (8080). Default is open for demo purposes only — restrict in any shared environment."
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

variable "api_port" {
  description = "Host port the Api container's HTTP endpoint is published on (matches docker-compose.yml)."
  type        = number
  default     = 8080
}

variable "rabbitmq_management_port" {
  description = "Host port the RabbitMQ management UI is published on."
  type        = number
  default     = 15672
}

variable "expose_rabbitmq_management" {
  description = "Whether to open the RabbitMQ management UI port to allowed_ssh_cidr_blocks. Keep false unless actively debugging."
  type        = bool
  default     = false
}

variable "git_repository_url" {
  description = "HTTPS URL of this repository, cloned by cloud-init onto the host to obtain docker-compose.yml and the .env template."
  type        = string
  default     = "https://github.com/CHANGE-ME/teste-f360.git"
}

variable "git_ref" {
  description = "Branch, tag, or commit to check out on the host."
  type        = string
  default     = "main"
}

variable "container_image_api" {
  description = "Container image reference for the Api host (published by CI). Passed into the host's environment for docker-compose.yml to consume."
  type        = string
  default     = "ghcr.io/CHANGE-ME/job-orchestrator-api:latest"
}

variable "container_image_worker" {
  description = "Container image reference for the Worker host (published by CI)."
  type        = string
  default     = "ghcr.io/CHANGE-ME/job-orchestrator-worker:latest"
}

variable "api_key_secret" {
  description = "API key value the Ingestion API accepts via the X-Api-Key header. Sensitive — pass via TF_VAR_api_key_secret or a secrets manager, never commit a real value."
  type        = string
  sensitive   = true
  default     = "REPLACE_ME_VIA_TF_VAR_OR_SECRETS_MANAGER"
}

variable "mongo_root_password" {
  description = "Root password for the self-managed MongoDB replica-set container. Sensitive — pass via TF_VAR_mongo_root_password or a secrets manager."
  type        = string
  sensitive   = true
  default     = "REPLACE_ME_VIA_TF_VAR_OR_SECRETS_MANAGER"
}

variable "rabbitmq_password" {
  description = "Password for the RabbitMQ default user. Sensitive — pass via TF_VAR_rabbitmq_password or a secrets manager."
  type        = string
  sensitive   = true
  default     = "REPLACE_ME_VIA_TF_VAR_OR_SECRETS_MANAGER"
}

variable "tags" {
  description = "Extra tags merged onto every taggable resource."
  type        = map(string)
  default     = {}
}
