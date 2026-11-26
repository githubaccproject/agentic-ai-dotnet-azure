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

The contracts index backs the current code. The policies index holds policy chunks by policy ID and version once policy ingestion is implemented. Pin the exact approved policy versions when creating a review so a later policy update cannot change the review's evidence.

## Runtime roles

| Identity | Role | Scope |
| --- | --- | --- |
| API | Storage Blob Data Contributor | Contracts container |
| API | Azure Service Bus Data Sender | Contract jobs queue |
| Worker | Storage Blob Data Reader | Contracts container |
| Worker | Azure Service Bus Data Receiver | Contract jobs queue |
| Worker | Cognitive Services OpenAI User | OpenAI account |
| Worker | Cognitive Services User | Document Intelligence account |
| Worker | Search Index Data Contributor | Contracts index, plus policies index when policy ingestion ships |
| Deployment | Search Service Contributor | Search service for index schema management |

The API and Worker use `DefaultAzureCredential`. Use the signed-in developer identity for a local demo and separate managed identities when application hosts exist. Identify the principal object IDs before assigning roles. The Azure deployment workflow logged in through its federated principal. Its permissions are independent of the API and Worker.

For local development, point both processes at the same MongoDB instance and database. The local smoke test used `mongodb://127.0.0.1:27017` with `telecomiq-dev`. For a hosted deployment, configure Atlas, a database user scoped to `telecomiq-dev`, and an IP access list for the runtime egress. Store the Atlas connection string in each runtime's secret configuration.


## Development verification

- Verified on 26 November 2026 that the local React UI and API accepted a fictional PDF and showed the document as `Ready`. The upload path writes the PDF to Blob Storage, stores contract metadata in MongoDB, and sends an ingestion job to Service Bus. The worker sets `Ready` after extraction with Document Intelligence, embedding generation with Azure OpenAI, and chunk indexing in Azure AI Search.
- Confirmed eight scoped role assignments for the local developer account across the contract jobs queue, both Blob containers, both search indexes, Azure OpenAI, and Document Intelligence. The ingestion test exercised the contract path.
- Next checkpoint is to start an AI risk review for the ready document and verify that it reaches `Completed` with a stored result. The review chat deployment and findings UI still need this runtime check.
- Policy ingestion and versioning, contract-to-policy comparison, deterministic checks, and human approval are pending. Hosted MongoDB and application deployment also need verification.

See [Azure AI Search RBAC](https://learn.microsoft.com/en-us/azure/search/search-security-rbac) for index scoping and [Azure built-in roles](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles) for Azure data-plane grants.
