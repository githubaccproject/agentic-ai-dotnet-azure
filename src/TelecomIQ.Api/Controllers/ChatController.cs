using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TelecomIQ.Application;
using TelecomIQ.Domain;
using TelecomIQ.Infrastructure;

namespace TelecomIQ.Api.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatController(ChatStore store, IRepository repository, AzureRest ai, ILogger<ChatController> logger) : ControllerBase
{
    // Keep the small demo host from running an unbounded number of LLM/MCP processes.
    private static readonly SemaphoreSlim Slots = new(2);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public sealed record CreateChat(Guid ContractId, Guid? ReviewId);
    public sealed record AskChat(Guid TurnId, [Required, StringLength(ChatSafety.MaxQuestion)] string Question);
    public sealed record SessionInfo(Guid Id, Guid ContractId, Guid? ReviewId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, int TurnCount, string Title);
    public sealed record SessionView(Guid Id, Guid ContractId, Guid? ReviewId, ChatTurn[] Turns);
    private static SessionView View(ChatSession s) => new(s.Id, s.ContractId, s.ReviewId, s.Turns);
    private string Owner => !string.IsNullOrEmpty(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["WEBSITE_SITE_NAME"]) && Guid.TryParse(HttpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"], out var id)
        ? id.ToString("D") : "local-development";

    private async Task<bool> Ready(Guid contractId, Guid? reviewId, CancellationToken ct)
    {
        var contract = await repository.Get(contractId, ct);
        if (contract?.Status != ProcessingStatus.Ready) return false;
        if (reviewId is not Guid id) return true;
        var review = await repository.GetReview(id, ct);
        return review?.Status == "Completed" && review.ContractId == contractId;
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<SessionView>> Create(CreateChat request, CancellationToken ct)
    {
        if (!await Ready(request.ContractId, request.ReviewId, ct))
            return Problem(statusCode: 409, detail: "Select a ready agreement and, optionally, its completed review.");
        var session = new ChatSession(Guid.NewGuid(), Owner, request.ContractId, request.ReviewId, DateTime.UtcNow);
        await store.Create(session, ct);
        return Ok(View(session));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<SessionInfo[]>> List([FromQuery] Guid contractId, [FromQuery] Guid? reviewId, CancellationToken ct)
    {
        var rows = await store.List(Owner, contractId, reviewId, ct);
        return Ok(rows.Select(s => new SessionInfo(s.Id, s.ContractId, s.ReviewId, s.CreatedAtUtc, s.UpdatedAtUtc,
            s.Turns.Length, s.Turns.FirstOrDefault()?.Question ?? "New conversation")).ToArray());
    }

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<SessionView>> Get(Guid id, CancellationToken ct)
    {
        var session = await store.Get(id, Owner, ct);
        return session is null ? NotFound() : Ok(View(session));
    }

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await store.Delete(id, Owner, ct) ? NoContent() : NotFound();

    [HttpPost("sessions/{id:guid}/messages")]
    public async Task Ask(Guid id, AskChat request, CancellationToken ct)
    {
        var session = await store.Get(id, Owner, ct);
        if (session is null) { Response.StatusCode = 404; return; }
        if (request.TurnId == Guid.Empty || !ChatSafety.ValidQuestion(request.Question))
        { Response.StatusCode = 400; return; }
        var question = request.Question.Trim();
        var existing = session.Turns.FirstOrDefault(t => t.Id == request.TurnId);
        if (existing is not null && existing.Question != question)
        { Response.StatusCode = 409; return; }
        if (existing is null && session.Turns.Length >= ChatSafety.MaxTurns)
        { Response.StatusCode = 409; return; }
        if (!await Ready(session.ContractId, session.ReviewId, ct))
        { Response.StatusCode = 409; return; }
        if (!await Slots.WaitAsync(0, ct))
        { Response.StatusCode = 429; return; }
        try
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache, no-transform";
            Response.Headers["X-Accel-Buffering"] = "no";
            await Response.StartAsync(ct);
            async Task Emit(string name, object data)
            {
                await Response.WriteAsync("event: " + name + "\ndata: " + JsonSerializer.Serialize(data, JsonOptions) + "\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            if (existing is not null) { await Emit("result", existing); return; }
            await Emit("progress", new { message = "Starting secure contract chat" });
            Review? review = session.ReviewId is Guid rid ? await repository.GetReview(rid, ct) : null;
            if (session.ReviewId is not null && (review?.Status != "Completed" || review.ContractId != session.ContractId))
            { await Emit("error", new { message = "Review scope is no longer available." }); return; }
            var result = await ai.AnswerChat(session, review, request.TurnId, question,
                status => Emit("progress", new { message = status }), ct);
            if (!await store.Append(session, result.Turn, result.Summary, ct))
            { await Emit("error", new { message = "Conversation changed. Reload it before retrying." }); return; }
            await Emit("result", result.Turn);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            // Never log model prompts, chat history, document text or credentials.
            logger.LogWarning("Chat failed {SessionId} {ErrorType}", id, ex.GetType().Name);
            if (!ct.IsCancellationRequested)
                await Response.WriteAsync("event: error\ndata: {\"message\":\"Chat could not complete. Please retry or start a new conversation.\"}\n\n", ct);
        }
        finally { Slots.Release(); }
    }
}
