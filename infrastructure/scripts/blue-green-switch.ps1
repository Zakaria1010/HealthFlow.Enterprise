param(
    [string]$Environment = "production",
    [string]$TrafficPercentage = "100"
)

$ProjectName = "healthflow"

Write-Host "Switching traffic to green deployment..." -ForegroundColor Green

# Update traffic routing for each service
$services = @("gateway", "patients", "analytics", "notifications")

foreach ($service in $services) {
    $appName = "$ProjectName-$service-$Environment"
    
    Write-Host "Updating traffic for $appName..." -ForegroundColor Yellow
    
    az containerapp ingress traffic set `
        --name $appName `
        --resource-group "$ProjectName-$Environment-rg" `
        --revision-weight "green=$TrafficPercentage" "blue=0"
}

Write-Host "Traffic switch completed!" -ForegroundColor Green