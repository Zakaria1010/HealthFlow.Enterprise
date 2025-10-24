param location string
param environment string
param projectName string
param containerAppEnvName string
param serviceName string
param imageName string
param keyVaultName string
param replicas int = 1

var containerAppName = '${projectName}-${serviceName}-${environment}'

resource containerApp 'Microsoft.App/containerApps@2022-03-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: resourceId('Microsoft.App/managedEnvironments', containerAppEnvName)
    configuration: {
      ingress: {
        external: true
        targetPort: serviceName == 'gateway' ? 80 : 8080
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
      secrets: [
        {
          name: 'registry-credentials'
          value: '${keyVaultName}'
        }
      ]
    }
    template: {
      containers: [
        {
          name: serviceName
          image: '${containerRegistry}/${imageName}:latest'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environment
            }
            {
              name: 'KeyVaultName'
              value: keyVaultName
            }
          ]
        }
      ]
      scale: {
        minReplicas: replicas
        maxReplicas: environment == 'production' ? 10 : 3
      }
    }
  }
}

output containerAppName string = containerApp.name
output containerAppUrl string = containerApp.properties.configuration.ingress.fqdn
