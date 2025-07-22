using NetCord.Hosting.Gateway;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using MusicBro.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";
});

var logLevel = builder.Configuration["LogLevel"];
if (!string.IsNullOrEmpty(logLevel) && Enum.TryParse<LogLevel>(logLevel, out var parsedLogLevel))
{
    builder.Logging.SetMinimumLevel(parsedLogLevel);
}

builder.Services
    .AddDiscordGateway(options =>
    {
        options.Token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        options.Intents = GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.MessageContent | GatewayIntents.GuildVoiceStates | GatewayIntents.GuildUsers;
    })
    .AddSingleton<VoiceService>()
    .AddSingleton<QueueManager>()
    .AddSingleton<YouTubeService>()
    .AddSingleton<DownloadService>()
    .AddSingleton<AutoPlaylistService>()
    .AddHostedService<MusicBotService>()
    .AddHostedService<AutoLeaveService>();

var host = builder.Build();

await host.RunAsync();