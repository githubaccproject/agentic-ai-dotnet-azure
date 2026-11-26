using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TelecomIQ.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddTelecomIQ(builder.Configuration);
builder.Services.AddHostedService<JobWorker>();
await builder.Build().RunAsync();
