using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Mogmog.Discord.Services;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Mogmog.Discord
{
    public static class Program
    {
        static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

        static async Task MainAsync(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            var disConfig = new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true,
                LargeThreshold = 250,
                MessageCacheSize = 100,
            };

            // Initialize the ASP.NET service provider and freeze this Task indefinitely.
            using var services = ConfigureServices(disConfig);

            var client = services.GetRequiredService<DiscordSocketClient>();
            var mogmog = services.GetRequiredService<MogmogConnectionService>();
            client.Log += LogAsync;
            client.MessageReceived += mogmog.DiscordMessageReceivedAsync;

            await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN"));
            await client.StartAsync();
            try
            {
                foreach (SocketGuild guild in client.Guilds)
                {
                    foreach (SocketTextChannel channel in guild.TextChannels)
                    {
                        channel.GetMessagesAsync();
                    }
                }
            } catch {};

            AppDomain.CurrentDomain.ProcessExit += (sender, args) => ProcessExit(services);

            await Task.Delay(-1);
        }

        static Task LogAsync(LogMessage message)
        {
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                    Log.Error(message.ToString());
                    break;
                case LogSeverity.Error:
                    Log.Error(message.ToString());
                    break;
                case LogSeverity.Warning:
                    Log.Warning(message.ToString());
                    break;
                case LogSeverity.Info:
                    Log.Information(message.ToString());
                    break;
                case LogSeverity.Verbose:
                    Log.Verbose(message.ToString());
                    break;
                case LogSeverity.Debug:
                    Log.Debug(message.ToString());
                    break;
                default:
                    Log.Verbose(message.ToString());
                    break;
            }
            return Task.CompletedTask;
        }

        static ServiceProvider ConfigureServices(DiscordSocketConfig disConfig)
        {
            return new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(disConfig))
                .AddSingleton<MogmogConnectionService>()
                .BuildServiceProvider();
        }

        static void ProcessExit(ServiceProvider services)
        {
            services.Dispose();
        }
    }
}
