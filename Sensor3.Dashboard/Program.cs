using Sensor3.Infrastructure;
using Sensor3.Dashboard.Components;
using Sensor3.Distribution;

var builder = WebApplication.CreateBuilder(args);
builder.AddDistribution();
builder.AddSensorTelemetry();
builder.Services.AddCascadingAuthenticationState();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton(Sensor3.Core.BuildMetadata.Load(typeof(Program).Assembly));
builder.Services.AddSingleton(Sensor3.Core.BuildMetadata.LoadIterations(typeof(Program).Assembly));
builder.Services.AddHealthChecks();
// Per-circuit receiver: browser sensors and fabricated client identities are never registered.
builder.Services.AddScoped<Sensor3.Sensors.SensorDiagnosticsStore>();
builder.Services.AddScoped<Sensor3.Contracts.ISensorDiagnosticsReceiver>(x => x.GetRequiredService<Sensor3.Sensors.SensorDiagnosticsStore>());
var app = builder.Build();
app.MapHealthChecks("/health");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseDistribution();
app.MapDistribution();
app.MapSensorTelemetry();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Sensor3.SharedUI._Imports).Assembly);

app.Run();


