using System.Security.Cryptography;
using System.Text;
using EggIdentity.Agent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

var configDir = Environment.GetEnvironmentVariable("AGENT_CONFIG_DIR");
if (string.IsNullOrEmpty(configDir)) configDir = "/etc/eggidentity/agents";
var port = Environment.GetEnvironmentVariable("AGENT_PORT");
if (string.IsNullOrEmpty(port)) port = "7777";
var intervalStr = Environment.GetEnvironmentVariable("AGENT_WATCH_INTERVAL");
if (string.IsNullOrEmpty(intervalStr)) intervalStr = "1m";
var notifySecret = Environment.GetEnvironmentVariable("DEPLOY_NOTIFY_SECRET") ?? "";

AgentRegistry registry;
try { registry = AgentRegistry.LoadFromDir(configDir); } catch (Exception e) { Console.Error.WriteLine($"eggidentity-agent: load config dir: {e.Message}"); return 1; }

TimeSpan interval;
try { interval = AgentConfig.ParseDuration(intervalStr); } catch (Exception e) { Console.Error.WriteLine($"eggidentity-agent: AGENT_WATCH_INTERVAL: {e.Message}"); return 1; }

var orchestrator = new AgentOrchestrator(registry, interval, notifySecret);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://*:{port}");
var app = builder.Build();

static bool IsAuthorized(AgentConfig cfg, HttpRequest req) {
    var secret = Environment.GetEnvironmentVariable(cfg.SecretEnv) ?? "";
    var token = (req.Headers.Authorization.ToString() ?? "").Replace("Bearer ", "");
    return secret != "" && CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(secret));
}

app.MapPost("/deploy/{appName}", async (string appName, HttpRequest req) => {
    if (!registry.Apps.TryGetValue(appName, out var cfg)) return Results.NotFound();
    if (!IsAuthorized(cfg, req)) return Results.Text("unauthorized", "text/plain", null, StatusCodes.Status401Unauthorized);
    var (res, ran) = await orchestrator.TryDeployAsync(appName);
    if (!ran) return Results.Text("deploy already in progress", "text/plain", null, StatusCodes.Status409Conflict);
    return Results.Json(res);
});

app.MapPost("/deploy/{appName}/fast", async (string appName, HttpRequest req) => {
    if (!registry.Apps.TryGetValue(appName, out var cfg)) return Results.NotFound();
    if (!IsAuthorized(cfg, req)) return Results.Text("unauthorized", "text/plain", null, StatusCodes.Status401Unauthorized);
    if (!orchestrator.HasFastPipeline(appName))
        return Results.Text($"fast deploy not configured for {appName}", "text/plain", null, StatusCodes.Status400BadRequest);
    var (res, ran) = await orchestrator.TryDeployFastAsync(appName);
    if (!ran) return Results.Text("deploy already in progress", "text/plain", null, StatusCodes.Status409Conflict);
    return Results.Json(res);
});

_ = orchestrator.RunAsync(app.Lifetime.ApplicationStopping);
Console.WriteLine($"eggidentity-agent: watching {registry.Apps.Count} app(s) every {interval}: {string.Join(", ", registry.Apps.Keys)}");
Console.WriteLine($"eggidentity-agent: listening on :{port}");
app.Run();
return 0;
