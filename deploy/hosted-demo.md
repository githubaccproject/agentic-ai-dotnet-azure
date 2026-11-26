# TelecomIQ private hosted demo

One Linux App Service **F1 Free** app serves the React frontend and the .NET 10 API from the same origin. It runs the existing Service Bus Worker inside the ASP.NET process while the app is active. The free host can go idle. Keep the page open while a document or review is processing, and use fictional PDFs only.

Use a native **MongoDB Atlas Free (M0)** cluster on Azure with a database user that has `readWrite` on `telecomiq-demo`. Use the Atlas console directly. The existing Azure AI, Storage and Service Bus services still use their normal meters.

## Set up deployment access

1. The `azure-dev` GitHub environment uses the existing OIDC settings for `rg-telecomiq-dev`. On the default branch, run **Actions → TelecomIQ hosted demo → preview**, then `provision`. The bootstrap branch `codex/azure-demo-bootstrap` can also trigger initial F1 provisioning once. The host starts with public network access disabled.
2. Create a single-tenant Entra app registration. Set `https://<hostname>/.auth/login/aad/callback` as its **Web** redirect URI, using the provision output. This is separate from the GitHub OIDC app registration.
3. In the `azure-dev` GitHub environment, add `DEMO_ENTRA_CLIENT_ID` and `DEMO_REVIEWER_OBJECT_ID` as variables and `DEMO_ENTRA_CLIENT_SECRET` as a secret. The reviewer ID is your Entra **user object ID**. Run `configure-auth` to open the hostname with Entra authentication and the reviewer allowlist.

## Connect the hosted runtime

1. In Atlas, create the Free cluster and the database user. Azure Portal **App Service → Properties** lists outbound IP addresses. Allowlist the app's outbound IPs in Atlas. Store the `mongodb+srv://` connection string as the GitHub environment secret `DEMO_MONGO_CONNECTION_STRING`.
2. Create an active `hosted-demo-jobs` queue in the existing Service Bus namespace. Keep it separate from local `contract-jobs` so local and hosted Workers process only jobs whose MongoDB records are in their own database.
3. Using the provisioned App Service's system-assigned identity, grant these data roles. Use the listed resource scope or an authorized inherited scope.

| Resource scope | Role |
| --- | --- |
| `contracts` and `policies` Blob containers | Storage Blob Data Contributor |
| `hosted-demo-jobs` queue | Azure Service Bus Data Sender and Azure Service Bus Data Receiver |
| `contracts` and `policies` search indexes | Search Index Data Contributor |
| Azure OpenAI account | Cognitive Services OpenAI User |
| Document Intelligence account | Cognitive Services User |

4. Run **TelecomIQ hosted demo → publish**. The workflow checks the F1 SKU and requires Entra authentication before deploying. It uploads a Release package with the React build into the app's `wwwroot`. The workflow also checks that anonymous requests cannot access the frontend or policy API.

## Live smoke test

Open the hostname in the deploy summary and sign in as the allowlisted reviewer. The cloud database starts independently from the laptop's MongoDB. Upload a fictional agreement from the frontend. To upload a sample policy with a fresh policy ID, use the authenticated `/swagger` page. Verify that both reach `Ready` and that **Review agreement only** and **Compare selected policies** complete. Open browser developer tools while the page is running to see status requests. The hosting and live pipeline are verified only after these steps succeed on Azure.
