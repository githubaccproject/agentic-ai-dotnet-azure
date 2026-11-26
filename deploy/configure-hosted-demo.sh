#!/usr/bin/env bash
set -euo pipefail

: "${RESOURCE_GROUP:?Set RESOURCE_GROUP}"
: "${DEPLOYMENT:?Set DEPLOYMENT}"
: "${QUEUE:?Set QUEUE}"
: "${GITHUB_OUTPUT:?Run from the hosted demo workflow}"
: "${MONGO_URL:?Set DEMO_MONGO_CONNECTION_STRING in the azure-dev environment}"
[[ "$MONGO_URL" =~ ^mongodb\+srv://[^/]+\.mongodb\.net(/|$) ]] || {
  echo "Use the MongoDB Atlas SRV URL for this demo."
  exit 1
}

outputs=$(az deployment group show -g "$RESOURCE_GROUP" -n "$DEPLOYMENT" --query properties.outputs -o json)
app=$(jq -er '.name.value' <<< "$outputs")
host=$(jq -er '.hostname.value' <<< "$outputs")
site=$(az webapp show -g "$RESOURCE_GROUP" -n "$app" --query id -o tsv)
plan=$(az webapp show -g "$RESOURCE_GROUP" -n "$app" --query serverFarmId -o tsv)
sku=$(az resource show --ids "$plan" --query sku.name -o tsv)
[[ "$sku" == F1 ]] || { echo "Expected the F1 free App Service plan."; exit 1; }

arm=$(az cloud show --query endpoints.resourceManager -o tsv)
az rest --method get --url "${arm%/}$site/config/authsettingsV2/list?api-version=2024-04-01" -o json |
  jq -e '.properties | .platform.enabled == true and .globalValidation.requireAuthentication == true and
    ((.globalValidation.excludedPaths // []) | length == 0) and
    .identityProviders.azureActiveDirectory.enabled == true and
    (.identityProviders.azureActiveDirectory.validation.defaultAuthorizationPolicy.allowedPrincipals.identities | length > 0)' >/dev/null

ai=$(az deployment group show -g "$RESOURCE_GROUP" -n trial --query properties.outputs -o json)
bus=$(jq -er '.serviceBusNamespace.value' <<< "$ai")
az servicebus queue show -g "$RESOURCE_GROUP" --namespace-name "${bus%%.*}" -n "$QUEUE" --query status -o tsv |
  grep -qx Active || { echo "Create the dedicated hosted-demo queue before publishing."; exit 1; }

config=$(mktemp)
trap 'rm -f -- "$config"' EXIT
jq --arg queue "$QUEUE" '{
  "HostedDemo__Enabled": "true",
  "Mongo__ConnectionString": env.MONGO_URL,
  "Mongo__Database": "telecomiq-demo",
  "Azure__BlobContainer": .blobContainer.value,
  "Azure__PolicyBlobContainer": .policyContainer.value,
  "Azure__ServiceBusNamespace": .serviceBusNamespace.value,
  "Azure__Queue": $queue,
  "Azure__OpenAI": .openAiEndpoint.value,
  "Azure__ChatDeployment": "telecomiq-chat",
  "Azure__EmbeddingDeployment": "telecomiq-embedding",
  "Azure__Search": .searchEndpoint.value,
  "Azure__SearchIndex": "contracts",
  "Azure__PolicySearchIndex": "policies",
  "Azure__DocumentIntelligence": .documentIntelligenceEndpoint.value
}' <<< "$ai" > "$config"
az webapp config appsettings set -g "$RESOURCE_GROUP" -n "$app" --settings "@$config" -o none
printf 'app_name=%s\nhostname=%s\n' "$app" "$host" >> "$GITHUB_OUTPUT"
