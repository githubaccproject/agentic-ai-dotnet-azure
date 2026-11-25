targetScope = 'resourceGroup'
param location string = resourceGroup().location
param openAiLocation string = location
param chatModel string = 'gpt-4.1-mini'
param chatVersion string = '2025-04-14'
param chatSku string = 'GlobalStandard'
param embeddingSku string = 'Standard'
param embeddingVersion string = '1'
var suffix = uniqueString(subscription().id, resourceGroup().id)
module free './main.bicep' = {
  name: 'telecomiq-free'
  params: {
    location: location
    enableSearch: true
    enableDocumentIntelligence: true
  }
}
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'sttq${suffix}'
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}
resource blob 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}
resource containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = [for name in ['contracts', 'policies']: {
  parent: blob
  name: name
  properties: { publicAccess: 'None' }
}]
resource bus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: 'sb-tq-${suffix}'
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
  properties: { disableLocalAuth: true, minimumTlsVersion: '1.2' }
}
resource queue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: bus
  name: 'contract-jobs'
  properties: { lockDuration: 'PT5M', maxDeliveryCount: 10 }
}
resource ai 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: 'aoai-tq-${suffix}'
  location: openAiLocation
  kind: 'OpenAI'
  sku: { name: 'S0' }
  properties: {
    customSubDomainName: 'aoai-tq-${suffix}'
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}
resource chat 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: ai
  name: 'telecomiq-chat'
  sku: { name: chatSku, capacity: 1 }
  properties: { model: { format: 'OpenAI', name: chatModel, version: chatVersion } }
}
resource embedding 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: ai
  name: 'telecomiq-embedding'
  sku: { name: embeddingSku, capacity: 1 }
  properties: { model: { format: 'OpenAI', name: 'text-embedding-3-small', version: embeddingVersion } }
  dependsOn: [chat]
}
output blobContainer string = '${storage.properties.primaryEndpoints.blob}contracts'
output policyContainer string = '${storage.properties.primaryEndpoints.blob}policies'
output serviceBusNamespace string = '${bus.name}.servicebus.windows.net'
output openAiEndpoint string = ai.properties.endpoint
output searchEndpoint string = free.outputs.searchEndpoint
output documentIntelligenceEndpoint string = free.outputs.documentIntelligenceEndpoint
