targetScope = 'resourceGroup'

param location string = resourceGroup().location
param enableSearch bool = false
param enableDocumentIntelligence bool = false

var suffix = uniqueString(subscription().id, resourceGroup().id)

resource search 'Microsoft.Search/searchServices@2023-11-01' = if (enableSearch) {
  name: 'telecomiq-${suffix}'
  location: location
  sku: {
    name: 'free'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    disableLocalAuth: true
  }
}

resource documents 'Microsoft.CognitiveServices/accounts@2023-05-01' = if (enableDocumentIntelligence) {
  name: 'telecomiq-di-${suffix}'
  location: location
  kind: 'FormRecognizer'
  sku: {
    name: 'F0'
  }
  properties: {
    customSubDomainName: 'telecomiq-di-${suffix}'
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

output searchEndpoint string = enableSearch ? 'https://telecomiq-${suffix}.search.windows.net' : ''
output documentIntelligenceEndpoint string = enableDocumentIntelligence ? 'https://telecomiq-di-${suffix}.cognitiveservices.azure.com' : ''
