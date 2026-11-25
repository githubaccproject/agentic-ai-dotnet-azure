using TelecomIQ.Domain;
using TelecomIQ.Application.Agents;
namespace TelecomIQ.Application.Orchestration;
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
    agent.Run(contractId,"Vendor Legal","vendor indemnification liability caps termination exit assistance intellectual property subcontracting dispute resolution",ct),
    agent.Run(contractId,"Commercial","vendor pricing annual escalation payment terms integration fees service credits penalties renewal charges",ct),
    agent.Run(contractId,"Telecom Security & Compliance","5G network availability SLA outage disaster recovery security incident notification customer data privacy retention cross-border transfers audit access encryption",ct));
   await repository.SaveReview(review with {Status="Completed",Findings=results.SelectMany(x=>x).ToArray()},ct);
  } catch {if(!ct.IsCancellationRequested) await repository.SaveReview(review with {Status="Failed"},ct);throw;}
 }
}
