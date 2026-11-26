using TelecomIQ.Application.Services;
using TelecomIQ.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var appService = !string.IsNullOrEmpty(builder.Configuration["WEBSITE_SITE_NAME"]);
var hostedDemo = builder.Configuration.GetValue<bool>("HostedDemo:Enabled");
var localDevelopment = builder.Environment.IsDevelopment() && !appService;
var reviewers = new HashSet<Guid>();
if (hostedDemo)
{
    if (!appService || builder.Environment.IsDevelopment())
        throw new InvalidOperationException("The hosted demo requires Azure App Service in Production.");

    foreach (var entry in (builder.Configuration["HostedDemo:ReviewerObjectIds"] ?? "")
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        if (!Guid.TryParse(entry, out var reviewer) || reviewer == Guid.Empty)
            throw new InvalidOperationException("Hosted demo reviewers must be Entra object IDs.");
        reviewers.Add(reviewer);
    }

    if (reviewers.Count == 0)
        throw new InvalidOperationException("Configure at least one hosted demo reviewer.");

    builder.Services.AddHostedService<JobWorker>();
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddTelecomIQ(builder.Configuration);
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IRiskReviewService, RiskReviewService>();

var app = builder.Build();
app.UseExceptionHandler();
app.MapHealthChecks("/health");

if (hostedDemo)
{
    // App Service Authentication supplies these headers for signed-in users.
    app.Use(async (context, next) =>
    {
        if (!string.Equals(context.Request.Headers["X-MS-CLIENT-PRINCIPAL-IDP"], "aad", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(context.Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"], out var reviewer)
            || !reviewers.Contains(reviewer))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    });

    app.UseDefaultFiles();
    app.UseStaticFiles();
}

if (localDevelopment || hostedDemo)
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
