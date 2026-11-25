#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
GROUP="${RESOURCE_GROUP:-rg-contractiq-dev}"
LOCATION="${AZURE_LOCATION:-centralindia}"
SEARCH="${ENABLE_SEARCH:-false}"
DOCUMENTS="${ENABLE_DOCUMENT_INTELLIGENCE:-false}"
MODE="${1:-preview}"
[[ "$SEARCH" == true || "$SEARCH" == false ]] || exit 2
[[ "$DOCUMENTS" == true || "$DOCUMENTS" == false ]] || exit 2
[[ "$MODE" == preview || "$MODE" == apply ]] || { echo "Usage: bash deploy.sh [preview|apply]"; exit 2; }
az account show --query '{subscription:name,id:id}' -o table
if [[ "$MODE" == preview ]]; then
  if [[ "$(az group exists --name "$GROUP")" != true ]]; then
    echo "Resource group does not exist. Preview creates nothing. Run apply with both flags false to create the empty group first."
    exit 0
  fi
  az deployment group what-if --resource-group "$GROUP" --template-file main.bicep --parameters location="$LOCATION" enableSearch="$SEARCH" enableDocumentIntelligence="$DOCUMENTS"
else
  echo "Only Free Search and F0 Document Intelligence are permitted. No paid fallback."
  read -r -p "Type DEPLOY to continue: " confirm
  [[ "$confirm" == DEPLOY ]] || exit 1
  az group create --name "$GROUP" --location "$LOCATION" -o none
  az deployment group create --resource-group "$GROUP" --template-file main.bicep --parameters location="$LOCATION" enableSearch="$SEARCH" enableDocumentIntelligence="$DOCUMENTS" --query properties.outputs -o json
fi
