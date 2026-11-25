using ContractIQ.Domain;
using ContractIQ.Application.Agents;
namespace ContractIQ.Application.Orchestration;
public sealed class RiskReviewOrchestrator(IRepository repository,RiskAgent agent) {
 public async Task Run(Guid contractId,Guid reviewId,CancellationToken ct) {
  var review=await repository.GetReview(reviewId,ct) ?? throw new InvalidOperationException("Unknown review");
  if(review.ContractId!=contractId) throw new InvalidOperationException("Contract mismatch");
  if(review.Status=="Completed") return;
  var doc=await repository.Get(contractId,ct);
  if(doc?.Status!=ProcessingStatus.Ready) throw new InvalidOperationException("Contract not ready");
  await repository.SaveReview(review with {Status="Processing"},ct);
  try {
   var results=await Task.WhenAll(
    agent.Run(contractId,"Legal","indemnity liability termination dispute resolution",ct),
    agent.Run(contractId,"Financial","payment penalties retainage price changes",ct),
    agent.Run(contractId,"Compliance","insurance safety regulations obligations",ct));
   await repository.SaveReview(review with {Status="Completed",Findings=results.SelectMany(x=>x).ToArray()},ct);
  } catch {if(!ct.IsCancellationRequested) await repository.SaveReview(review with {Status="Failed"},ct);throw;}
 }
}
