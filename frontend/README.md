# ContractIQ React UI

Requires Node.js 22 LTS (22.12+ recommended) and npm. From frontend:

    npm install
    npm run dev

Open http://localhost:5173. Run the .NET API on http://localhost:5080 using the http launch profile, and run the Worker separately with Azure and MongoDB configuration. The Vite development proxy forwards /api to the backend, so no CORS change is needed for local development. For production, configure a same-origin reverse proxy and authentication; the backend currently exposes business endpoints only in Development.

Features: PDF upload, contract status polling, risk review submission and polling, severity counts, category filtering, evidence IDs and resume by GUID. No fake results. There is no list endpoint, evidence text endpoint or human approval endpoint yet. No credentials belong in the frontend. Upload/review requires working backend dependencies.

Run npm run build for TypeScript checking and production bundling. Dependency installation, build and live integration have not been verified by the assistant. Commit the generated package-lock.json after installation.
