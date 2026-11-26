export interface Contract {
  id: string;
  status: 'Uploaded' | 'Processing' | 'Ready' | 'Failed';
  previousContractId?: string | null;
}

export interface PolicyReference { policyId: string; version: number }
export interface Evidence { id: string; page: number; text: string }
export interface PolicyEvidence extends Evidence, PolicyReference {}

export interface Finding {
  id?: string | null;
  category: string;
  severity: string;
  description: string;
  evidenceId: string;
  contractEvidence?: Evidence | null;
  policyEvidence?: PolicyEvidence | null;
  suggestedWording?: string | null;
}

export interface RevisionCheck {
  previousFindingId: string;
  category: string;
  previousDescription: string;
  policyEvidence: PolicyEvidence;
  status: 'Resolved' | 'StillPresent' | 'NeedsReview';
  explanation: string;
  revisedEvidence?: Evidence | null;
}

export type DecisionAction = 'RequestChange' | 'AcceptRisk' | 'ApproveException';

export interface DecisionInput {
  findingId: string;
  action: DecisionAction;
  reviewer: string;
  reason: string;
  proposedWording?: string | null;
}

export interface Decision extends DecisionInput {
  id: string;
  recordedAtUtc: string;
}

export interface Review {
  id: string;
  contractId: string;
  status: 'Queued' | 'Processing' | 'Completed' | 'Failed';
  findings: Finding[];
  failureCode?: string | null;
  policies?: PolicyReference[];
  previousReviewId?: string | null;
  revisionChecks?: RevisionCheck[];
  decisions?: Decision[];
}
