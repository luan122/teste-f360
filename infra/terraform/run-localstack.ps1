# run-localstack.ps1
Write-Host "Verificando dependencias..." -ForegroundColor Cyan

# Verifica se o Docker esta rodando
if (!(Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Error "Docker não encontrado. Por favor, instale o Docker Desktop."
    exit 1
}

# Verifica se o Python esta instalado
if (!(Get-Command pip -ErrorAction SilentlyContinue)) {
    Write-Error "Python/pip não encontrado. Por favor, instale o Python pelo site oficial ou Microsoft Store."
    exit 1
}

# Verifica se o Terraform esta instalado
if (!(Get-Command terraform -ErrorAction SilentlyContinue)) {
    Write-Host "Terraform não encontrado. Instalando via Winget..." -ForegroundColor Yellow
    winget install Hashicorp.Terraform --accept-source-agreements --accept-package-agreements
    
    # Adiciona o diretório ao PATH da sessão atual
    $env:Path += ";C:\Program Files\HashiCorp\Terraform\"
}

# Verifica se o tflocal esta instalado
if (!(Get-Command tflocal -ErrorAction SilentlyContinue)) {
    Write-Host "tflocal não encontrado. Instalando via pip..." -ForegroundColor Yellow
    pip install terraform-local
}

Write-Host "Iniciando LocalStack via Docker..." -ForegroundColor Cyan
# Para container antigo se existir
docker rm -f localstack-terraform-test 2>$null
# Inicia LocalStack
docker run --rm -d --name localstack-terraform-test -p 4566:4566 -p 4510-4559:4510-4559 localstack/localstack

Write-Host "Configurando variáveis de ambiente AWS fake para esta sessão..." -ForegroundColor Cyan
[Environment]::SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test", "Process")
[Environment]::SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test", "Process")
[Environment]::SetEnvironmentVariable("AWS_DEFAULT_REGION", "us-east-1", "Process")

Write-Host "Aguardando LocalStack iniciar completamente (10s)..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

Write-Host "Pronto! O ambiente está configurado." -ForegroundColor Green
Write-Host ""
Write-Host "Execute os seguintes comandos para testar a infraestrutura:" -ForegroundColor Cyan
Write-Host "tflocal init"
Write-Host "tflocal plan -var=""git_repository_url=https://github.com/teste/teste.git"" -var=""container_image_api=api:latest"" -var=""container_image_worker=worker:latest"" -var=""api_key_secret=dummy"" -var=""mongo_root_password=dummy"" -var=""rabbitmq_password=dummy"""
