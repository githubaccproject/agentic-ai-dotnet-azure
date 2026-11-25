export interface Contract { id: string; status: 'Uploaded'|'Processing'|'Ready'|'Failed' }
export interface Finding { category: string; severity: string; description: string; evidenceId: string }
export interface Review { id: string; contractId: string; status: 'Queued'|'Processing'|'Completed'|'Failed'; findings: Finding[] }
