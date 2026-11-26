import { useEffect, useState } from 'react';
import { api } from './api';
import { FindingCard, RevisionResults } from './ReviewFindings';
import type { Contract, DecisionInput, PolicyReference, Review } from './types';

const categories = ['All', 'Vendor Legal', 'Commercial', 'Telecom Security & Compliance'];
const legacyCategories: Record<string, string> = {Legal: 'Vendor Legal', Financial: 'Commercial', Compliance: 'Telecom Security & Compliance'};
const message = (error: unknown) => error instanceof Error ? error.message : 'Unexpected error';
const contractPending = (status?: string) => status === 'Uploaded' || status === 'Processing';
const reviewPending = (status?: string) => status === 'Queued' || status === 'Processing';

export default function App() {
  const [file, setFile] = useState<File | null>(null);
  const [contract, setContract] = useState<Contract | null>(null);
  const [review, setReview] = useState<Review | null>(null);
  const [policies, setPolicies] = useState<PolicyReference[] | null>(null);
  const [selected, setSelected] = useState<PolicyReference[]>([]);
  const [previousReviewId, setPreviousReviewId] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [filter, setFilter] = useState('All');
  const [pollVersion, setPollVersion] = useState(0);
  const [lookup, setLookup] = useState('');
  const [lookupKind, setLookupKind] = useState('contract');

  const contractId = contract?.id;
  const contractStatus = contract?.status;
  const reviewId = review?.id;
  const reviewStatus = review?.status;
  const reviewing = reviewPending(reviewStatus);
  const canSubmit = !busy && !reviewing && contractStatus === 'Ready';
  const canUploadRevision = reviewStatus === 'Completed' && (review?.policies?.length ?? 0) > 0;

  useEffect(() => {
    if (!contractId || !contractPending(contractStatus)) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    async function poll(id: string) {
      try {
        const next = await api.contract(id, controller.signal);
        if (controller.signal.aborted) return;
        setContract(next);
        if (contractPending(next.status)) timer = setTimeout(poll, 2500, id);
      } catch (e) {
        if (!controller.signal.aborted) setError(message(e));
      }
    }
    timer = setTimeout(poll, 1500, contractId);
    return () => {controller.abort(); clearTimeout(timer);};
  }, [contractId, contractStatus, pollVersion]);

  useEffect(() => {
    if (!reviewId || !reviewPending(reviewStatus)) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    async function poll(id: string) {
      try {
        const next = await api.review(id, controller.signal);
        if (controller.signal.aborted) return;
        setReview(next);
        if (reviewPending(next.status)) timer = setTimeout(poll, 2500, id);
      } catch (e) {
        if (!controller.signal.aborted) setError(message(e));
      }
    }
    timer = setTimeout(poll, 1500, reviewId);
    return () => {controller.abort(); clearTimeout(timer);};
  }, [reviewId, reviewStatus, pollVersion]);

  async function loadPolicies() {
    await perform(async () => {
      const ready = await api.policies();
      setPolicies(ready);
      setSelected(current => current.filter(policy => ready.some(item =>
        item.policyId === policy.policyId && item.version === policy.version)));
    });
  }

  async function perform(run: () => Promise<void>) {
    setBusy(true);
    setError('');
    try {
      await run();
    } catch (e) {
      setError(message(e));
    } finally {
      setBusy(false);
    }
  }

  async function upload(asRevision = false) {
    if (!file || busy || reviewing) return;
    if (!file.name.toLowerCase().endsWith('.pdf') || file.size < 5 || file.size > 20 * 1024 * 1024) {
      setError('Choose a PDF up to 20 MB.');
      return;
    }

    const previous = asRevision && canUploadRevision ? review : null;
    await perform(async () => {
      setContract(await api.upload(file, previous?.contractId));
      setFile(null);
      setPreviousReviewId(previous?.id ?? '');
      setReview(null);
      setFilter('All');
    });
  }

  async function submit(mode: 'contract' | 'comparison' | 'revision') {
    if (!canSubmit || !contractId) return;
    await perform(async () => {
      setReview(await api.submit(contractId, mode === 'comparison' ? selected : [],
        mode === 'revision' ? previousReviewId.trim() : undefined));
      setFilter('All');
    });
  }

  async function record(decisionId: string, decision: DecisionInput) {
    if (!reviewId || busy) throw new Error('Please wait for the current action to finish.');
    let saved = false;
    await perform(async () => {
      await api.recordDecision(reviewId, decisionId, decision);
      setReview(await api.review(reviewId));
      saved = true;
    });
    if (!saved) throw new Error('Decision could not be confirmed.');
  }

  async function resume() {
    await perform(async () => {
      if (lookupKind === 'review') {
        const result = await api.review(lookup.trim());
        setContract(await api.contract(result.contractId));
        setReview(result);
        setPreviousReviewId(result.previousReviewId ?? '');
      } else {
        setContract(await api.contract(lookup.trim()));
        setReview(null);
        setPreviousReviewId('');
      }
      setFilter('All');
      setPollVersion(value => value + 1);
    });
  }

  const findings = review?.findings ?? [];
  const visible = findings.filter(f => filter === 'All' || (legacyCategories[f.category] ?? f.category) === filter);
  return <div className="shell">
    <aside>
      <div className="brand"><span className="logo">TQ</span> TelecomIQ</div>
      <p className="muted">Telecom vendor risk intelligence</p>
      <nav><a href="#upload">01 · Documents</a><a href="#review">02 · Risk review</a><a href="#findings">03 · Findings</a></nav>
      <div className="side-note">Development workspace<br/>.NET 10 · Azure AI</div>
    </aside>
    <main>
      <header>
        <div>
          <div className="eyebrow">VENDOR INTELLIGENCE / WORKSPACE</div>
          <h1>Vendor risk assessment</h1>
          <p className="muted">Upload an agreement, compare it with company policies, and record reviewer decisions.</p>
        </div>
        <span className="pill">Development</span>
      </header>
      {error && <div role="alert" className="error">{error}
        <button onClick={() => {setError(''); setPollVersion(value => value + 1);}}>Dismiss / retry polling</button>
      </div>}
      <section id="upload">
        <h2>01 · Upload vendor agreement</h2>
        <div className="upload">
          <label htmlFor="pdf">Select a vendor agreement PDF</label>
          <input id="pdf" key={contractId} type="file" accept=".pdf,application/pdf" disabled={busy || reviewing}
            onChange={e => setFile(e.target.files?.[0] ?? null)}/>
          <small>PDF only · Maximum 20 MB</small>
          <div className="actions">
            <button disabled={!file || busy || reviewing} onClick={() => void upload()}>Upload as new agreement</button>
            {canUploadRevision && <button className="secondary" disabled={!file || busy || reviewing}
              onClick={() => void upload(true)}>Upload as revision of this agreement</button>}
          </div>
        </div>
        {contract && <div className="status">
          <div><strong>Document status</strong><small>{contract.id}</small>
            {contract.previousContractId && <small>Previous agreement {contract.previousContractId}</small>}
          </div>
          <span className="pill" data-status={contract.status}>{contract.status}</span>
        </div>}
        {contractStatus === 'Failed' && <p role="alert">Ingestion failed. Check the Worker logs before uploading again.</p>}
      </section>
      <section id="review">
        <h2>02 · AI risk review</h2>
        <div className="agents">{categories.slice(1).map(category => <div key={category}><strong>{category}</strong></div>)}</div>
        {contract?.previousContractId && <div className="revision-options">
          <label htmlFor="prior-review">Previous policy review ID</label>
          <input id="prior-review" value={previousReviewId} disabled={busy || reviewing}
            onChange={e => setPreviousReviewId(e.target.value)} placeholder="Paste the completed review ID"/>
          <button disabled={!canSubmit || !previousReviewId.trim()} onClick={() => void submit('revision')}>Re-review revision</button>
          <small>The revision uses the same policy versions as the previous review.</small>
        </div>}
        <div className="actions">
          <button className="secondary" disabled={!canSubmit} onClick={() => void submit('contract')}>Review agreement only</button>
          <button className="secondary" disabled={busy || reviewing || !contractId} onClick={() => void loadPolicies()}>
            {policies ? 'Refresh ready policies' : 'Choose policies for comparison'}
          </button>
        </div>
        {policies && <fieldset className="policy-options" disabled={busy || reviewing}>
          <legend>Policy versions</legend>
          {policies.length === 0 && <p>No policy versions are ready.</p>}
          {policies.length > 0 && <>
            <small>Select one version per policy, up to 12 policies.</small>
            {policies.map(policy => {
              const current = selected.find(item => item.policyId === policy.policyId);
              const checked = current?.version === policy.version;
              return <label key={policy.policyId + ':' + policy.version}>
                <input type="checkbox" checked={checked}
                  disabled={!current && selected.length >= 12}
                  onChange={() => setSelected(items => [
                    ...items.filter(item => item.policyId !== policy.policyId), ...(checked ? [] : [policy])
                  ])}/>
                {policy.policyId} · v{policy.version}
              </label>;
            })}
            <button disabled={!canSubmit || !selected.length} onClick={() => void submit('comparison')}>Compare selected policies</button>
          </>}
        </fieldset>}
        {review && <div className="status">
          <div><strong>Review status</strong><small>{review.id}</small>
            {(review.policies ?? []).map(p => <small key={p.policyId}>{p.policyId} · v{p.version}</small>)}
          </div>
          <span className="pill" data-status={review.status}>{review.status}</span>
        </div>}
        {reviewStatus === 'Failed' && <p role="alert">{review?.failureCode === 'Throttled'
          ? 'Azure is temporarily limiting requests. Wait a minute, then start the review again.'
          : 'Review failed. Check the Worker logs before starting another review.'}</p>}
      </section>
      <section id="findings">
        <h2>03 · Risk findings</h2>
        <div className="metrics">{['Critical', 'High', 'Medium', 'Low'].map(severity => <div key={severity}>
          <small>{severity} severity</small>
          <strong>{reviewStatus === 'Completed' ? findings.filter(f => f.severity.toLowerCase() === severity.toLowerCase()).length : '—'}</strong>
        </div>)}</div>
        <div className="filters">{categories.map(category => <button key={category}
          className={filter === category ? 'selected' : 'secondary'} aria-pressed={filter === category}
          onClick={() => setFilter(category)}>{category}</button>)}</div>
        {reviewStatus !== 'Completed' ? <p className="muted">Findings will appear when the review completes.</p>
          : visible.length === 0 ? <p>No supported findings in this category.</p>
          : <div className="findings">{visible.map((finding, index) =>
            <FindingCard key={reviewId + ':' + (finding.id ?? finding.evidenceId + ':' + index)} finding={finding}
              decisions={(review?.decisions ?? []).filter(d => d.findingId === finding.id)} busy={busy} record={record}/>
          )}</div>}
        {review?.previousReviewId && reviewStatus === 'Completed' && <>
          <h3>Previous findings rechecked</h3><small>Previous review {review.previousReviewId}</small>
          <RevisionResults checks={review.revisionChecks ?? []}/>
        </>}
        {reviewStatus === 'Completed' && <div className="finding">
          <h3>Agent activity · MCP tools</h3>
          {(review?.toolActivity ?? []).length === 0
            ? <p className="muted">No tool calls recorded. Tools are available for policy comparisons; the model may choose not to call them.</p>
            : <ul>{review?.toolActivity?.map((item, index) => <li key={index}>
              <strong>{item.tool}</strong> · {item.category} · {item.status} · {item.durationMs} ms
            </li>)}</ul>}
        </div>}
        <p className="muted">Review AI findings and suggested wording before making a decision.</p>
      </section>
      <section>
        <h2>Resume existing work</h2>
        <div className="resume">
          <select aria-label="ID type" value={lookupKind} onChange={e => setLookupKind(e.target.value)}>
            <option value="contract">Agreement ID</option><option value="review">Review ID</option>
          </select>
          <input aria-label="Existing ID" placeholder="Paste GUID" value={lookup} onChange={e => setLookup(e.target.value)}/>
          <button disabled={busy || !lookup.trim()} onClick={() => void resume()}>Load</button>
        </div>
      </section>
    </main>
  </div>;
}
