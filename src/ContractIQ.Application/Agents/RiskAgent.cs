using ContractIQ.Domain;
using ContractIQ.Application.RAG;
namespace ContractIQ.Application.Agents;
public sealed class RiskAgent(RagService rag, IAi ai) {
 public async Task<Finding[]> Run(Guid id,string category,string query,CancellationToken ct) {
  var evidence=await rag.Retrieve(id,query,ct);
  if(evidence.Length==0) return [];
  var findings=await ai.Analyze(category,evidence,ct);
  var ids=evidence.Select(x=>x.Id).ToHashSet();
  return findings.Where(x=>ids.Contains(x.EvidenceId) && new[]{"Low","Medium","High","Critical"}.Contains(x.Severity)).Select(x=>x with {Category=category}).ToArray();
 }
}
