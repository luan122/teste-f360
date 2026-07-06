#!/bin/bash
echo -e "\e[36mVerificando dependencias...\e[0m"

# Verifica Docker
if ! command -v docker &> /dev/null; then
    echo -e "\e[31mDocker não encontrado. Por favor, instale o Docker.\e[0m"
    exit 1
fi

# Verifica Python/pip
if ! command -v pip3 &> /dev/null; then
    echo -e "\e[33mPython pip não encontrado. Instalando python3-pip...\e[0m"
    sudo apt-get update
    sudo apt-get install -y python3-pip python3-venv
fi

# Verifica Terraform
if ! command -v terraform &> /dev/null; then
    echo -e "\e[33mTerraform não encontrado. Instalando...\e[0m"
    sudo apt-get update && sudo apt-get install -y gnupg software-properties-common wget
    wget -O- https://apt.releases.hashicorp.com/gpg | \
        gpg --dearmor | \
        sudo tee /usr/share/keyrings/hashicorp-archive-keyring.gpg > /dev/null
    echo "deb [signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] \
        https://apt.releases.hashicorp.com $(lsb_release -cs) main" | \
        sudo tee /etc/apt/sources.list.d/hashicorp.list
    sudo apt-get update && sudo apt-get install terraform -y
fi

# Verifica tflocal
if ! command -v tflocal &> /dev/null; then
    echo -e "\e[33mtflocal não encontrado. Instalando via pip...\e[0m"
    # O --break-system-packages é necessário nas versões mais recentes do Ubuntu (PEP 668)
    pip3 install terraform-local --break-system-packages 2>/dev/null || pip3 install terraform-local
fi

echo -e "\e[36mIniciando LocalStack via Docker...\e[0m"
docker rm -f localstack-terraform-test 2>/dev/null
docker run --rm -d --name localstack-terraform-test -p 4566:4566 -p 4510-4559:4510-4559 localstack/localstack

echo -e "\e[33mAguardando LocalStack iniciar completamente (10s)...\e[0m"
sleep 10

echo -e "\e[32mPronto! O ambiente está configurado.\e[0m"
echo ""
echo -e "\e[36mCopie e cole este comando no seu terminal para exportar as credenciais fake e testar o terraform:\e[0m"
echo 'export AWS_ACCESS_KEY_ID="test" AWS_SECRET_ACCESS_KEY="test" AWS_DEFAULT_REGION="us-east-1"'
echo ""
echo "E execute:"
echo "tflocal init"
echo "tflocal plan -var=\"git_repository_url=https://github.com/teste/teste.git\" -var=\"container_image_api=api:latest\" -var=\"container_image_worker=worker:latest\" -var=\"api_key_secret=dummy\" -var=\"mongo_root_password=dummy\" -var=\"rabbitmq_password=dummy\""
