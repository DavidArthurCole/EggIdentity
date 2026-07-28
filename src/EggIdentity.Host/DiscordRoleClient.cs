using System.Net.Http.Headers;

namespace EggIdentity.Host;

public interface IDiscordRoleClient {
    Task AddRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct);
    Task RemoveRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct);
}

public sealed class DiscordRoleClient(IHttpClientFactory httpClientFactory, string botToken) : IDiscordRoleClient {
    public Task AddRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, guildId, discordUserId, roleId, ct);

    public Task RemoveRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, guildId, discordUserId, roleId, ct);

    private async Task SendAsync(HttpMethod method, string guildId, string discordUserId, string roleId, CancellationToken ct) {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        using var req = new HttpRequestMessage(method, BuildRoleUrl(guildId, discordUserId, roleId));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    public static string BuildRoleUrl(string guildId, string discordUserId, string roleId) =>
        $"https://discord.com/api/v10/guilds/{guildId}/members/{discordUserId}/roles/{roleId}";
}
