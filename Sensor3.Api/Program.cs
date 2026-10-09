using Sensor3.Core;
using Sensor3.Distribution;
var builder = WebApplication.CreateBuilder(args);
builder.AddDistribution();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton(BuildMetadata.Load(typeof(Program).Assembly));
builder.Services.AddSingleton(BuildMetadata.LoadIterations(typeof(Program).Assembly));
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseDistribution();
app.MapDistribution();
app.MapHealthChecks("/health");
app.MapGet("/api/system/build-info", (Sensor3.Contracts.BuildInfo info) => info);
app.MapGet("/api/system/iterations", (IReadOnlyList<Sensor3.Contracts.IterationInfo> entries) => entries);
app.Logger.LogInformation("Sensor 3 API startar");
app.Run();
