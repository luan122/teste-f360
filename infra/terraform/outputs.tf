output "host_public_ip" {
  description = "Public (Elastic) IP of the Docker Compose host."
  value       = aws_eip.host.public_ip
}

output "api_base_url" {
  description = "Base URL for the Ingestion API once the compose stack is up (allow a few minutes after apply for cloud-init to finish)."
  value       = "http://${aws_eip.host.public_ip}:${var.api_port}"
}

output "rabbitmq_management_url" {
  description = "RabbitMQ management UI URL. Only reachable if expose_rabbitmq_management = true and your CIDR is in allowed_ssh_cidr_blocks."
  value       = "http://${aws_eip.host.public_ip}:${var.rabbitmq_management_port}"
}

output "ssh_command" {
  description = "SSH command to reach the host, if allowed_ssh_cidr_blocks is non-empty and ssh_key_name is set. Prefer `aws ssm start-session` otherwise."
  value       = "ssh ec2-user@${aws_eip.host.public_ip}"
}

output "ssm_session_command" {
  description = "AWS Systems Manager Session Manager command — works without opening SSH ingress, as long as the AWS CLI + Session Manager plugin are installed locally."
  value       = "aws ssm start-session --target ${aws_instance.host.id} --region ${var.aws_region}"
}

output "instance_id" {
  description = "EC2 instance id of the Docker Compose host."
  value       = aws_instance.host.id
}

output "security_group_id" {
  description = "Security group id attached to the host, for reference when adding further ingress rules."
  value       = aws_security_group.host.id
}
