using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Messaging.ServiceBus;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelecomIQ.Application;
using TelecomIQ.Application.Ingestion;
using TelecomIQ.Application.RAG;
using TelecomIQ.Application.Agents;
using TelecomIQ.Application.Orchestration;
using TelecomIQ.Application.Services;
namespace TelecomIQ.Infrastructure;
public static class DependencyInjection {
 public static IServiceCollection AddTelecomIQ(this IServiceCollection services,IConfiguration config) {
  string Get(string key)=>config[key]??throw new InvalidOperationException($"Missing {key}");
  BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
  services.AddSingleton(new DefaultAzureCredential());
  services.AddSingleton<TokenCredential>(sp => sp.GetRequiredService<DefaultAzureCredential>());
  services.AddSingleton(sp=>new BlobContainerClient(new Uri(Get("Azure:BlobContainer")),sp.GetRequiredService<DefaultAzureCredential>()));
  services.AddSingleton(sp=>new ServiceBusClient(Get("Azure:ServiceBusNamespace"),sp.GetRequiredService<DefaultAzureCredential>()));
  services.AddSingleton(sp=>sp.GetRequiredService<ServiceBusClient>().CreateSender(Get("Azure:Queue")));
  services.AddSingleton<IMongoDatabase>(_=>new MongoClient(Get("Mongo:ConnectionString")).GetDatabase(Get("Mongo:Database")));
  services.AddSingleton<IStorage,BlobStorage>();services.AddSingleton<IQueue,Queue>();services.AddSingleton<IRepository,Repository>();
  services.AddSingleton(new HttpClient{Timeout=TimeSpan.FromSeconds(90)});
  services.AddSingleton<AzureRest>();
  services.AddSingleton<IAi>(sp=>sp.GetRequiredService<AzureRest>());
  services.AddSingleton<IPolicyReviewAi>(sp => sp.GetRequiredService<AzureRest>());
  services.AddSingleton<PolicyReviewAgent>();
  services.AddSingleton<ISearch>(sp=>sp.GetRequiredService<AzureRest>());
  services.AddSingleton<IPolicySearch>(sp=>sp.GetRequiredService<AzureRest>());
  services.AddSingleton<IExtractor>(sp=>sp.GetRequiredService<AzureRest>());
  services.AddSingleton<IPolicyStorage>(sp =>
  {
   var container = config["Azure:PolicyBlobContainer"] is string policyUrl
    ? new Uri(policyUrl)
    : new Uri(sp.GetRequiredService<BlobContainerClient>().Uri, "/policies");
   return new PolicyStorage(new BlobContainerClient(container, sp.GetRequiredService<DefaultAzureCredential>()));
  });
  services.AddSingleton<IPolicyRepository, PolicyRepository>();
  services.AddSingleton<PolicyUploadService>();
  services.AddSingleton<PolicyIngestionService>();
  services.AddSingleton<DocumentIngestionService>();services.AddSingleton<RagService>();services.AddSingleton<RiskAgent>();services.AddSingleton<RiskReviewOrchestrator>();
  return services;
 }
}
