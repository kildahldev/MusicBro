using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using MusicBro.Commands;
using MusicBro.Models;

namespace MusicBro.Services;

public class MusicBotService : BackgroundService
{
    private readonly GatewayClient _client;
    private readonly ILogger<MusicBotService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly VoiceService _voiceService;
    private readonly QueueManager _queueManager;
    private readonly Dictionary<string, (object instance, MethodInfo method)> _commands = new();

    public MusicBotService(GatewayClient client, ILogger<MusicBotService> logger, IServiceProvider serviceProvider, IConfiguration configuration, VoiceService voiceService, QueueManager queueManager)
    {
        _client = client;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _voiceService = voiceService;
        _queueManager = queueManager;
        RegisterCommands();
    }

    private void RegisterCommands()
    {
        var commandsInstance = ActivatorUtilities.CreateInstance<MusicBotCommands>(_serviceProvider);
        var methods = typeof(MusicBotCommands).GetMethods();

        foreach (var method in methods)
        {
            var commandAttr = method.GetCustomAttribute<CommandAttribute>();
            if (commandAttr != null)
            {
                foreach (var name in commandAttr.Names)
                {
                    _commands[name] = (commandsInstance, method);
                    _logger.LogInformation("Registered command: {CommandName}", name);
                }
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.MessageCreate += HandleMessageAsync;
        _client.InteractionCreate += HandleInteractionCreateAsync;
        _client.Ready += (readyEventArgs) =>
        {
            _logger.LogInformation("Bot is ready! Logged in as {Username}", readyEventArgs.User.Username);
            return ValueTask.CompletedTask;
        };

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async ValueTask HandleMessageAsync(Message message)
    {
        var prefix = Environment.GetEnvironmentVariable("DISCORD_PREFIX") ?? ".";
        
        if (message.Author.IsBot || !message.Content.StartsWith(prefix))
            return;

        var command = message.Content.Substring(prefix.Length).Split(' ')[0].ToLower();

        if (_commands.TryGetValue(command, out var commandInfo))
        {
            try
            {
                var context = new CommandContext { Message = message, Client = _client };
                var result = await (Task<string>)commandInfo.method.Invoke(commandInfo.instance, new object[] { context })!;
                if (result != null)
                {
                    var channel = await _client.Rest.GetChannelAsync(message.ChannelId);
                    if (channel is TextChannel textChannel)
                    {
                        await textChannel.SendMessageAsync(result);
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command {Command}", command);
                try
                {
                    if (message.Channel != null)
                    {
                        await message.Channel.SendMessageAsync(Constants.Messages.CommandError);
                    }
                }
                catch (Exception sendEx)
                {
                    _logger.LogError(sendEx, "Failed to send error message");
                }
            }
        }
    }

    private async ValueTask HandleInteractionCreateAsync(Interaction interaction)
    {
        // Only handle component interactions (button clicks)
        if (interaction is not ComponentInteraction componentInteraction)
            return;
            
        try
        {
            var customId = componentInteraction.Data.CustomId;
            _logger.LogInformation("Component interaction received: {CustomId}", customId);

            switch (customId)
            {
                case "music_pause":
                    if (_queueManager.Queue.IsPlaying && !_voiceService.IsPaused)
                    {
                        _voiceService.Pause();
                        await interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options => options.Content = options.Content));
                    }
                    else
                    {
                        await interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options => options.Content = options.Content));
                    }
                    break;

                case "music_resume":
                    if (_queueManager.Queue.IsPlaying && _voiceService.IsPaused)
                    {
                        _voiceService.Resume();
                        await interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options => options.Content = options.Content));
                    }
                    else
                    {
                        await interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options => options.Content = options.Content));
                    }
                    break;

                case "music_skip":
                    if (_queueManager.Queue.CurrentTrack != null)
                    {
                        var trackToSkip = _queueManager.Queue.CurrentTrack.Title;
                        await _queueManager.SkipAsync();
                        
                        // Send message about who skipped the track
                        var channel = await _client.Rest.GetChannelAsync(componentInteraction.Channel.Id);
                        if (channel is TextChannel textChannel)
                        {
                            var skipMessage = string.Format(Constants.Messages.SkippedBy, trackToSkip, componentInteraction.User.Username);
                            await textChannel.SendMessageAsync(skipMessage);
                        }
                        
                        await interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options => options.Content = options.Content));
                    }
                    else
                    {
                        await interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options => options.Content = options.Content));
                    }
                    break;

                default:
                    _logger.LogWarning("Unknown component interaction: {CustomId}", customId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling component interaction");
            // Don't try to respond to expired interactions
        }
    }
}