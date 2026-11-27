import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';

type Citation = {id: string; source: string; page: number; text: string; policyId?: string; policyVersion?: number};
type Usage = {promptTokens: number; completionTokens: number; embeddingTokens: number; unreportedCalls: number; estimatedUsd: number | null; durationMs: number; modelCalls: number};
type Turn = {id: string; question: string; answer: string; route: string; citations: Citation[]; tools: {tool: string; status: string; durationMs: number}[]; usage: Usage; grounding: string};
type Session = {id: string; contractId: string; reviewId?: string; turns: Turn[]};
type SessionInfo = {id: string; title: string; turnCount: number; updatedAtUtc: string};
const suggested = ['Summarize this agreement', 'What are the main liability risks?', 'What are the security incident reporting requirements?', 'What risks were accepted previously?', 'Suggest improved contract wording'];

async function json<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(path, {...options, signal: options.signal ?? AbortSignal.timeout(30000)});
  if (!response.ok) throw new Error('Chat request failed (' + response.status + ')');
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export default function ChatPanel({contractId, reviewId}: {contractId?: string; reviewId?: string}) {
  const [sessions, setSessions] = useState<SessionInfo[]>([]);
  const [session, setSession] = useState<Session | null>(null);
  const [question, setQuestion] = useState('');
  const [pending, setPending] = useState('');
  const [progress, setProgress] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const controller = useRef<AbortController | null>(null);
  const bottom = useRef<HTMLDivElement | null>(null);
  const params = new URLSearchParams({contractId: contractId ?? '', ...(reviewId ? {reviewId} : {})});
  const listPath = '/api/chat/sessions?' + params.toString();
  useEffect(() => {
    if (!contractId) return;
    const abort = new AbortController();
    void json<SessionInfo[]>(listPath, {signal: abort.signal}).then(rows => {
      if (!abort.signal.aborted) setSessions(rows);
    }).catch(e => {if (!abort.signal.aborted) setError(String(e));});
    return () => {abort.abort(); controller.current?.abort();};
  }, [listPath, contractId]);
  useEffect(() => {bottom.current?.scrollIntoView({block: 'nearest'});}, [session?.turns.length, pending, progress]);

  async function refresh() {
    setSessions(await json<SessionInfo[]>(listPath));
  }
  async function create() {
    const value = await json<Session>('/api/chat/sessions', {
      method: 'POST', headers: {'Content-Type':'application/json'},
      body: JSON.stringify({contractId, reviewId})
    });
    setSession(value);
    return value;
  }
  async function load(id: string) {
    setBusy(true); setError('');
    try {setSession(id ? await json<Session>('/api/chat/sessions/' + id) : null);}
    catch(e) {setError(String(e));}
    finally {setBusy(false);}
  }
  async function remove() {
    if (!session || !window.confirm('Delete this conversation and its saved answers?')) return;
    setBusy(true); setError('');
    try {await json<void>('/api/chat/sessions/' + session.id, {method:'DELETE'}); setSession(null); await refresh();}
    catch(e) {setError(String(e));}
    finally {setBusy(false);}
  }
  async function send(event?: FormEvent) {
    event?.preventDefault();
    const q = question.trim();
    if (!q || busy || !contractId) return;
    setBusy(true); setError(''); setQuestion(''); setPending(q); setProgress('Starting');
    const abort = new AbortController();
    controller.current = abort;
    const timeout = setTimeout(() => abort.abort(), 210000);
    try {
      const active = session ?? await create();
      const response = await fetch('/api/chat/sessions/' + active.id + '/messages', {
        method:'POST', headers:{'Content-Type':'application/json', Accept:'text/event-stream'},
        body:JSON.stringify({turnId: crypto.randomUUID(), question:q}), signal:abort.signal
      });
      if (!response.ok || !response.body) throw new Error('Chat request failed (' + response.status + '). Try reloading the conversation.');
      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '', finished = false;
      for (;;) {
        const {value, done} = await reader.read();
        buffer += decoder.decode(value, {stream: !done});
        let end: number;
        while ((end = buffer.indexOf('\n\n')) >= 0) {
          const eventText = buffer.slice(0, end);
          buffer = buffer.slice(end + 2);
          const name = eventText.split('\n').find(line => line.startsWith('event: '))?.slice(7);
          const data = eventText.split('\n').filter(line => line.startsWith('data: ')).map(line => line.slice(6)).join('\n');
          if (!data) continue;
          const item = JSON.parse(data);
          if (name === 'progress') setProgress(item.message);
          if (name === 'error') throw new Error(item.message);
          if (name === 'result') {
            const turn = item as Turn;
            setSession({...active, turns:[...active.turns.filter(t=>t.id!==turn.id), turn]});
            finished = true;
          }
        }
        if (done) break;
      }
      if (!finished) throw new Error('Chat ended without a confirmed answer. Reload conversation before retrying.');
      await refresh();
    } catch(e) {
      setError(abort.signal.aborted ? 'Request stopped. Reload the conversation to check if an answer was saved.' : String(e));
      setQuestion(q);
    } finally {
      clearTimeout(timeout); controller.current = null; setPending(''); setProgress(''); setBusy(false);
    }
  }
  const turns = session?.turns ?? [];
  const tokens = turns.reduce((a,t)=>a+t.usage.promptTokens+t.usage.completionTokens+t.usage.embeddingTokens,0);
  const incomplete = turns.some(t=>t.usage.unreportedCalls>0);
  const priced = turns.length>0 && turns.every(t=>t.usage.estimatedUsd!==null);
  const cost = turns.reduce((a,t)=>a+(t.usage.estimatedUsd??0),0);
  return <section id="chat" className="chat-section">
    <h2>04 · TelecomIQ AI Assistant</h2>
    <p className="muted">Ask questions about the loaded agreement, policies, risk findings and decisions. AI answers require human verification.</p>
    {!contractId ? <p>Upload or load an agreement and wait until it is Ready to start chatting.</p> : <>
      <div className="chat-scope"><strong>{reviewId ? 'Agreement + completed review' : 'Agreement only'}</strong>
        <small>{contractId}{reviewId ? ' · Review ' + reviewId : ''}</small>
        <small>{reviewId ? 'Selected policy versions and scoped MCP tools are available.' : 'Complete a policy review to enable policy evidence and MCP tools.'}</small></div>
      <div className="actions"><select aria-label="Conversation history" value={session?.id ?? ''} disabled={busy}
        onChange={e=>void load(e.target.value)}>
        <option value="">New conversation</option>
        {sessions.map(s=><option key={s.id} value={s.id}>{s.title.slice(0,65)} · {s.turnCount} turns</option>)}
        {session && !sessions.some(s=>s.id===session.id) && <option value={session.id}>Current conversation</option>}
      </select><button className="secondary" disabled={busy} onClick={()=>{setSession(null);setQuestion('');}}>New chat</button>
        {session && <button className="secondary" disabled={busy} onClick={()=>void remove()}>Delete chat</button>}
      </div>
      <div className="chat-metrics"><div><small>Conversation turns</small><strong>{turns.length}/30</strong></div>
        <div><small>Reported tokens{incomplete?' (partial)':''}</small><strong>{tokens.toLocaleString()}</strong></div>
        <div><small>Estimated cost (USD)</small><strong>{priced?'$'+cost.toFixed(5):'Not available'}</strong></div>
        <div><small>MCP tool calls</small><strong>{turns.reduce((a,t)=>a+t.tools.length,0)}</strong></div></div>
      <div className="chat-thread" aria-live="polite">
        {!turns.length && <p className="muted">Ask a question or choose a suggestion below. Chat history is saved for this agreement and review scope.</p>}
        {turns.map(t=><div className="chat-turn" key={t.id}>
          <div className="chat-user">{t.question}</div>
          <div className="chat-answer"><small>{t.route} · {t.grounding==='ModelChecked'?'Citation and model checks passed':t.grounding==='CitationIdsValidated'?'Citation IDs checked':'Insufficient evidence'}</small>
            <p>{t.answer}</p>
            {t.citations.length>0 && <details><summary>Sources ({t.citations.length})</summary>{t.citations.map(c=><div className="chat-source" key={c.id}>
              <strong>{c.source}{c.page>0?' · Page '+c.page:''}</strong>
              {c.policyId && <small>{c.policyId} · v{c.policyVersion}</small>}
              <p>{c.text}</p></div>)}</details>}
            <details><summary>Agent activity and usage</summary>
              <small>{t.usage.durationMs} ms · {t.usage.modelCalls} chat calls · {t.usage.promptTokens} input · {t.usage.completionTokens} output · {t.usage.embeddingTokens} embedding tokens</small>
              {t.usage.unreportedCalls>0 && <small>Some usage is unreported, including MCP embedding calls when used.</small>}
              {t.tools.map((tool,i)=><small key={i}>{tool.tool} · {tool.status} · {tool.durationMs} ms</small>)}
            </details>
          </div>
        </div>)}
        {pending && <><div className="chat-user">{pending}</div><div className="chat-answer" role="status">{progress}…</div></>}
        <div ref={bottom}/>
      </div>
      {error && <p role="alert" className="error">{error}</p>}
      <div className="actions">{suggested.map(q=><button key={q} type="button" className="secondary chat-suggestion" disabled={busy} onClick={()=>setQuestion(q)}>{q}</button>)}</div>
      <form className="chat-form" onSubmit={send}><label htmlFor="chat-question">Your question</label>
        <textarea id="chat-question" value={question} maxLength={1500} rows={3} disabled={busy} onChange={e=>setQuestion(e.target.value)} placeholder="Ask about a clause, policy requirement or risk…"/>
        <div className="actions"><button disabled={busy || !question.trim() || turns.length>=30}>Send question</button>
          {busy && <button type="button" className="secondary" onClick={()=>controller.current?.abort()}>Stop</button>}</div>
        <small>{question.length}/1500 · Validated answers are sent after retrieval and tool execution.</small>
      </form>
    </>}
  </section>;
}
