# TelecomIQ — Telecom Vendor Risk Intelligence
Reference implementation for telecom vendor agreement risk review, inspired by telecom operator workflows. Not affiliated with T-Mobile. Synthetic documents only. Not production-ready.
## Flow
Development API uploads PDF to Blob, saves Mongo metadata and sends a Service Bus job. Worker extracts pages with Document Intelligence, chunks and embeds text, indexes Azure AI Search. Review jobs run three parallel agents (Vendor Legal, Commercial, Telecom Security & Compliance) using hybrid retrieval and Azure OpenAI JSON output. Findings are filtered against retrieved evidence IDs; human review is required.
## Naming and compatibility
TelecomIQ is the business-facing and internal project name. Existing API routes and contract data schema are retained for compatibility. Azure resource names now use telecomiq; existing resources are not renamed automatically. The React UI is in frontend/; see frontend/README.md.
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
Run dotnet build TelecomIQ.slnx, then dotnet run --project src/TelecomIQ.Worker and ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/TelecomIQ.Api. Use local trusted environment only. POST multipart file to /api/contracts, poll GET /api/contracts/{id}, POST /api/contracts/{id}/reviews once Ready, poll GET /api/reviews/{id}.
## Limitations
API business endpoints intentionally disabled outside Development until authentication, tenant authorization and malware scanning are implemented. Queue is sent directly by API, not Event Grid; no transactional outbox, so upload/metadata/queue failures need reconciliation. Worker is a hosted Service Bus consumer, not Azure Functions. Chunking is character-based with overlap. No claim of semantic citation verification, exhaustive risk coverage, exactly-once execution or legal advice. Infrastructure provisioning, tests, AI evaluation, deployment, retry policy, robust concurrency and human approval UI remain pending. Azure integration and build must be verified.
