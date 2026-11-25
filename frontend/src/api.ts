import type { Contract, Review } from './types';
async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(path, {...options, signal: options.signal ?? AbortSignal.timeout(60000)});
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new Error(problem?.detail ?? problem?.title ?? 'API request failed (' + response.status + ')');
  }
  return response.json() as Promise<T>;
}
export const api = {
  upload(file: File) { const body = new FormData(); body.append('File', file); return request<Contract>('/api/contracts', {method:'POST', body}); },
  contract(id: string, signal?: AbortSignal) { return request<Contract>('/api/contracts/'+encodeURIComponent(id), {signal}); },
  submit(id: string) { return request<Review>('/api/contracts/'+encodeURIComponent(id)+'/reviews', {method:'POST'}); },
  review(id: string, signal?: AbortSignal) { return request<Review>('/api/reviews/'+encodeURIComponent(id), {signal}); }
};
