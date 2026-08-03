using System.Net.Http.Headers;
using System.Security.Claims;
using EggIdentity.Auth;
using EggIdentity.Client;
using EggIdentity.Core;
using EggIdentity.Core.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

const string CookieScheme = "EggIdentityCoreCookie";

var listenAddr = Environment.GetEnvironmentVariable("LISTEN_ADDR") ?? ":8095";
var configFilePath = Environment.GetEnvironmentVariable("EGGIDENTITY_CORE_CONFIG_FILE") ?? "/etc/eggidentity/config.env";

var authentikAuthority = Environment.GetEnvironmentVariable("AUTHENTIK_AUTHORITY");
var coreClientId = Environment.GetEnvironmentVariable("EGGIDENTITY_CORE_CLIENT_ID");
var coreClientSecret = Environment.GetEnvironmentVariable("EGGIDENTITY_CORE_CLIENT_SECRET");
var coreCallbackPath = Environment.GetEnvironmentVariable("EGGIDENTITY_CORE_CALLBACK_PATH") ?? "/signin-oidc";
var identityApiUrl = Environment.GetEnvironmentVariable("IDENTITY_API_URL");
var identityApiSecret = Environment.GetEnvironmentVariable("IDENTITY_API_SECRET");

var authEnabled = !string.IsNullOrEmpty(authentikAuthority) && !string.IsNullOrEmpty(coreClientId) &&
    !string.IsNullOrEmpty(coreClientSecret) && !string.IsNullOrEmpty(identityApiUrl) && !string.IsNullOrEmpty(identityApiSecret);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(listenAddr.StartsWith(':') ? $"http://0.0.0.0{listenAddr}" : $"http://{listenAddr}");

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var authBuilder = builder.Services.AddAuthentication(CookieScheme)
    .AddCookie(CookieScheme, o => {
        if (authEnabled) {
            o.Events.OnValidatePrincipal = ctx => AuthentikAspNetAuth.OnValidatePrincipalCheckRevoked(
                ctx, ctx.HttpContext.RequestServices.GetRequiredService<IdentityApiClient>(),
                ClaimTypes.NameIdentifier, SessionClaims.Role);
        }
    });

if (authEnabled) {
    builder.Services.AddHttpClient<IdentityApiClient>(c => {
        c.BaseAddress = new Uri(identityApiUrl!);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", identityApiSecret);
    });

    AuthentikAspNetAuth.AddIfConfigured(authBuilder, new AuthentikAspNetAuthOptions {
        CookieScheme = CookieScheme,
        Authority = authentikAuthority!,
        ClientId = coreClientId!,
        ClientSecret = coreClientSecret!,
        CallbackPath = coreCallbackPath,
        UserIdClaim = ClaimTypes.NameIdentifier,
        RoleClaim = SessionClaims.Role,
        DiscordIdClaim = SessionClaims.DiscordId,
    });
}

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddSingleton(new EggIdentityCoreBotHostedService(configFilePath));
builder.Services.AddHostedService(sp => sp.GetRequiredService<EggIdentityCoreBotHostedService>());
builder.Services.AddScoped(sp => sp.GetRequiredService<EggIdentityCoreBotHostedService>().Bot?.ConfigService!);

var app = builder.Build();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/admin/login", () =>
    Results.Challenge(new AuthenticationProperties { RedirectUri = "/admin" }, [OpenIdConnectDefaults.AuthenticationScheme]));

app.MapRazorComponents<AppHost>().AddInteractiveServerRenderMode();

app.Run();
