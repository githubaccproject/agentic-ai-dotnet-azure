# ContractIQ — Agentic AI .NET Azure
Reference implementation for contract risk review. Synthetic documents only. Not production-ready.
## Flow
Development API uploads PDF to Blob, saves Mongo metadata and sends a Service Bus job. Worker extracts pages with Document Intelligence, chunks and embeds text, indexes Azure AI Search. Review jobs run three parallel category-specific agents using hybrid retrieval and Azure OpenAI JSON output. Findings are filtered against retrieved evidence IDs; human review is required.
## Setup
.NET 10 SDK; provision Blob container, Service Bus queue, MongoDB, Document Intelligence, Azure OpenAI chat and text-embedding-3-small (1536 dimensions), Azure AI Search. Create search index from deploy/search-index.json using Search REST API 2024-07-01. Assign appropriate data-plane RBAC roles to the developer identity and worker/API identities. Run az login locally. Set environment variables for both hosts:
- Azure__BlobContainer (full container URL)
- Azure__ServiceBusNamespace (fully qualified namespace)
- Azure__Queue
- Azure__OpenAI (endpoint)
- Azure__ChatDeployment
- Azure__EmbeddingDeployment
- Azure__Search (endpoint)
- Azure__SearchIndex (contracts)
- Azure__DocumentIntelligence (endpoint)
- Mongo__ConnectionString (secret; never commit)
- Mongo__Database
Run dotnet build ContractIQ.slnx, then dotnet run --project src/ContractIQ.Worker and ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/ContractIQ.Api. Use local trusted environment only. POST multipart file to /api/contracts, poll GET /api/contracts/{id}, POST /api/contracts/{id}/reviews once Ready, poll GET /api/reviews/{id}.
## Limitations
API business endpoints intentionally disabled outside Development until authentication, tenant authorization and malware scanning are implemented. Queue is sent directly by API, not Event Grid; no transactional outbox, so upload/metadata/queue failures need reconciliation. Worker is a hosted Service Bus consumer, not Azure Functions. Chunking is character-based with overlap. No claim of semantic citation verification, exhaustive risk coverage, exactly-once execution or legal advice. Infrastructure provisioning, tests, AI evaluation, deployment, retry policy, robust concurrency and human approval UI remain pending. Azure integration and build must be verified.
