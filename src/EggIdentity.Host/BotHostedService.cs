using System.Reflection;
using Discord.WebSocket;
using EggIdentity.Bot;
using EggIdentity.Db;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace EggIdentity.Host;

public sealed class BotHostedService(string configFilePath, string postgresConnectionString) : IHostedService {
    public EggIdentityBot? Bot { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken) {
        var builder = new EggIdentityBotBuilder()
            .WithConfigFile(configFilePath)
            .WithEnvFallback(key => key == "POSTGRES_CONNECTION_STRING" ? postgresConnectionString : Environment.GetEnvironmentVariable(key))
            .WithName("EggIdentity")
            .WithBuild(BuildInfo.Build(Environment.GetEnvironmentVariable, Assembly.GetExecutingAssembly()));

        var cfg = builder.BuildConfig();

        try {
            Bot = await EggIdentityBot.StartAsync(cfg, builder);
        } catch (GatewayReconnectException ex) {
            Console.Error.WriteLine(
                $"eggidentity: bot start failed - gateway rejected the connection, likely because the " +
                $"GuildMembers privileged intent isn't enabled for this bot application: {ex.Message}");
        } catch (Exception ex) {
            Console.Error.WriteLine($"eggidentity: bot start failed, continuing: {ex.Message}");
        }

        if (Bot is null || !ulong.TryParse(cfg.GuildId, out var guildId))
            return;

        var dataSource = NpgsqlDataSource.Create(postgresConnectionString);
        await using (var conn = await dataSource.OpenConnectionAsync(cancellationToken))
            await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "BotMigrations"), cancellationToken);

        var channelConfigStore = new ChannelConfigStore(dataSource);
        var notifier = new DeployNotifier(channelConfigStore, Bot.Client, guildId, cfg.Name);
        var deployStateStore = new DeployStateStore(dataSource);
        var tracker = new DeployVersionTracker(deployStateStore, notifier);
        try {
            await tracker.CheckAndNotifyAsync(cfg.Name, Environment.GetEnvironmentVariable("GIT_SHA") ?? "", cfg.Build.Version, cancellationToken);
        } catch (Exception ex) {
            Console.Error.WriteLine($"eggidentity: deploy self-report failed, continuing: {ex.Message}");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        if (Bot is not null) await Bot.DisposeAsync();
    }
}
