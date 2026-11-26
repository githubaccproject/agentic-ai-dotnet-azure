import { useRef, useState } from 'react';
import type { Decision, DecisionAction, DecisionInput, Evidence, Finding, RevisionCheck } from './types';

const decisionLabels: Record<DecisionAction, string> = {
  RequestChange: 'Request change',
  AcceptRisk: 'Accept risk',
  ApproveException: 'Approve exception'
};

const revisionLabels: Record<RevisionCheck['status'], string> = {
  Resolved: 'AI assessment · Resolved',
  StillPresent: 'AI assessment · Still present',
  NeedsReview: 'Needs reviewer confirmation'
};

function Citation({title, evidence}: {title: string; evidence: Evidence}) {
  return <div className="citation">
    <strong>{title}</strong>
    <small>Page {evidence.page} · {evidence.id}</small>
    <blockquote>{evidence.text}</blockquote>
  </div>;
}

export function FindingCard({finding, decisions, busy, record}: {
  finding: Finding;
  decisions: Decision[];
  busy: boolean;
  record: (id: string, decision: DecisionInput) => Promise<void>;
}) {
  const [action, setAction] = useState<DecisionAction>('RequestChange');
  const [reviewer, setReviewer] = useState('');
  const [reason, setReason] = useState('');
  const [wording, setWording] = useState(finding.suggestedWording ?? '');
  const [open, setOpen] = useState(false);
  const request = useRef<{id: string; body: string} | null>(null);

  async function save() {
    if (!finding.id || !reviewer.trim() || !reason.trim() || busy) return;
    const input: DecisionInput = {
      findingId: finding.id, action, reviewer: reviewer.trim(), reason: reason.trim(),
      proposedWording: action === 'RequestChange' ? wording.trim() || null : null
    };
    const body = JSON.stringify(input);
    if (request.current?.body !== body)
      request.current = {id: crypto.randomUUID(), body};
    try {
      await record(request.current.id, input);
      request.current = null;
      setReason('');
      setOpen(false);
    } catch {
      // The main view displays the API error and the form keeps the entered values.
    }
  }

  return <article>
    <div className="finding-heading"><strong>{finding.category}</strong>
      <span className="pill" data-status={finding.severity}>{finding.severity}</span>
    </div>
    <p>{finding.description}</p>
    {!finding.contractEvidence && <small>Evidence ID {finding.evidenceId}</small>}
    <div className="citations">
      {finding.contractEvidence && <Citation title="Agreement" evidence={finding.contractEvidence}/>}
      {finding.policyEvidence && <Citation title={`Company policy · v${finding.policyEvidence.version}`} evidence={finding.policyEvidence}/>}
    </div>
    {finding.suggestedWording && <div className="citation">
      <strong>Suggested wording for reviewer</strong>
      <blockquote>{finding.suggestedWording}</blockquote>
    </div>}
    {decisions.length > 0 && <div className="decision-log">
      <strong>Decision history</strong>
      <ul>{decisions.map(d => <li key={d.id}>
        <strong>{decisionLabels[d.action]}</strong> · {d.reviewer} · {new Date(d.recordedAtUtc).toLocaleString()}
        <div>{d.reason}</div>
        {d.proposedWording && <blockquote>{d.proposedWording}</blockquote>}
      </li>)}</ul>
    </div>}
    {finding.id && <button type="button" className="secondary" onClick={() => setOpen(!open)}>
      {open ? 'Close decision form' : 'Record decision'}
    </button>}
    {open && <form className="decision-form" onSubmit={e => {e.preventDefault(); void save();}}>
      <label>Decision
        <select disabled={busy} value={action} onChange={e => setAction(e.target.value as DecisionAction)}>
          {Object.entries(decisionLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select>
      </label>
      <label>Reviewer name <input required maxLength={100} disabled={busy} value={reviewer} onChange={e => setReviewer(e.target.value)}/></label>
      <label>Reason <textarea required maxLength={500} disabled={busy} value={reason} onChange={e => setReason(e.target.value)}/></label>
      {action === 'RequestChange' && <label>Proposed wording (optional)
        <textarea maxLength={1500} disabled={busy} value={wording} onChange={e => setWording(e.target.value)}/>
      </label>}
      <button disabled={busy || !reviewer.trim() || !reason.trim()}>Save decision</button>
    </form>}
  </article>;
}

export function RevisionResults({checks}: {checks: RevisionCheck[]}) {
  if (!checks.length) return <p>The previous review has no policy findings to recheck.</p>;

  return <div className="findings">{checks.map(check => <article key={check.previousFindingId}>
    <div className="finding-heading"><strong>{check.category}</strong><span className="pill">{revisionLabels[check.status]}</span></div>
    <p><strong>Previous finding</strong> {check.previousDescription}</p>
    <p>{check.explanation}</p>
    <div className="citations">
      {check.revisedEvidence && <Citation title="Revised agreement" evidence={check.revisedEvidence}/>}
      <Citation title={`Same policy · v${check.policyEvidence.version}`} evidence={check.policyEvidence}/>
    </div>
  </article>)}</div>;
}
