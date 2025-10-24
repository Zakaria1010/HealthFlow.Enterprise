param location string
param environment string
param projectName string

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: '${projectName}-${environment}-kv'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    enableSoftDelete: true
  }
}

output keyVaultName string = keyVault.name
