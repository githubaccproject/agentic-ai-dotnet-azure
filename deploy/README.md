# Zero-budget development infrastructure

The default deployment creates only an empty resource group. Optional resources are strictly Azure AI Search Free and Document Intelligence F0. Availability, quotas and trial eligibility depend on the subscription and region. Deployment fails rather than switching to a paid SKU. No guarantee of zero charges for other account activity. Do not upgrade the trial or any resource tier for this project.

## Azure Cloud Shell (Bash)
Sign in at https://shell.azure.com and select your trial subscription using az account set --subscription <subscription-id>. Clone the repository and enter its root. Run:

    bash deploy/deploy.sh apply
    ENABLE_SEARCH=true ENABLE_DOCUMENT_INTELLIGENCE=true bash deploy/deploy.sh preview
    ENABLE_SEARCH=true ENABLE_DOCUMENT_INTELLIGENCE=true bash deploy/deploy.sh apply

The last two commands are optional. Review the preview before applying. If either free SKU is unavailable, leave its flag false. Change AZURE_LOCATION if needed. Free tier quotas are limited. The default region is centralindia.

## Access
Templates disable local key authentication. Assign Search Index Data Contributor and Search Service Contributor as needed to the development identity for indexing and index creation, and Cognitive Services User for Document Intelligence. Role assignments are intentionally not automated; an authorized administrator must grant least privilege. Create the index using search-index.json after configuring access. Use az login locally.

## Application limitations
The current .NET implementation still expects real Azure Blob, Service Bus, Azure OpenAI and MongoDB endpoints. Local Azurite, Service Bus emulator, local MongoDB and a local AI provider require additional configuration and code changes (especially credential and AI provider adapters). These are NOT included or working yet. The Bicep deployment alone cannot run the complete pipeline. No paid resources are created by these templates. Never commit secrets.

## Cleanup
Only if this is a dedicated disposable group and you have checked all its contents:

    az group delete --name rg-telecomiq-dev

This deletes every resource in the group. Bicep has not been compiled or deployed in an Azure subscription by the assistant.
