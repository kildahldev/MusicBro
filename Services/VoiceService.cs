using Microsoft.Extensions.Logging;
using NetCord.Gateway.Voice;
using NetCord.Gateway;
using NetCord;
using NetCord.Rest;
using MusicBro.Models;
using MusicBro.Helpers;
using System.Diagnostics;
using static MusicBro.Constants.Messages;

namespace MusicBro.Services;

public class VoiceService
{
    public event Func<Task>? VoiceChannelJoined;
    private readonly ILogger<VoiceService> _logger;
    private readonly GatewayClient _client;
    private readonly YouTubeService _youtubeService;
    private VoiceClient? _voiceClient;
    private ulong? _messageChannelId;
    private ulong? _currentMessageId;
    private CancellationTokenSource? _progressCts;
    private CancellationTokenSource? _playbackCts;
    private long _currentStreamPosition;
    private bool _isPaused;
    private TaskCompletionSource<bool>? _pauseCompletionSource;
    private ulong? _currentVoiceChannelId;
    private ulong? _currentGuildId;
    private ulong? _botUserId;
    private Track? _currentTrack;
    private TimeSpan _savedProgress;

    public VoiceService(ILogger<VoiceService> logger, GatewayClient client, YouTubeService youtubeService)
    {
        _logger = logger;
        _client = client;
        _youtubeService = youtubeService;
        
        // Subscribe to voice state updates to detect bot movement
        _client.VoiceStateUpdate += HandleVoiceStateUpdate;
        _client.Ready += async (readyEventArgs) =>
        {
            _botUserId = readyEventArgs.User.Id;
            await ValueTask.CompletedTask;
        };
    }

    public VoiceClient? VoiceClient => _voiceClient;
    public bool IsPaused => _isPaused;
    public ulong? CurrentVoiceChannelId => _currentVoiceChannelId;
    public ulong? CurrentGuildId => _currentGuildId;

    public void SetVoiceClient(VoiceClient voiceClient, ulong guildId, ulong voiceChannelId)
    {
        _voiceClient = voiceClient;
        _currentGuildId = guildId;
        _currentVoiceChannelId = voiceChannelId;
        _logger.LogInformation("Voice client set for guild {GuildId}, channel {ChannelId}", guildId, voiceChannelId);
    }

    public void SetMessageChannel(ulong channelId)
    {
        _messageChannelId = channelId;
    }

    public void Pause()
    {
        if (!_isPaused)
        {
            _isPaused = true;
            _pauseCompletionSource = new TaskCompletionSource<bool>();
            _logger.LogInformation("Playback paused");
        }
    }

    public void Resume()
    {
        if (_isPaused)
        {
            _isPaused = false;
            _pauseCompletionSource?.SetResult(true);
            _pauseCompletionSource = null;
            _logger.LogInformation("Playback resumed");
        }
    }

    public async Task<bool> JoinVoiceChannelAsync(ulong voiceChannelId, ulong guildId, GatewayClient client, ulong messageChannelId)
    {
        _logger.LogDebug("Starting voice channel join for channel: {ChannelId}", voiceChannelId);

        try
        {
            _logger.LogDebug("Attempting to join voice channel: {ChannelId}", voiceChannelId);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var voiceClient = await client.JoinVoiceChannelAsync(guildId, voiceChannelId);
            _logger.LogDebug("Voice client created, starting connection");
            await voiceClient.StartAsync(cts.Token);
            
            SetVoiceClient(voiceClient, guildId, voiceChannelId);
            
            // Set the message channel
            SetMessageChannel(messageChannelId);
            
            // Self-deafen the bot
            try
            {
                var voiceStateProps = new VoiceStateProperties(guildId, voiceChannelId)
                {
                    SelfDeaf = true
                };
                await client.UpdateVoiceStateAsync(voiceStateProps);
                _logger.LogDebug("Bot self-deafened successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to self-deafen bot, continuing anyway");
            }
            
            _logger.LogInformation("Successfully joined voice channel: {ChannelId}", voiceChannelId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join voice channel");
            return false;
        }
    }

    public async Task PlayTrackAsync(Track track)
    {
        // If track doesn't have a local file path, try to get it
        if (string.IsNullOrEmpty(track.LocalFilePath))
        {
            var completeTrack = await _youtubeService.GetCompleteTrackAsync(track.Url, track.RequestedBy, track.RequestedById);
            if (completeTrack?.LocalFilePath != null)
            {
                track.LocalFilePath = completeTrack.LocalFilePath;
            }
            else
            {
                _logger.LogError("Failed to download audio for track: {Title}", track.Title);
                throw new InvalidOperationException($"Failed to download audio for track: {track.Title}");
            }
        }

        // Track the current track for potential restart after voice client recreation
        _currentTrack = track;
        
        _logger.LogInformation("Starting playback of track: {Title} from {FilePath}", track.Title, track.LocalFilePath);

        // Create new playback cancellation token
        _playbackCts?.Cancel();
        _playbackCts = new CancellationTokenSource();

        Process? ffmpeg = null;

        try
        {
            // Delete previous "now playing" message if it exists
            await CleanupCurrentMessageAsync();

            // Update bot status to show current track
            try
            {
                var presence = new PresenceProperties(UserStatusType.Online)
                    .AddActivities(new UserActivityProperties(track.Title, UserActivityType.Listening));
                await _client.UpdatePresenceAsync(presence);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update bot status");
            }

            // Send initial "now playing" message with playback controls
            if (_messageChannelId.HasValue)
            {
                var channel = await _client.Rest.GetChannelAsync(_messageChannelId.Value);
                if (channel is TextChannel textChannel)
                {
                    var initialProgress = _savedProgress > TimeSpan.Zero ? FormatDuration(_savedProgress) : "0:00";
                    var content = string.Format(NowPlayingWithProgress, $"[{track.Title}](<{track.Url}>)", track.RequestedBy, initialProgress, FormatDuration(track.Duration));
                    
                    // Create playback control buttons
                    var actionRow = PlaybackButtonHelper.CreatePlaybackButtons(_isPaused);
                    
                    var message = await textChannel.SendMessageAsync(new MessageProperties 
                    { 
                        Content = content 
                    }.AddComponents(new IComponentProperties[] { actionRow }));
                    _currentMessageId = message.Id;
                }
            }

            // Build FFmpeg arguments with optional seek for resuming playback and streaming-optimized loudness normalization
            var ffmpegArgs = _savedProgress > TimeSpan.Zero
                ? $@"-ss {_savedProgress:hh\:mm\:ss\.fff} -i ""{track.LocalFilePath}"" -af ""loudnorm=I=-16:LRA=11:TP=-1.5"" -ac 2 -f s16le -ar 48000 pipe:1"
                : $@"-i ""{track.LocalFilePath}"" -af ""loudnorm=I=-16:LRA=11:TP=-1.5"" -ac 2 -f s16le -ar 48000 pipe:1";

            if (_savedProgress > TimeSpan.Zero)
            {
                _logger.LogInformation("Resuming playback from {Position}", FormatDuration(_savedProgress));
            }

            ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ffmpeg"),
                Arguments = ffmpegArgs,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            if (ffmpeg == null)
            {
                _logger.LogError("Failed to start FFmpeg process");
                throw new InvalidOperationException("Failed to start FFmpeg process");
            }

            try
            {
                await _voiceClient!.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));
            }
            catch (ObjectDisposedException)
            {
                _logger.LogInformation("Voice client disposed while entering speaking state, aborting playback");
                throw new OperationCanceledException("Voice client disposed during setup");
            }

            var outStream = _voiceClient.CreateOutputStream();
            using var opusStream = new OpusEncodeStream(outStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);

            // Adjust stream position if resuming from saved progress
            if (_savedProgress > TimeSpan.Zero)
            {
                // Calculate initial stream position based on saved progress
                const long bytesPerSecond = 192000; // 48000 Hz * 2 channels * 2 bytes per sample
                _currentStreamPosition = (long)(_savedProgress.TotalSeconds * bytesPerSecond);
                _savedProgress = TimeSpan.Zero; // Reset after using
            }
            else
            {
                _currentStreamPosition = 0;
            }

            // Start progress tracking
            _progressCts?.Cancel();
            _progressCts = new CancellationTokenSource();
            _ = Task.Run(() => UpdateProgressAsync(track, _progressCts.Token));

            // Use cancellation token for the stream copy
            await CopyStreamWithProgressAsync(ffmpeg.StandardOutput.BaseStream, opusStream, track, _playbackCts.Token);
            
            try
            {
                await opusStream.FlushAsync(_playbackCts.Token);
            }
            catch (ObjectDisposedException)
            {
                // Voice client was disposed during flush, this is expected during channel moves
                _logger.LogInformation("OpusStream disposed during flush, likely due to voice client recreation");
            }
            
            await ffmpeg.WaitForExitAsync(_playbackCts.Token);

            // Stop progress tracking
            _progressCts?.Cancel();

            // Clear current track since it finished playing
            _currentTrack = null;
            
            _logger.LogInformation("Finished playing track: {Title}", track.Title);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Playback of track {Title} was cancelled", track.Title);
            _progressCts?.Cancel();

            // Kill FFmpeg process on cancellation
            if (ffmpeg != null && !ffmpeg.HasExited)
            {
                ffmpeg.Kill();
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing track: {Title}", track.Title);
            _progressCts?.Cancel();
            throw;
        }
    }

    public void StopPlayback()
    {
        _playbackCts?.Cancel();
        _progressCts?.Cancel();
    }

    private async Task CopyStreamWithProgressAsync(Stream source, Stream destination, Track track, CancellationToken cancellationToken)
    {
        const int bufferSize = 81920;
        var buffer = new byte[bufferSize];

        while (!cancellationToken.IsCancellationRequested)
        {
            // Check for pause
            if (_isPaused && _pauseCompletionSource != null)
            {
                await _pauseCompletionSource.Task;
            }

            var bytesRead = await source.ReadAsync(buffer, 0, bufferSize, cancellationToken);
            if (bytesRead == 0) break;

            try
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                // Voice client was disposed (likely during channel move),
                // Save current progress for resuming later
                const long bytesPerSecond = 192000; // 48000 Hz * 2 channels * 2 bytes per sample
                var currentPosition = Interlocked.Read(ref _currentStreamPosition);
                var elapsedSeconds = currentPosition / (double)bytesPerSecond;
                _savedProgress = TimeSpan.FromSeconds(elapsedSeconds);
                _logger.LogInformation("Voice client disposed during playback, saved progress: {Progress}", FormatDuration(_savedProgress));
                return;
            }

            // Update stream position
            Interlocked.Add(ref _currentStreamPosition, bytesRead);
        }
    }

    private async Task UpdateProgressAsync(Track track, CancellationToken cancellationToken)
    {
        const long bytesPerSecond = 192000; // 48000 Hz * 2 channels * 2 bytes per sample

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, cancellationToken);

                // Calculate elapsed time based on stream position
                var currentPosition = Interlocked.Read(ref _currentStreamPosition);
                var elapsedSeconds = currentPosition / (double)bytesPerSecond;
                var elapsed = TimeSpan.FromSeconds(elapsedSeconds);

                if (elapsed > track.Duration) elapsed = track.Duration;

                if (_messageChannelId.HasValue && _currentMessageId.HasValue)
                {
                    var channel = await _client.Rest.GetChannelAsync(_messageChannelId.Value);
                    if (channel is TextChannel textChannel)
                    {
                        // Add paused indicator to content
                        var pausedIndicator = _isPaused ? " **[PAUSED]**" : "";
                        var updatedContent = string.Format(NowPlayingWithProgress,
                            $"[{track.Title}](<{track.Url}>)",
                            track.RequestedBy,
                            FormatDuration(elapsed),
                            FormatDuration(track.Duration)) + pausedIndicator;

                        // Update playback control buttons
                        var actionRow = PlaybackButtonHelper.CreatePlaybackButtons(_isPaused);

                        await textChannel.ModifyMessageAsync(_currentMessageId.Value, options => 
                        {
                            options.Content = updatedContent;
                            options.Components = new IComponentProperties[] { actionRow };
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating progress for track: {Title}", track.Title);
                break;
            }
        }
    }

    public async Task CleanupCurrentMessageAsync()
    {
        // Clear bot status when playback stops
        try
        {
            var presence = new PresenceProperties(UserStatusType.Online);
            await _client.UpdatePresenceAsync(presence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear bot status");
        }

        if (_messageChannelId.HasValue && _currentMessageId.HasValue)
        {
            try
            {
                var channel = await _client.Rest.GetChannelAsync(_messageChannelId.Value);
                if (channel is TextChannel textChannel)
                {
                    await textChannel.DeleteMessageAsync(_currentMessageId.Value);
                }
                _currentMessageId = null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete current now playing message");
            }
        }
    }

    private async ValueTask HandleVoiceStateUpdate(VoiceState voiceState)
    {
        try
        {
            // Only care about our bot's voice state changes
            if (!_botUserId.HasValue || voiceState.UserId != _botUserId.Value) return;
            
            // Only care if we have an active voice client
            if (_voiceClient == null || !_currentGuildId.HasValue) return;
            
            // Check if bot was moved to a different channel in the same guild
            if (voiceState.GuildId == _currentGuildId.Value && 
                voiceState.ChannelId.HasValue && 
                voiceState.ChannelId != _currentVoiceChannelId)
            {
                _logger.LogInformation("Bot was moved from channel {OldChannel} to channel {NewChannel}", 
                    _currentVoiceChannelId, voiceState.ChannelId);
                
                await RecreateVoiceClientAsync(voiceState.GuildId, voiceState.ChannelId.Value);
            }
            // Check if bot was disconnected (removed from voice channel)
            else if (voiceState.GuildId == _currentGuildId.Value && !voiceState.ChannelId.HasValue)
            {
                _logger.LogInformation("Bot was disconnected from voice channel {OldChannel}", _currentVoiceChannelId);
                
                // Stop current playback and cleanup
                StopPlayback();
                await CleanupVoiceClientAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling voice state update");
        }
    }
    
    private async Task RecreateVoiceClientAsync(ulong guildId, ulong newChannelId)
    {
        try
        {
            _logger.LogInformation("Recreating voice client for new channel {ChannelId}", newChannelId);

            
            // Dispose old voice client (but keep guild/channel tracking for new client)
            if (_voiceClient != null)
            {
                _voiceClient.Dispose();
                _voiceClient = null;
            }

            Thread.Sleep(500);
            // Create new voice client for the new channel
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var newVoiceClient = await _client.JoinVoiceChannelAsync(guildId, newChannelId);
            await newVoiceClient.StartAsync(cts.Token);
            
            // Update our tracking
            _voiceClient = newVoiceClient;
            _currentVoiceChannelId = newChannelId;

            // Self-deafen the bot again
            try
            {
                var voiceStateProps = new VoiceStateProperties(guildId, newChannelId)
                {
                    SelfDeaf = true
                };
                await _client.UpdateVoiceStateAsync(voiceStateProps);
                _logger.LogDebug("Bot self-deafened successfully after recreation");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to self-deafen bot after recreation, continuing anyway");
            }

            // Restart current track if one was playing
            if (_currentTrack != null)
            {
                _logger.LogInformation("Restarting track playback after voice client recreation: {Title}", _currentTrack.Title);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await PlayTrackAsync(_currentTrack);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to restart track playback after voice client recreation");
                    }
                });
            }
            
            _logger.LogInformation("Successfully recreated voice client for channel {ChannelId}", newChannelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recreate voice client for channel {ChannelId}", newChannelId);
            await CleanupVoiceClientAsync();
        }
    }
    
    private async Task CleanupVoiceClientAsync()
    {
        try
        {
            if (_voiceClient != null)
            {
                _voiceClient.Dispose();
                _voiceClient = null;
            }
            
            _currentVoiceChannelId = null;
            _currentGuildId = null;
            
            // Clean up any current message
            await CleanupCurrentMessageAsync();
            
            _logger.LogInformation("Voice client cleaned up");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during voice client cleanup");
        }
    }
    
    public async Task LeaveVoiceChannelAsync()
    {
        try
        {
            StopPlayback();
            await CleanupCurrentMessageAsync();
            
            if (_voiceClient != null)
            {
                await _voiceClient.CloseAsync();
            }
            
            // Update voice state to tell Discord we're leaving
            if (_currentGuildId.HasValue)
            {
                try
                {
                    var voiceStateProps = new VoiceStateProperties(_currentGuildId.Value, null);
                    await _client.UpdateVoiceStateAsync(voiceStateProps);
                    _logger.LogDebug("Updated voice state to disconnect from voice channel");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update voice state when leaving, but continuing with cleanup");
                }
            }
            
            await CleanupVoiceClientAsync();
            _logger.LogInformation("Successfully left voice channel");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving voice channel");
        }
    }
    
    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }
}