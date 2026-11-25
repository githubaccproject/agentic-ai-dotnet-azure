using Azure.Messaging.ServiceBus;
using TelecomIQ.Domain;
using TelecomIQ.Infrastructure;
using TelecomIQ.Application.Ingestion;
using TelecomIQ.Application.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
var builder=Host.CreateApplicationBuilder(args);
builder.Services.AddTelecomIQ(builder.Configuration);
builder.Services.AddHostedService<JobWorker>();
await builder.Build().RunAsync();
sealed class JobWorker(ServiceBusClient client,IConfiguration config,DocumentIngestionService ingestion,RiskReviewOrchestrator review,ILogger<JobWorker> logger):BackgroundService {
 protected override async Task ExecuteAsync(CancellationToken ct){
  await using var processor=client.CreateProcessor(config["Azure:Queue"]!,new ServiceBusProcessorOptions{AutoCompleteMessages=false,MaxConcurrentCalls=2,MaxAutoLockRenewalDuration=TimeSpan.FromMinutes(20)});
  processor.ProcessMessageAsync+=async args=>{
   var job=JsonSerializer.Deserialize<Job>(args.Message.Body.ToString())??throw new InvalidOperationException("Invalid job");
   if(job.Kind=="ingest")await ingestion.Run(job.ContractId,args.CancellationToken);
   else if(job.Kind=="review" && job.ReviewId is Guid id)await review.Run(job.ContractId,id,args.CancellationToken);
   else {await args.DeadLetterMessageAsync(args.Message,"Invalid job");return;}
   await args.CompleteMessageAsync(args.Message,args.CancellationToken);
  };
  processor.ProcessErrorAsync+=args=>{logger.LogError(args.Exception,"Job processing failed");return Task.CompletedTask;};
  await processor.StartProcessingAsync(ct);
  try{await Task.Delay(Timeout.Infinite,ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){}
  await processor.StopProcessingAsync();
 }
}
