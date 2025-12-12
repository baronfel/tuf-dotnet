// Azure App Service deployment for Production TUF Server
// Provides a fully managed platform with auto-scaling and integrated monitoring

@description('Name of the App Service plan')
param appServicePlanName string = 'tuf-server-plan-${uniqueString(resourceGroup().id)}'

@description('Name of the App Service')
param appServiceName string = 'tuf-server-${uniqueString(resourceGroup().id)}'

@description('Location for all resources')
param location string = resourceGroup().location

@description('SKU for the App Service Plan')
@allowed([
  'B1'
  'B2'
  'B3'
  'S1'
  'S2'
  'S3'
  'P1V3'
  'P2V3'
  'P3V3'
])
param skuName string = 'B1'

@description('Container image to deploy')
param containerImage string = 'your-registry/production-tuf-server:latest'

@description('Container registry server')
param containerRegistryServer string = 'your-registry.azurecr.io'

@description('Container registry username')
@secure()
param containerRegistryUsername string = ''

@description('Container registry password')
@secure()
param containerRegistryPassword string = ''

@description('CORS allowed origins (comma-separated)')
param corsAllowedOrigins string = '*'

@description('Storage account name for TUF repository data')
param storageAccountName string = 'tufrepo${uniqueString(resourceGroup().id)}'

@description('Enable Application Insights')
param enableApplicationInsights bool = true

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

// Application Insights for monitoring
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = if (enableApplicationInsights) {
  name: '${appServiceName}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
  }
}

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: skuName
  }
  kind: 'linux'
  properties: {
    reserved: true // Required for Linux
  }
}

// App Service with container deployment
resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appServiceName
  location: location
  kind: 'app,linux,container'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerImage}'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
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
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${containerRegistryServer}'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_USERNAME'
          value: containerRegistryUsername
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_PASSWORD'
          value: containerRegistryPassword
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: enableApplicationInsights ? applicationInsights.properties.ConnectionString : ''
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
      ]
      azureStorageAccounts: {
        metadata: {
          type: 'AzureFiles'
          accountName: storageAccount.name
          shareName: 'metadata'
          accessKey: storageAccount.listKeys().keys[0].value
          mountPath: '/mnt/metadata'
        }
        targets: {
          type: 'AzureFiles'
          accountName: storageAccount.name
          shareName: 'targets'
          accessKey: storageAccount.listKeys().keys[0].value
          mountPath: '/mnt/targets'
        }
      }
      healthCheckPath: '/health'
    }
  }
}

// Auto-scaling settings
resource autoScaleSettings 'Microsoft.Insights/autoscalesettings@2022-10-01' = {
  name: '${appServiceName}-autoscale'
  location: location
  properties: {
    enabled: true
    targetResourceUri: appServicePlan.id
    profiles: [
      {
        name: 'DefaultAutoscaleProfile'
        capacity: {
          minimum: '1'
          maximum: '3'
          default: '1'
        }
        rules: [
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricNamespace: 'Microsoft.Web/serverfarms'
              metricResourceUri: appServicePlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT5M'
              timeAggregation: 'Average'
              operator: 'GreaterThan'
              threshold: 70
            }
            scaleAction: {
              direction: 'Increase'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT5M'
            }
          }
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricNamespace: 'Microsoft.Web/serverfarms'
              metricResourceUri: appServicePlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT5M'
              timeAggregation: 'Average'
              operator: 'LessThan'
              threshold: 30
            }
            scaleAction: {
              direction: 'Decrease'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT5M'
            }
          }
        ]
      }
    ]
  }
}

// Outputs
output appServiceName string = appService.name
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output storageAccountName string = storageAccount.name
output applicationInsightsName string = enableApplicationInsights ? applicationInsights.name : ''
output applicationInsightsInstrumentationKey string = enableApplicationInsights ? applicationInsights.properties.InstrumentationKey : ''