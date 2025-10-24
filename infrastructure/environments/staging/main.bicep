param location string
param environment string
param projectName string
param keyVaultName string
param vnetName string

// SQL Server
module sqlServer '../modules/sql-database/main.bicep' = {
  name: 'sqlServer'
  params: {
    location: location
    environment: environment
    projectName: projectName
    keyVaultName: keyVaultName
    vnetName: vnetName
    skuName: 'GP_S_Gen5_1'
  }
}

// Cosmos DB
module cosmosDb '../modules/cosmos-db/main.bicep' = {
  name: 'cosmosDb'
  params: {
    location: location
    environment: environment
    projectName: projectName
    keyVaultName: keyVaultName
  }
}

// RabbitMQ
module rabbitmq '../modules/rabbitmq/main.bicep' = {
  name: 'rabbitmq'
  params: {
    location: location
    environment: environment
    projectName: projectName
    vnetName: vnetName
  }
}

// Container Apps Environment
resource containerAppEnv 'Microsoft.App/managedEnvironments@2022-03-01' = {
  name: '${projectName}-${environment}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
    vnetConfiguration: {
      internal: false
    }
  }
}

// Container Apps for each service
module gatewayApp '../modules/container-app/main.bicep' = {
  name: 'gatewayApp'
  params: {
    location: location
    environment: environment
    projectName: projectName
    containerAppEnvName: containerAppEnv.name
    serviceName: 'gateway'
    imageName: '${projectName}/gateway'
    keyVaultName: keyVaultName
  }
}

// Repeat for other services: patients, analytics, notifications, background-worker
