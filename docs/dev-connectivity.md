# TelecomIQ development connectivity

Use only fictional agreements and policies. The Web API exposes business endpoints in local Development and in the authenticated App Service demo. See the [hosted demo setup](../deploy/hosted-demo.md).

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
- The MongoDB development export confirmed all seven sample policy PDFs at `Ready` by matching their SHA-256 hashes. The legal policy was indexed twice under different IDs.
- The Development API and worker support selected-policy comparison, recorded reviewer decisions, and re-review of a revised agreement. See the [workflow check](#verify-policy-comparison-and-re-review) below.
- The Release build, focused .NET checks, Development API smoke check, and local MongoDB repository smoke passed. The React workflow passed in Chromium with mocked API responses.
- The live agreement-only review received HTTP 429 in the Worker. The review now runs its categories sequentially, retries throttled JSON requests with Azure's retry hint, and records a throttled failure if the retry budget is exhausted. Live re-verification is pending.
- The F1 hosted demo passed the Entra configuration workflow. An anonymous request received HTTP 401 while the site was starting up, and the explicit sign-in endpoint returned a Microsoft Entra redirect. End-to-end reviewer sign-in and deployment are pending. All 13 outbound App Service IPs were verified in the Atlas access list.
- Live Azure comparison and re-review still need verification. Policy publication approval, deterministic rules, authenticated reviewer roles, and hosted deployment remain separate milestones.

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

## Verify policy comparison and re-review

Keep the API, Worker, frontend, and MongoDB running. The workflow reuses both provisioned search indexes and the existing jobs queue.

1. Open the frontend and load the already `Ready` fictional vendor agreement using **Resume existing work** and its agreement ID. Use the sample agreement if you need to upload it again.
2. Under **AI risk review**, click **Choose policies for comparison**. Select Security Policy v1 using policy ID `2c296d9a-5b4a-4f7a-8f56-8927e221b395` or the ID used for your copy of `05_Security_Policy.pdf`. Start **Compare selected policies**.
3. After `Completed`, inspect Telecom Security & Compliance. Check for a finding about the original agreement's 72-hour deadline after confirmed unauthorized access versus the policy's 24-hour deadline after discovering suspected or confirmed incidents. Check the agreement and policy citations. The policy compares the event that starts the clock as well as the number of hours.
4. Use **Record decision** to request a change, enter a fictional reviewer name and reason, and edit the proposed wording. Reload the same review ID and verify its decision history.
5. Make a fictional revised PDF by replacing the incident reporting clause with language requiring notice within 24 hours of discovering a suspected or confirmed incident affecting operator systems or data. Select that PDF and choose **Upload as revision of this agreement**. The previous review ID fills in automatically. When the revision shows `Ready`, select **Re-review revision**.
6. Under **Previous findings rechecked**, verify the revised clause's citation and the original policy citation. A resolved label is a provisional AI assessment. Insufficient evidence shows **Needs reviewer confirmation**.

Swagger exposes `GET /api/policies` for ready versions, accepts the optional `policies` or `previousReviewId` JSON fields on `POST /api/contracts/{id}/reviews`, and accepts `previousContractId` as an optional multipart field on contract upload. Decisions use `PUT /api/reviews/{id}/decisions/{decisionId}`. The reviewer name is supplied by the user in this Development workflow.

Run the focused review regression checks locally with `dotnet run --project tests/TelecomIQ.ReviewChecks -c Release`. They use local fakes and cover version pinning, unsupported citations, decision retries, prior-review linkage, and legacy MongoDB review deserialization.
