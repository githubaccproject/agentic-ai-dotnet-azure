import { useEffect, useState } from 'react';
import { api } from './api';
import type { Contract, Review } from './types';
const categories = ['All','Legal','Financial','Compliance'];
const message = (e: unknown) => e instanceof Error ? e.message : 'Unexpected error';
export default function App() {
 const [file,setFile]=useState<File|null>(null);
 const [contract,setContract]=useState<Contract|null>(null);
 const [review,setReview]=useState<Review|null>(null);
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState('');
 const [filter,setFilter]=useState('All');
 const [pollVersion,setPollVersion]=useState(0);
 const [lookup,setLookup]=useState('');
 const [lookupKind,setLookupKind]=useState('contract');
 const contractId=contract?.id, contractStatus=contract?.status;
 const reviewId=review?.id, reviewStatus=review?.status;
 useEffect(()=>{
  if(!contractId || !['Uploaded','Processing'].includes(contractStatus ?? '')) return;
  const controller=new AbortController(); let timer: ReturnType<typeof setTimeout>;
  async function poll(){try{const next=await api.contract(contractId!,controller.signal);if(!controller.signal.aborted){setContract(next);if(['Uploaded','Processing'].includes(next.status))timer=setTimeout(poll,2500);}}catch(e){if(!controller.signal.aborted)setError(message(e));}}
  timer=setTimeout(poll,1500);return()=>{controller.abort();clearTimeout(timer);};
 },[contractId,contractStatus,pollVersion]);
 useEffect(()=>{
  if(!reviewId || !['Queued','Processing'].includes(reviewStatus ?? ''))return;
  const controller=new AbortController();let timer:ReturnType<typeof setTimeout>;
  async function poll(){try{const next=await api.review(reviewId!,controller.signal);if(!controller.signal.aborted){setReview(next);if(['Queued','Processing'].includes(next.status))timer=setTimeout(poll,2500);}}catch(e){if(!controller.signal.aborted)setError(message(e));}}
  timer=setTimeout(poll,1500);return()=>{controller.abort();clearTimeout(timer);};
 },[reviewId,reviewStatus,pollVersion]);
 async function upload(){if(!file||busy)return;setError('');if(!file.name.toLowerCase().endsWith('.pdf')||file.size<5||file.size>20*1024*1024){setError('Choose a PDF up to 20 MB.');return;}setBusy(true);try{const result=await api.upload(file);setContract(result);setReview(null);setFilter('All');}catch(e){setError(message(e));}finally{setBusy(false);}}
 async function submit(){if(!contract||busy||contract.status!=='Ready')return;setBusy(true);setError('');try{setReview(await api.submit(contract.id));setFilter('All');}catch(e){setError(message(e));}finally{setBusy(false);}}
 async function resume(){if(busy)return;setBusy(true);setError('');try{if(lookupKind==='review'){const result=await api.review(lookup.trim());const doc=await api.contract(result.contractId);setContract(doc);setReview(result);}else{setContract(await api.contract(lookup.trim()));setReview(null);}setPollVersion(v=>v+1);}catch(e){setError(message(e));}finally{setBusy(false);}}
 const findings=review?.findings??[];
 const visible=findings.filter(f=>filter==='All'||f.category.toLowerCase()===filter.toLowerCase());
 const reviewing=reviewStatus==='Queued'||reviewStatus==='Processing';
 return <div className="shell"><aside><div className="brand"><span className="logo">CQ</span> ContractIQ</div><p className="muted">AI contract intelligence</p><nav><a href="#upload">01 · Documents</a><a href="#review">02 · Risk review</a><a href="#findings">03 · Findings</a></nav><div className="side-note">Development workspace<br/>.NET 10 · Azure AI</div></aside><main><header><div><div className="eyebrow">CONTRACT INTELLIGENCE / WORKSPACE</div><h1>Contract risk review</h1><p className="muted">Upload a contract, extract its content and review potential risks.</p></div><span className="pill">Development</span></header>
 {error&&<div role="alert" className="error">{error} <button onClick={()=>{setError('');setPollVersion(v=>v+1);}}>Dismiss / retry polling</button></div>}
 <section id="upload"><h2>01 · Upload contract</h2><div className="upload"><label htmlFor="pdf">Select a PDF contract</label><input id="pdf" type="file" accept=".pdf,application/pdf" disabled={busy||reviewing} onChange={e=>setFile(e.target.files?.[0]??null)}/><small>PDF only · Maximum 20 MB</small><button disabled={!file||busy||reviewing} onClick={upload}>{busy?'Working…':'Upload document'}</button></div>
 {contract&&<div className="status"><div><strong>Document status</strong><small>{contract.id}</small></div><span className="pill" data-status={contract.status}>{contract.status}</span></div>}
 {contractStatus==='Failed'&&<p role="alert">Ingestion failed. Check the Worker logs before uploading again.</p>}</section>
 <section id="review"><h2>02 · AI risk review</h2><div className="agents">{categories.slice(1).map(c=><div key={c}><strong>{c}</strong><small>{c==='Legal'?'Liability and termination':c==='Financial'?'Payments and penalties':'Insurance and obligations'}</small></div>)}</div><button disabled={busy||contractStatus!=='Ready'||reviewing} onClick={submit}>Start risk review</button>{review&&<div className="status"><div><strong>Review status</strong><small>{review.id}</small></div><span className="pill" data-status={review.status}>{review.status}</span></div>}{reviewStatus==='Failed'&&<p role="alert">Review failed. Check Worker logs before starting another review.</p>}</section>
 <section id="findings"><h2>03 · Risk findings</h2><div className="metrics">{['High','Medium','Low'].map(s=><div key={s}><small>{s} severity</small><strong>{reviewStatus==='Completed'?findings.filter(f=>f.severity.toLowerCase()===s.toLowerCase()).length:'—'}</strong></div>)}</div><div className="filters">{categories.map(c=><button key={c} className={filter===c?'selected':'secondary'} aria-pressed={filter===c} onClick={()=>setFilter(c)}>{c}</button>)}</div>
 {reviewStatus!=='Completed'?<p className="muted">Findings will appear when the review completes.</p>:visible.length===0?<p>No findings in this category. This does not guarantee the contract is risk-free.</p>:<div className="findings">{visible.map((f,i)=><article key={f.evidenceId+'-'+i}><div><strong>{f.category}</strong><span className="pill" data-status={f.severity}>{f.severity}</span></div><p>{f.description}</p><small>Evidence ID: {f.evidenceId}</small></article>)}</div>}
 <p className="muted">AI-generated findings require human review. Evidence IDs are references, not verified quotations.</p></section>
 <section><h2>Resume existing work</h2><div className="resume"><select aria-label="ID type" value={lookupKind} onChange={e=>setLookupKind(e.target.value)}><option value="contract">Contract ID</option><option value="review">Review ID</option></select><input aria-label="Existing ID" placeholder="Paste GUID" value={lookup} onChange={e=>setLookup(e.target.value)}/><button disabled={busy||!lookup.trim()} onClick={resume}>Load</button></div></section>
 </main></div>;
}
