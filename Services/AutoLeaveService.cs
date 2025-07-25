using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord;

namespace MusicBro.Services;

public class AutoLeaveService : BackgroundService
{
    private readonly ILogger<AutoLeaveService> _logger;
    private readonly GatewayClient _client;
    private readonly VoiceService _voiceService;
    private readonly QueueManager _queueManager;
    private ulong? _botUserId;

    public AutoLeaveService(
        ILogger<AutoLeaveService> logger,
        GatewayClient client,
        VoiceService voiceService,
        QueueManager queueManager)
    {
        _logger = logger;
        _client = client;
        _voiceService = voiceService;
        _queueManager = queueManager;

        // Subscribe to voice state updates to detect when humans leave
        _client.VoiceStateUpdate += HandleVoiceStateUpdate;
        _client.Ready += async (readyEventArgs) =>
        {
            _botUserId = readyEventArgs.User.Id;
            await ValueTask.CompletedTask;
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check every hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                await CheckAndLeaveIfAloneAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutoLeaveService hourly check");
            }
        }
    }

    private async ValueTask HandleVoiceStateUpdate(VoiceState voiceState)
    {
        try
        {
            // Ignore bot's own voice state changes
            if (_botUserId.HasValue && voiceState.UserId == _botUserId.Value)
                return;

            // Only care if we're in a voice channel
            if (_voiceService.VoiceClient == null || !_voiceService.CurrentGuildId.HasValue ||
                !_voiceService.CurrentVoiceChannelId.HasValue)
                return;

            var guildId = _voiceService.CurrentGuildId.Value;
            var voiceChannelId = _voiceService.CurrentVoiceChannelId.Value;

            // Check if this voice state change is in our guild and channel
            if (voiceState.GuildId == guildId)
            {
                // Check if a human left our voice channel
                var user = await _client.Rest.GetUserAsync(voiceState.UserId);
                if (!user.IsBot && voiceState.ChannelId != voiceChannelId &&
                    await WasUserInOurChannelAsync(voiceState.UserId, guildId, voiceChannelId))
                {
                    _logger.LogInformation("Human user {UserId} left our voice channel, checking if we're alone",
                        voiceState.UserId);

                    // Trigger immediate check with 3 attempts
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await CheckAndLeaveIfAloneWithRetriesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in immediate alone check after user left");
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling voice state update in AutoLeaveService");
        }
    }

    private async Task<bool> WasUserInOurChannelAsync(ulong userId, ulong guildId, ulong voiceChannelId)
    {
        try
        {
            // Check if the user is currently in any voice channel
            var guild = await _client.Rest.GetGuildAsync(guildId);
            await guild.GetUserVoiceStateAsync(userId);

            //If we can get the voice state without an exception, the user is still in a channel
            return false;
        }
        catch
        {
            return true;
        }
    }

    private async Task CheckAndLeaveIfAloneWithRetriesAsync()
    {
        // Only check if we're in a voice channel
        if (_voiceService.VoiceClient == null || !_voiceService.CurrentGuildId.HasValue ||
            !_voiceService.CurrentVoiceChannelId.HasValue)
        {
            return;
        }

        var guildId = _voiceService.CurrentGuildId.Value;
        var voiceChannelId = _voiceService.CurrentVoiceChannelId.Value;

        _logger.LogDebug("Checking if alone in voice channel {ChannelId} with retries", voiceChannelId);

        // Check if we're alone (3 attempts with 10 second intervals)
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var hasHumans = await HasHumansInVoiceChannelAsync(guildId, voiceChannelId);

            if (hasHumans)
            {
                _logger.LogDebug("Found humans in voice channel, staying");
                return;
            }

            _logger.LogDebug("No humans found in voice channel, attempt {Attempt}/3", attempt);

            if (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        // No humans found after 3 attempts, leave the voice channel
        _logger.LogInformation("No humans found in voice channel after 3 attempts, leaving");
        await LeaveVoiceChannelAsync();
    }

    private async Task CheckAndLeaveIfAloneAsync()
    {
        try
        {
            // Only check if we're in a voice channel
            if (_voiceService.VoiceClient == null || !_voiceService.CurrentGuildId.HasValue ||
                !_voiceService.CurrentVoiceChannelId.HasValue)
            {
                return;
            }

            var guildId = _voiceService.CurrentGuildId.Value;
            var voiceChannelId = _voiceService.CurrentVoiceChannelId.Value;

            _logger.LogDebug("Hourly check: checking if alone in voice channel {ChannelId}", voiceChannelId);

            var hasHumans = await HasHumansInVoiceChannelAsync(guildId, voiceChannelId);

            if (!hasHumans)
            {
                _logger.LogInformation("Hourly check: No humans found in voice channel, leaving");
                await LeaveVoiceChannelAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in hourly voice channel check");
        }
    }

    private async Task<bool> HasHumansInVoiceChannelAsync(ulong guildId, ulong voiceChannelId)
    {
        try
        {
            var guild = await _client.Rest.GetGuildAsync(guildId);

            // Get all guild users and check their voice states
            await foreach (var user in guild.GetUsersAsync())
            {
                if (!user.IsBot)
                {
                    try
                    {
                        var userVoiceState = await guild.GetUserVoiceStateAsync(user.Id);
                        if (userVoiceState.ChannelId == voiceChannelId)
                        {
                            _logger.LogDebug("Found human user {UserId} in voice channel {ChannelId}", user.Id,
                                voiceChannelId);
                            return true; // Found a human in our channel
                        }
                    }
                    catch
                    {
                        // Continue checking other users if voice state fails
                    }
                }
            }

            _logger.LogDebug("No humans found in voice channel {ChannelId}", voiceChannelId);
            return false; // Only bots or empty
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for humans in voice channel {ChannelId}", voiceChannelId);
            return true; // Assume humans are present on error to avoid unintended leaving
        }
    }

    private async Task LeaveVoiceChannelAsync()
    {
        try
        {
            await _voiceService.LeaveVoiceChannelAsync();
            _logger.LogInformation("Bot left voice channel due to inactivity");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving voice channel due to inactivity");
        }
    }
}