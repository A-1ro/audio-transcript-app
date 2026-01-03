targetScope = 'resourceGroup'

@description('Existing Azure Functions app name.')
param functionAppName string = 'dev-func-transcript-app'

@description('Existing Storage account name.')
param storageAccountName string = 'audiostrage'

@description('Existing Application Insights name.')
param appInsightsName string = 'dev-func-transcript-app'

@description('Existing Cosmos DB account name.')
param cosmosAccountName string = 'dev-cosmosdb-transcription-app'

@description('Existing Speech account name.')
param speechAccountName string = 'speech-batch-transcript'

resource functionApp 'Microsoft.Web/sites@2023-12-01' existing = {
  name: functionAppName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = {
  name: cosmosAccountName
}

resource speechAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = {
  name: speechAccountName
}

@description('Used by azd deploy to locate the Function App.')
output AZURE_FUNCTION_APP_NAME string = functionApp.name

@description('Resource group used for deployment.')
output AZURE_RESOURCE_GROUP string = resourceGroup().name

@description('Azure region of the resource group.')
output AZURE_LOCATION string = resourceGroup().location
