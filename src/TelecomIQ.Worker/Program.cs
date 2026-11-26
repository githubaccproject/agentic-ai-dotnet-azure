using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TelecomIQ.Infrastructure;

if (args is ["--mcp-review-tools", var scopeId])
{
    await ReviewToolServer.Run(scopeId);
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddTelecomIQ(builder.Configuration);
builder.Services.AddHostedService<JobWorker>();
await builder.Build().RunAsync();
