# Agentic AI .NET Azure

ContractIQ: enterprise contract risk review reference application using .NET 10 and Azure. Synthetic documents only.

## Getting started
Install the .NET 10 SDK, then run `dotnet build ContractIQ.slnx` and `dotnet run --project src/ContractIQ.Api`. The API exposes `/health`.

## Architecture
API and Worker host Application workflows. Domain contains business models; Infrastructure implements external integrations.

## Status
Phase 1 foundation only. Worker is a generic host placeholder; Azure Functions, Blob Storage, Service Bus, Document Intelligence, Azure OpenAI, Azure AI Search, MongoDB, agents and tests are planned, not implemented.
