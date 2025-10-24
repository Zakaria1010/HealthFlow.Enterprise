param(
    [string]$Environment = "staging",
    [string]$Location = "eastus",
    [switch]$WhatIf = $false
)

$ProjectName = "healthflow"
$ResourceGroup = "$ProjectName-$Environment-rg"

Write-Host "Deploying infrastructure to $Environment environment..." -ForegroundColor Green

# Validate Bicep files
Write-Host "Validating Bicep templates..." -ForegroundColor Yellow
az bicep build --file infrastructure/main.bicep

if ($LASTEXITCODE -ne 0) {
    Write-Error "Bicep validation failed"
    exit 1
}

# Create resource group if it doesn't exist
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "false") {
    Write-Host "Creating resource group: $ResourceGroup" -ForegroundColor Yellow
    az group create --name $ResourceGroup --location $Location
}

# Deploy infrastructure
Write-Host "Starting deployment..." -ForegroundColor Yellow
if ($WhatIf) {
    az deployment sub what-if `
        --name "deploy-$ProjectName-$Environment-$((Get-Date).ToString('yyyyMMdd-HHmmss'))" `
        --location $Location `
        --template-file infrastructure/main.bicep `
        --parameters infrastructure/environments/$Environment/parameters.$Environment.json `
        --parameters projectName=$ProjectName environment=$Environment
} else {
    az deployment sub create `
        --name "deploy-$ProjectName-$Environment-$((Get-Date).ToString('yyyyMMdd-HHmmss'))" `
        --location $Location `
        --template-file infrastructure/main.bicep `
        --parameters infrastructure/environments/$Environment/parameters.$Environment.json `
        --parameters projectName=$ProjectName environment=$Environment
        
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Deployment completed successfully!" -ForegroundColor Green
    } else {
        Write-Error "Deployment failed"
        exit 1
    }
}