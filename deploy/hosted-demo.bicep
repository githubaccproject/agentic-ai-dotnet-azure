targetScope = 'resourceGroup'

param location string = resourceGroup().location
param appName string = 'telecomiq-demo-${uniqueString(resourceGroup().id)}'
param configureAuth bool = false
param entraClientId string = ''
param reviewerObjectId string = ''
@secure()
param entraClientSecret string = ''

var issuer = '${environment().authentication.loginEndpoint}${tenant().tenantId}/v2.0'

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: { name: 'F1', tier: 'Free' }
  properties: { reserved: true }
}

resource app 'Microsoft.Web/sites@2024-04-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  tags: { workload: 'telecomiq-hosted-demo' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    publicNetworkAccess: 'Disabled'
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: configureAuth ? [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'HostedDemo__ReviewerObjectIds', value: reviewerObjectId }
        { name: 'MICROSOFT_PROVIDER_AUTHENTICATION_SECRET', value: entraClientSecret }
      ] : []
    }
  }
}

resource auth 'Microsoft.Web/sites/config@2024-04-01' = if (configureAuth) {
  parent: app
  name: 'authsettingsV2'
  properties: {
    platform: { enabled: true }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'RedirectToLoginPage'
      redirectToProvider: 'azureactivedirectory'
    }
    httpSettings: { requireHttps: true }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: entraClientId
          clientSecretSettingName: 'MICROSOFT_PROVIDER_AUTHENTICATION_SECRET'
          openIdIssuer: issuer
        }
        validation: {
          defaultAuthorizationPolicy: {
            allowedPrincipals: { identities: [reviewerObjectId] }
          }
        }
      }
    }
  }
}

resource scm 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: app
  name: 'scm'
  properties: { allow: false }
}

resource ftp 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: app
  name: 'ftp'
  properties: { allow: false }
}

output name string = app.name
output hostname string = app.properties.defaultHostName
output principalId string = app.identity.principalId
