param location string
param environment string
param projectName string

// Networking
module networking './networking.bicep' = {
  name: 'networking'
  params: {
    location: location
    environment: environment
    projectName: projectName
  }
}

// Key Vault
module keyvault './keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    environment: environment
    projectName: projectName
  }
}

output keyVaultName string = keyvault.outputs.keyVaultName
output vnetName string = networking.outputs.vnetName
