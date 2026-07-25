using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using EggIdentity.Agent;

var secret = Environment.GetEnvironmentVariable("DEPLOY_AGENT_SECRET");
if (string.IsNullOrEmpty(secret)) {
    Console.Error.WriteLine("eggidentity-agent: DEPLOY_AGENT_SECRET is required");
    return 1;
}
var port = Environment.GetEnvironmentVariable("DEPLOY_AGENT_PORT");
if (string.IsNullOrEmpty(port)) port = "7777";
var configPath = Environment.GetEnvironmentVariable("DEPLOY_AGENT_CONFIG");
if (string.IsNullOrEmpty(configPath)) configPath = "/etc/eggidentity/deploy-agent.yaml";

string yaml;
try { yaml = File.ReadAllText(configPath); } catch (Exception e) { Console.Error.WriteLine($"eggidentity-agent: read config: {e.Message}"); return 1; }

AgentConfig cfg;
try { cfg = AgentConfig.Parse(yaml); } catch (Exception e) { Console.Error.WriteLine($"eggidentity-agent: parse config: {e.Message}"); return 1; }
if (cfg.Steps.Count == 0) {
    Console.Error.WriteLine("eggidentity-agent: config has no steps");
    return 1;
}

var executor = new Executor { Repo = cfg.Repo, RepoUrl = cfg.RepoUrl, Steps = cfg.Steps };
var handler = new DeployHandler(executor.Run);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://*:{port}");
var app = builder.Build();

app.MapPost("/deploy", (HttpRequest req) => {
    var token = (req.Headers.Authorization.ToString() ?? "").Replace("Bearer ", "");
    var ok = !string.IsNullOrEmpty(secret) && CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(secret));
    if (!ok) return Results.Text("unauthorized", "text/plain", null, StatusCodes.Status401Unauthorized);
    var (res, ran) = handler.TryRun();
    if (!ran) return Results.Text("deploy already in progress", "text/plain", null, StatusCodes.Status409Conflict);
    return Results.Json(res);
});

if (cfg.Watch is { } watch) {
    var notifySecret = Environment.GetEnvironmentVariable("DEPLOY_NOTIFY_SECRET") ?? "";
    if (string.IsNullOrEmpty(watch.NotifyBotUrl))
        Console.WriteLine("eggidentity-agent: watch notify disabled: notify_bot_url not configured");
    var watcher = new Watcher(cfg.Name, watch.Interval, watch.NotifyBotUrl, notifySecret, handler.TryRun);
    _ = watcher.RunAsync(app.Lifetime.ApplicationStopping);
    Console.WriteLine($"eggidentity-agent: watching every {watch.Interval}");
}

Console.WriteLine($"eggidentity-agent: {cfg.Name} listening on :{port} ({cfg.Steps.Count} steps)");
app.Run();
return 0;
