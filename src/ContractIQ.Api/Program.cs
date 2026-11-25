using ContractIQ.Application.Services;
using ContractIQ.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddContractIQ(builder.Configuration);
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IRiskReviewService, RiskReviewService>();

var app = builder.Build();
app.UseExceptionHandler();
app.MapHealthChecks("/health");
// Keep business endpoints disabled outside Development until authentication is implemented.
if (app.Environment.IsDevelopment())
    app.MapControllers();

app.Run();
