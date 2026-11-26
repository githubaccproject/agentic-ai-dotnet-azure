# TelecomIQ development connectivity

Use only fictional agreements and policies. The Web API currently exposes business endpoints only in Development. Run it on a trusted local interface until application authentication and authorization are implemented.

## API and Worker settings

Set configuration through the environment or your local secret store. These Azure endpoints and names were verified in Azure workflow run [#8](https://github.com/githubaccproject/agentic-ai-dotnet-azure/actions/runs/45672014820).

| Setting | Development value |
| --- | --- |
| `Azure__BlobContainer` | `https://sttqfxwt5ietl6nyc.blob.core.windows.net/contracts` |
| `Azure__ServiceBusNamespace` | `sb-tq-fxwt5ietl6nyc.servicebus.windows.net` |
| `Azure__Queue` | `contract-jobs` |
| `Azure__OpenAI` | `https://aoai-tq-fxwt5ietl6nyc.openai.azure.com/` |
| `Azure__ChatDeployment` | `telecomiq-chat` |
| `Azure__EmbeddingDeployment` | `telecomiq-embedding` |
| `Azure__Search` | `https://telecomiq-fxwt5ietl6nyc.search.windows.net` |
| `Azure__SearchIndex` | `contracts` |
| `Azure__DocumentIntelligence` | `https://telecomiq-di-fxwt5ietl6nyc.cognitiveservices.azure.com` |
| `Mongo__Database` | Suggested name `telecomiq-dev` |
| `Mongo__ConnectionString` | Atlas connection string supplied from a secret store |

The contract and policy ingestion paths use separate indexes. The policy index stores chunks by policy ID and version. The current risk review retrieves contract chunks. A future policy comparison must pin the approved policy versions so a later policy update cannot change the review's evidence.

## Runtime roles

| Identity | Role | Scope |
| --- | --- | --- |
| API | Storage Blob Data Contributor | Contracts container |
| API | Azure Service Bus Data Sender | Contract jobs queue |
| API | Storage Blob Data Contributor | Policies container |
| Worker | Storage Blob Data Reader | Contracts container |
| Worker | Storage Blob Data Reader | Policies container |
| Worker | Azure Service Bus Data Receiver | Contract jobs queue |
| Worker | Cognitive Services OpenAI User | OpenAI account |
| Worker | Cognitive Services User | Document Intelligence account |
| Worker | Search Index Data Contributor | Contracts and policies indexes |
| Deployment | Search Service Contributor | Search service for index schema management |

The API and Worker use `DefaultAzureCredential`. Use the signed-in developer identity for a local demo and separate managed identities when application hosts exist. Identify the principal object IDs before assigning roles. The Azure deployment workflow logged in through its federated principal. Its permissions are independent of the API and Worker.

For local development, point both processes at the same MongoDB instance and database. The local smoke test used `mongodb://127.0.0.1:27017` with `telecomiq-dev`. For a hosted deployment, configure Atlas, a database user scoped to `telecomiq-dev`, and an IP access list for the runtime egress. Store the Atlas connection string in each runtime's secret configuration.


## Development verification

- Verified on 26 November 2026 that the local React UI and API accepted a fictional PDF and showed the document as `Ready`. The upload path writes the PDF to Blob Storage, stores contract metadata in MongoDB, and sends an ingestion job to Service Bus. The worker sets `Ready` after extraction with Document Intelligence, embedding generation with Azure OpenAI, and chunk indexing in Azure AI Search.
- Confirmed eight scoped role assignments for the local developer account across the contract jobs queue, both Blob containers, both search indexes, Azure OpenAI, and Document Intelligence. The ingestion test exercised the contract path.
- Next checkpoint is to start an AI risk review for the ready document and verify that it reaches `Completed` with a stored result. The review chat deployment and findings UI still need this runtime check.
- The Development API and worker implement versioned policy ingestion. Verify the pipeline against the existing Azure resources using the steps below.
- Policy approval, contract-to-policy comparison, deterministic checks, and human approval are pending. Hosted MongoDB and application deployment also need verification.

## Verify policy ingestion

Run the API in Development and start the worker from the same checkout. Policy ingestion uses the existing storage account's `policies` container and the `policies` search index. Set `Azure__PolicyBlobContainer` or `Azure__PolicySearchIndex` on both processes if the development resource names differ.

1. Open `/swagger` on the API. Execute `POST /api/policies/{policyId}/versions/{version}` with a new GUID, version `1`, and a fictional policy PDF.
2. Execute `GET /api/policies/{policyId}/versions/{version}` with the same GUID and version until the response shows `Ready`. The worker sets this status after policy chunks are embedded and indexed.
3. In Azure AI Search, open Search Explorer for the `policies` index and run the query below with the same GUID. Confirm that `value` contains the policy text and page numbers.

```json
{
  "search": "*",
  "filter": "policyId eq '11111111-1111-1111-1111-111111111111' and policyVersion eq '1'",
  "select": "policyId,policyVersion,page,text",
  "count": true
}
```

Reposting identical PDF bytes with the same ID and version retries an incomplete upload or returns the current version. A different PDF for an existing version returns HTTP 409. Upload a revision under the same policy ID with the next version number.

See [Azure AI Search RBAC](https://learn.microsoft.com/en-us/azure/search/search-security-rbac) for index scoping and [Azure built-in roles](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles) for Azure data-plane grants.
