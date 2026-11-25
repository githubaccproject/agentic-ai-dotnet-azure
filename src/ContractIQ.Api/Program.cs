using ContractIQ.Application;
using ContractIQ.Domain;
using ContractIQ.Infrastructure;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddContractIQ(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
var app=builder.Build();
app.UseExceptionHandler();
app.MapHealthChecks("/health");
// Development-only API until tenant authentication and authorization are implemented.
if(app.Environment.IsDevelopment()){
 app.MapPost("/api/contracts",async (IFormFile file,IStorage storage,IRepository repo,IQueue queue,CancellationToken ct)=>{
  if(file.Length<5 || file.Length>20*1024*1024 || !file.FileName.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase))return Results.BadRequest("PDF up to 20 MB required");
  await using var stream=file.OpenReadStream();
  var header=new byte[5];await stream.ReadExactlyAsync(header,ct);
  if(System.Text.Encoding.ASCII.GetString(header)!="%PDF-")return Results.BadRequest("Invalid PDF header");
  stream.Position=0;
  var id=Guid.NewGuid();var path=$"{id:N}/contract.pdf";
  await storage.Upload(path,stream,ct);
  await repo.Save(new Contract(id,path,ProcessingStatus.Uploaded),ct);
  await queue.Send(new Job("ingest",id),ct);
  return Results.Accepted($"/api/contracts/{id}",new{id});
 }).DisableAntiforgery();
 app.MapGet("/api/contracts/{id:guid}",async(Guid id,IRepository repo,CancellationToken ct)=>await repo.Get(id,ct) is {} doc?Results.Ok(doc):Results.NotFound());
 app.MapPost("/api/contracts/{id:guid}/reviews",async(Guid id,IRepository repo,IQueue queue,CancellationToken ct)=>{
  if((await repo.Get(id,ct))?.Status!=ProcessingStatus.Ready)return Results.Conflict("Contract not ready");
  var review=new Review(Guid.NewGuid(),id,"Queued",[]);
  await repo.SaveReview(review,ct);await queue.Send(new Job("review",id,review.Id),ct);
  return Results.Accepted($"/api/reviews/{review.Id}",review);
 });
 app.MapGet("/api/reviews/{id:guid}",async(Guid id,IRepository repo,CancellationToken ct)=>await repo.GetReview(id,ct) is {} review?Results.Ok(review):Results.NotFound());
}
app.Run();
