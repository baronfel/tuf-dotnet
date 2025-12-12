// Azure Container Instances deployment for Production TUF Server
// Provides a simple, serverless container hosting solution

@description('Name of the container group')
param containerGroupName string = 'tuf-server-${uniqueString(resourceGroup().id)}'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Container image to deploy')
param containerImage string = 'your-registry/production-tuf-server:latest'

@description('Number of CPU cores for the container')
@allowed([
  1
  2
  4
])
param cpuCores int = 1

@description('Amount of memory for the container in GB')
@allowed([
  1
  2
  4
  8
])
param memoryInGb int = 2

@description('DNS name label for the public IP')
param dnsNameLabel string = 'tuf-server-${uniqueString(resourceGroup().id)}'

@description('Port to expose')
param port int = 8080

@description('CORS allowed origins (comma-separated)')
param corsAllowedOrigins string = '*'

@description('Storage account name for TUF repository data')
param storageAccountName string = 'tufrepo${uniqueString(resourceGroup().id)}'

// Create storage account for TUF repository data
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    encryption: {
      services: {
        blob: {
          enabled: true
        }
        file: {
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

// Create file shares for metadata and targets
resource metadataFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  name: '${storageAccount.name}/default/metadata'
  properties: {
    shareQuota: 5 // 5 GB quota
  }
}

resource targetsFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  name: '${storageAccount.name}/default/targets'
  properties: {
    shareQuota: 50 // 50 GB quota
  }
}

// Container group with TUF server
resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2023-05-01' = {
  name: containerGroupName
  location: location
  properties: {
    containers: [
      {
        name: 'tuf-server'
        properties: {
          image: containerImage
          ports: [
            {
              port: port
              protocol: 'TCP'
            }
          ]
          environmentVariables: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:${port}'
            }
            {
              name: 'TUF_METADATA_PATH'
              value: '/mnt/metadata'
            }
            {
              name: 'TUF_TARGETS_PATH'
              value: '/mnt/targets'
            }
            {
              name: 'CORS__AllowedOrigins__0'
              value: corsAllowedOrigins
            }
          ]
          resources: {
            requests: {
              cpu: cpuCores
              memoryInGB: memoryInGb
            }
          }
          volumeMounts: [
            {
              name: 'metadata-volume'
              mountPath: '/mnt/metadata'
              readOnly: true
            }
            {
              name: 'targets-volume'
              mountPath: '/mnt/targets'
              readOnly: true
            }
          ]
          livenessProbe: {
            httpGet: {
              path: '/health'
              port: port
              scheme: 'HTTP'
            }
            initialDelaySeconds: 30
            periodSeconds: 30
            timeoutSeconds: 10
            failureThreshold: 3
          }
          readinessProbe: {
            httpGet: {
              path: '/ready'
              port: port
              scheme: 'HTTP'
            }
            initialDelaySeconds: 10
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
          }
        }
      }
    ]
    osType: 'Linux'
    restartPolicy: 'Always'
    ipAddress: {
      type: 'Public'
      ports: [
        {
          port: port
          protocol: 'TCP'
        }
      ]
      dnsNameLabel: dnsNameLabel
    }
    volumes: [
      {
        name: 'metadata-volume'
        azureFile: {
          shareName: 'metadata'
          storageAccountName: storageAccount.name
          storageAccountKey: storageAccount.listKeys().keys[0].value
          readOnly: true
        }
      }
      {
        name: 'targets-volume'
        azureFile: {
          shareName: 'targets'
          storageAccountName: storageAccount.name
          storageAccountKey: storageAccount.listKeys().keys[0].value
          readOnly: true
        }
      }
    ]
  }
}

// Outputs
output containerGroupName string = containerGroup.name
output fqdn string = containerGroup.properties.ipAddress.fqdn
output ipAddress string = containerGroup.properties.ipAddress.ip
output storageAccountName string = storageAccount.name
output metadataFileShareName string = 'metadata'
output targetsFileShareName string = 'targets'