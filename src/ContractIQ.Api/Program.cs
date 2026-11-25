using ContractIQ.Application.Services;
using ContractIQ.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddContractIQ(builder.Configuration);
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IRiskReviewService, RiskReviewService>();

var app = builder.Build();
app.UseExceptionHandler();
app.MapHealthChecks("/health");

// Expose API documentation and business endpoints only in Development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "TelecomIQ API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "TelecomIQ API";
    });
    app.MapControllers();
}

app.Run();
