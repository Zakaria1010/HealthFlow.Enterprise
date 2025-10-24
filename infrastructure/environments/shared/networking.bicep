param location string
param environment string
param projectName string

resource vnet 'Microsoft.Network/virtualNetworks@2023-05-01' = {
  name: '${projectName}-${environment}-vnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'services'
        properties: {
          addressPrefix: '10.0.1.0/24'
        }
      }
      {
        name: 'databases'
        properties: {
          addressPrefix: '10.0.2.0/24'
      }
      }
    ]
  }
}

output vnetName string = vnet.name
output vnetId string = vnet.id
