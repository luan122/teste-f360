#cloud-config
# Bootstraps the reference EC2 host: installs Docker + the Compose plugin, clones the repository
# at the requested ref, writes a .env consumed by docker-compose.yml, and brings the stack up.
# See ARCHITECTURE.md ADR-008 and infra/terraform/README.md for context.

package_update: true

packages:
  - docker
  - git

runcmd:
  - systemctl enable --now docker
  - usermod -aG docker ec2-user
  - mkdir -p /opt/job-orchestrator
  - git clone --branch "${git_ref}" --depth 1 "${git_repository_url}" /opt/job-orchestrator || (cd /opt/job-orchestrator && git fetch --depth 1 origin "${git_ref}" && git checkout "${git_ref}")
  - |
    cat > /opt/job-orchestrator/.env <<'ENV_EOF'
    API_IMAGE=${container_image_api}
    WORKER_IMAGE=${container_image_worker}
    API_KEYS=${api_key_secret}
    MONGO_INITDB_ROOT_PASSWORD=${mongo_root_password}
    RABBITMQ_DEFAULT_PASS=${rabbitmq_password}
    ENV_EOF
  - chmod 600 /opt/job-orchestrator/.env
  # Docker Compose v2 ships as a plugin on Amazon Linux 2023's docker package; install explicitly
  # if the base image doesn't already include it.
  - |
    if ! docker compose version >/dev/null 2>&1; then
      mkdir -p /usr/local/lib/docker/cli-plugins
      curl -sSL "https://github.com/docker/compose/releases/latest/download/docker-compose-linux-x86_64" \
        -o /usr/local/lib/docker/cli-plugins/docker-compose
      chmod +x /usr/local/lib/docker/cli-plugins/docker-compose
    fi
  - cd /opt/job-orchestrator && docker compose --env-file .env up -d

final_message: "Distributed Job Orchestrator host bootstrap complete after $UPTIME seconds."
