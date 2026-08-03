using System.Reflection;
using Discord.WebSocket;
using EggIdentity.Bot;
using EggIdentity.Db;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace EggIdentity.Core;

public sealed class EggIdentityCoreBotHostedService(string configFilePath) : IHostedService {
    public EggIdentityBot? Bot { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken) {
        var builder = new EggIdentityBotBuilder()
            .WithConfigFile(configFilePath)
            .WithName("EggIdentity")
            .WithBuild(BuildInfo.Build(Environment.GetEnvironmentVariable, Assembly.GetExecutingAssembly()));

        var cfg = builder.BuildConfig();

        try {
            Bot = await EggIdentityBot.StartAsync(cfg, builder);
        } catch (GatewayReconnectException ex) {
            Console.Error.WriteLine(
                $"eggidentity-core: bot start failed - gateway rejected the connection, likely because the " +
                $"GuildMembers privileged intent isn't enabled for this bot application: {ex.Message}");
        } catch (Exception ex) {
            Console.Error.WriteLine($"eggidentity-core: bot start failed, continuing: {ex.Message}");
        }

        if (Bot is null || string.IsNullOrEmpty(cfg.PostgresConnectionString) || !ulong.TryParse(cfg.GuildId, out var guildId))
            return;

        var dataSource = NpgsqlDataSource.Create(cfg.PostgresConnectionString);
        await using (var conn = await dataSource.OpenConnectionAsync(cancellationToken))
            await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"), cancellationToken);

        var channelConfigStore = new ChannelConfigStore(dataSource);
        var notifier = new DeployNotifier(channelConfigStore, Bot.Client, guildId, cfg.Name);
        var deployStateStore = new DeployStateStore(dataSource);
        var tracker = new DeployVersionTracker(deployStateStore, notifier);
        try {
            await tracker.CheckAndNotifyAsync(cfg.Name, Environment.GetEnvironmentVariable("GIT_SHA") ?? "", cfg.Build.Version, cancellationToken);
        } catch (Exception ex) {
            Console.Error.WriteLine($"eggidentity-core: deploy self-report failed, continuing: {ex.Message}");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        if (Bot is not null) await Bot.DisposeAsync();
    }
}
