import { useRef } from 'react';

type Status = 'Covered' | 'Basic / partial' | 'Next steps';
type Topic = {name: string; detail: string; status: Status};
const covered = (name:string, detail:string):Topic => ({name,detail,status:'Covered'});
const partial = (name:string, detail:string):Topic => ({name,detail,status:'Basic / partial'});
const next = (name:string, detail:string):Topic => ({name,detail,status:'Next steps'});
const groups: {title:string; topics:Topic[]}[] = [
  {title:'01 · AI and document intelligence',topics:[
    covered('LLM & prompt engineering','Azure OpenAI chat, system prompts and JSON responses.'),
    covered('PDF ingestion & text extraction','Azure Document Intelligence extracts text by page.'),
    covered('Chunking & embeddings','1,600-character chunks with 200-character overlap; Azure OpenAI embeddings.'),
    covered('Vector database & hybrid search','Azure AI Search, HNSW, keyword + vector retrieval, 1,536-dimensional index.'),
    covered('RAG & policy comparison','Agreement and pinned policy evidence with source page citations.'),
    partial('Agentic RAG','Optional adaptive policy retrieval through model-selected MCP tools.'),
    next('Advanced retrieval','Semantic reranking, semantic chunking and retrieval quality benchmarks.')
  ]},
  {title:'02 · Agents and business tools',topics:[
    covered('Agent orchestration','RiskReviewOrchestrator coordinates three sequential specialist review roles.'),
    partial('Multi-agent workflow','Legal, Commercial and Security & Compliance roles share agent implementations; no autonomous delegation.'),
    covered('LLM function calling','Model-selected read-only business functions with argument and call limits.'),
    covered('MCP client & server','Private stdio tool discovery and execution: policy lookup, prior decisions and liability calculator.'),
    covered('Human-in-the-loop','Human records risk decisions, reasons and proposed changes.'),
    covered('Revision analysis','Recheck previous findings against the revised agreement and pinned policies.'),
    next('Advanced agent collaboration','Independent supervisor, delegation, reflection and controlled parallel execution.')
  ]},
  {title:'03 · Conversational AI',topics:[
    covered('Contract-aware chatbot','React assistant scoped to a ready agreement and optional completed review.'),
    covered('Conversation memory','MongoDB chat history, recent turns and bounded conversation summary.'),
    covered('Query rewriting & intent classification','Follow-up questions rewritten into standalone queries and specialist route labels.'),
    partial('Dynamic routing','Specialist prompt selection; not independent agent delegation.'),
    covered('Conversational RAG & MCP','Hybrid retrieval and optional tools with a completed review scope.'),
    partial('Streaming','Server-Sent Events for progress and final validated answer; no raw token streaming.'),
    next('Semantic caching','Not implemented.')
  ]},
  {title:'04 · Responsible AI and observability',topics:[
    covered('Evidence validation','Known citation IDs, selected policy versions and source passages checked.'),
    partial('Guardrails & prompt injection defense','Bounded inputs, tool allowlist, untrusted-evidence instructions and output validation.'),
    partial('AI evaluation','Model-based relevance and groundedness gate; not independently benchmarked.'),
    covered('Application monitoring','Application Insights requests/errors, structured logs and tool duration/status.'),
    partial('Tokens & cost analytics','Reported chat token usage; some MCP usage excluded; cost requires configured rates.'),
    partial('OpenTelemetry instrumentation','ActivitySource and Meter signals; exporter/collector setup remains open.'),
    next('Production AI safety','Dedicated Content Safety, Prompt Shields, PII protection and benchmarked evaluation.')
  ]},
  {title:'05 · Enterprise engineering and Azure',topics:[
    covered('Application architecture','.NET 10, C#, ASP.NET Core, React, TypeScript, layered architecture.'),
    covered('Asynchronous processing','Azure Service Bus and background worker for ingestion and review jobs.'),
    covered('Storage & persistence','Azure Blob Storage, MongoDB Atlas and separate AI Search indexes.'),
    covered('Identity & deployment','Entra ID demo allowlist, Azure managed identity, GitHub Actions, Bicep and App Service.'),
    partial('Resilience','HTTP 429 retries, bounded calls, basic dead-letter handling and partial idempotency.'),
    next('Production hardening','Document-level RBAC, transactional outbox, circuit breakers, load tests and retention controls.')
  ]}
];
const all = groups.flatMap(g=>g.topics);
export default function ProjectTopics() {
  const dialog = useRef<HTMLDialogElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  function open(){dialog.current?.showModal();}
  function close(){dialog.current?.close();}
  return <div className="project-topics">
    <div className="topics-trigger-wrap">
      <button ref={trigger} type="button" className="secondary topics-trigger" aria-haspopup="dialog"
        aria-describedby="topics-hint" onClick={open}><span aria-hidden="true" className="topics-info">i</span> Topics covered</button>
      <span id="topics-hint" role="tooltip" className="topics-tooltip">Explore AI concepts, Azure technologies and implementation status in TelecomIQ.</span>
    </div>
    <dialog ref={dialog} className="topics-dialog" aria-labelledby="topics-title" onClose={()=>trigger.current?.focus()}>
      <div className="topics-heading">
        <div><div className="eyebrow">TELECOMIQ · LEARNING REFERENCE</div>
          <h2 id="topics-title">Topics covered in this project</h2>
          <p className="muted">Explore the concepts and where each is used in the application.</p></div>
        <button type="button" className="secondary topics-close" aria-label="Close project topics" onClick={close}>Close</button>
      </div>
      <div className="topics-counts">{(['Covered','Basic / partial','Next steps'] as Status[]).map(status=>
        <div key={status}><strong>{all.filter(t=>t.status===status).length}</strong><small>{status}</small></div>)}</div>
      <p className="topics-note">Status describes source-code coverage, not a guarantee of production readiness or live end-to-end verification.</p>
      {groups.map(group=><div className="topics-group" key={group.title}>
        <h3>{group.title}</h3>
        {group.topics.map(t=><div className="topic-item" key={t.name}>
          <div><strong>{t.name}</strong><small>{t.detail}</small></div>
          <span className="topic-status" data-state={t.status}>{t.status}</span>
        </div>)}
      </div>)}
      <div className="topics-footer"><a href="https://github.com/githubaccproject/agentic-ai-dotnet-azure#readme" target="_blank" rel="noreferrer">Project architecture & documentation ↗</a><button type="button" onClick={close}>Close</button></div>
    </dialog>
  </div>;
}
