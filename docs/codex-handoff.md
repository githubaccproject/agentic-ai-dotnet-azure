# TelecomIQ Codex handoff

## Project goal

Build a telecom vendor agreement risk assessment application. The intended workflow compares vendor clauses with approved company policy versions, combines AI findings with deterministic C# checks, and sends evidence to a human reviewer. Use fictional telecom documents throughout.

The repository is `githubaccproject/agentic-ai-dotnet-azure`, branch `main`. The application uses a .NET 10 Web API and Worker Service, React and TypeScript, MongoDB, Azure Blob Storage, Service Bus, Document Intelligence, Azure OpenAI, and Azure AI Search. Bicep and GitHub Actions manage infrastructure using Azure OIDC.

## Verified baseline

| Area | Evidence |
| --- | --- |
| Local setup | React runs on `localhost:5173`. The API and worker run in Visual Studio, with both processes connected to local MongoDB at `127.0.0.1:27017` and database `telecomiq-dev`. |
| Azure inventory | Resource group `rg-telecomiq-dev` has Blob Storage, Service Bus, Azure OpenAI, Document Intelligence, and AI Search. The `contracts` and `policies` containers and indexes exist. The chat deployment uses GPT-4.1 mini and the embedding deployment uses text-embedding-3-small. Consult the [dev connectivity notes](dev-connectivity.md) for the endpoint names, deployment names, and provisioning evidence. |
| Runtime access | Eight scoped role assignments were confirmed for the local developer signed into Azure CLI. They cover the jobs queue, the two containers, the two indexes, Azure OpenAI, and Document Intelligence. Hosted API and worker identities will need their own role assignments. |
| Contract upload and ingestion | The UI showed `Ready` for agreement `1e1e6bbe-211c-4af9-9733-9ecf08cbe61a`. Its PDF was visible in Blob Storage, its MongoDB row had `Status: 2`, and Search Explorer returned two chunks from page 1. The index reports 1,536 dimensions for the vector field. |
| Versioned policy ingestion | Implemented in commit `b325cc5`. The .NET Release build passed. Local checks exercised retry, version isolation, the Swagger routes, and bad PDF rejection. The development MongoDB export matched all seven sample policy PDFs to `Ready` records by SHA-256 hash. One legal policy was uploaded under two IDs. |
| Hosted sign-in | The F1 App Service was provisioned, all 13 outbound addresses were verified in the Atlas IP access list, and GitHub Actions applied Entra authentication. The sign-in endpoint redirected to Microsoft Entra. The hosted application and runtime connectivity have yet to be verified. |
| Build warnings | Existing NuGet vulnerability warnings for Microsoft.OpenApi, SharpCompress, and Snappier remain open. |

The verified contract flow is React upload → API → Blob Storage and MongoDB → `contract-jobs` → worker → Document Intelligence → overlapping C# chunks → Azure OpenAI embeddings → Azure AI Search → MongoDB status → UI. The contract chunker takes up to 1,600 characters at a 1,400-character stride.

## Fictional test files

- [TelecomIQ_Fictional_Vendor_Agreement.pdf](../samples/TelecomIQ_Fictional_Vendor_Agreement.pdf) is the original one-page PDF used for the verified contract ingestion.
- [TelecomIQ_Seven_Sample_Policies.zip](../samples/TelecomIQ_Seven_Sample_Policies.zip) contains the seven fictional policies for the vendor baseline, legal, commercial, network SLA, security, privacy, and disaster recovery. Extract the ZIP and upload individual PDFs.

## Continue in the Windows Codex project

Use the `agentic-ai-dotnet-azure` checkout as the project working directory. Inspect `git status` before pulling and preserve existing local changes. Run the API in Development with its `http` profile and start the worker from the same checkout. Keep local MongoDB running. The [development connectivity guide](dev-connectivity.md) contains the current endpoints and permissions. Existing local profiles use Azure developer sign-in and local MongoDB.

1. **Verify the contract review.** Use the already-Ready agreement in React. If the page has been reset, load the contract ID above through **Resume existing work → Agreement ID → Load**. Click **Start risk review**. Verify `Completed`, the findings or empty result in React, and the matching review record in MongoDB. The review code runs Vendor Legal, Commercial, and Telecom Security & Compliance against retrieved contract chunks. Completion has yet to be observed.
2. **Verify policy ingestion live.** From the API's `/swagger` page, submit one of the policy PDFs to `POST /api/policies/{policyId}/versions/{version}`. Generate a new GUID for that policy and use version `1`. Poll the matching `GET` for `Ready` and confirm the policy ID, version, page, and text in the `policies` index. Submit revisions with the same policy ID and a new version number. Follow the [policy verification procedure](dev-connectivity.md#verify-policy-ingestion).
3. **Verify policy comparison and re-review live.** The Development workflow pins selected ready versions, cites passages from both indexes, records human decisions and reasons, and rechecks prior findings for a linked agreement revision. Follow the [local demo](dev-connectivity.md#verify-policy-comparison-and-re-review). Policy publication approval and deterministic C# rules are future work.
4. **Prepare for hosted operation.** Finish authentication and authorization, retries and reconciliation, idempotency, logging and token tracking, unit/integration/end-to-end tests, hosted MongoDB connectivity, and deployment verification. Record architecture and demonstrate the full workflow once verified.

Use GitHub Actions for Azure operations. Keep spending limits enabled, reuse the existing development resources, and obtain approval before creating additional billable resources. Keep credentials and personal account identifiers out of commits and logs. Record verified milestones in this handoff and the [connectivity progress log](dev-connectivity.md#development-verification).

## Tool calling and MCP (added; live verification pending)
Policy comparison offers three allowlisted read-only tools selected by Azure OpenAI. A private stdio MCP child uses the same executable and trusted review ID, and accesses only selected policy versions or the linked prior review. The calculator takes model-provided numbers; these are not independently verified financial facts. Tool outputs are bounded, calls limited to three per category, and additional policy citations validated against selected versions. Activity is stored on completed reviews and shown in the React UI. Agreement-only review is unchanged. Main pushes now run build, review checks and MCP discovery/calculation checks before publishing to the existing authenticated free App Service. No new Azure resources. Live policy search, revision decisions, and tool-selection behavior must be tested after deployment. The child runs within the existing application trust boundary and inherits the host configuration. Only synthetic agreements are supported.
