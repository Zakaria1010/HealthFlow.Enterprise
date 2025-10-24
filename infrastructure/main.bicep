targetScope = 'subscription'

@description('Location for all resources')
param location string = 'eastus'

@description('Environment name')
param environment string

@description('Project name')
param projectName string = 'healthflow'

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: '${projectName}-${environment}-rg'
  location: location
}

module shared './environments/shared/main.bicep' = {
  name: 'sharedResources'
  scope: rg
  params: {
    location: location
    environment: environment
    projectName: projectName
  }
}

module envSpecific './environments/${environment}/main.bicep' = {
  name: 'environmentResources'
  scope: rg
  params: {
    location: location
    environment: environment
    projectName: projectName
    keyVaultName: shared.outputs.keyVaultName
    vnetName: shared.outputs.vnetName
  }
}
