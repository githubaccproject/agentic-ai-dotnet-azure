import type { Contract, DecisionInput, PolicyReference, Review } from './types';

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(path, {...options, signal: options.signal ?? AbortSignal.timeout(60000)});
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new Error(problem?.detail ?? problem?.title ?? 'API request failed (' + response.status + ')');
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

function json(method: string, body: object): RequestInit {
  return {method, headers: {'Content-Type': 'application/json'}, body: JSON.stringify(body)};
}

export const api = {
  upload(file: File, previousContractId?: string) {
    const body = new FormData();
    body.append('File', file);
    if (previousContractId) body.append('PreviousContractId', previousContractId);
    return request<Contract>('/api/contracts', {method: 'POST', body});
  },
  contract(id: string, signal?: AbortSignal) {
    return request<Contract>('/api/contracts/' + encodeURIComponent(id), {signal});
  },
  policies(signal?: AbortSignal) {
    return request<PolicyReference[]>('/api/policies', {signal});
  },
  submit(id: string, policies: PolicyReference[], previousReviewId?: string) {
    return request<Review>('/api/contracts/' + encodeURIComponent(id) + '/reviews',
      json('POST', {policies, previousReviewId}));
  },
  review(id: string, signal?: AbortSignal) {
    return request<Review>('/api/reviews/' + encodeURIComponent(id), {signal});
  },
  recordDecision(reviewId: string, decisionId: string, decision: DecisionInput) {
    return request<void>('/api/reviews/' + encodeURIComponent(reviewId) + '/decisions/' + encodeURIComponent(decisionId),
      json('PUT', decision));
  }
};
