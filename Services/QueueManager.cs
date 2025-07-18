using Microsoft.Extensions.Logging;
using MusicBro.Models;
using static MusicBro.Constants.Messages;

namespace MusicBro.Services;

public class QueueManager
{
    private readonly ILogger<QueueManager> _logger;
    private readonly AutoPlaylistService _autoPlaylistService;
    private readonly YouTubeService _youtubeService;
    private readonly VoiceService _voiceService;
    private readonly MusicQueue _queue = new();

    public QueueManager(ILogger<QueueManager> logger, AutoPlaylistService autoPlaylistService, YouTubeService youtubeService, VoiceService voiceService)
    {
        _logger = logger;
        _autoPlaylistService = autoPlaylistService;
        _youtubeService = youtubeService;
        _voiceService = voiceService;
    }
    
    public MusicQueue Queue => _queue;
    
    public async Task PlayNextAsync()
    {
        var nextTrack = _queue.GetNext();
        if (nextTrack != null && _voiceService.VoiceClient != null)
        {
            _logger.LogInformation("Playing next track: {Title}", nextTrack.Title);
            _queue.IsPlaying = true;
            try
            {
                await _voiceService.PlayTrackAsync(nextTrack);
                // Only play next track if not cancelled (skip was called)
                await PlayNextAsync();
            }
            catch (OperationCanceledException)
            {
                // Playback was cancelled (skip was called), don't continue
            }
        }
        else if (_voiceService.VoiceClient != null)
        {
            // Queue is empty, try to play from autoplaylist
            var autoTrack = await TryGetAutoPlaylistTrackAsync();
            if (autoTrack != null)
            {
                _logger.LogInformation("Playing autoplaylist track: {Title}", autoTrack.Title);
                _queue.IsPlaying = true;
                _queue.SetCurrentTrack(autoTrack);
                try
                {
                    await _voiceService.PlayTrackAsync(autoTrack);
                    // Only play next track if not cancelled
                    await PlayNextAsync();
                }
                catch (OperationCanceledException)
                {
                    // Playback was cancelled
                }
            }
            else
            {
                _queue.IsPlaying = false;
                _logger.LogInformation("Queue is empty and no autoplaylist available, stopping playback");
                
                // Clean up current message when playback stops
                await _voiceService.CleanupCurrentMessageAsync();
            }
        }
        else
        {
            _queue.IsPlaying = false;
            _logger.LogInformation("Queue is empty, stopping playback");
            
            // Clean up current message when playback stops
            await _voiceService.CleanupCurrentMessageAsync();
        }
    }

    private async Task<Track?> TryGetAutoPlaylistTrackAsync()
    {
        try
        {
            var url = await _autoPlaylistService.GetRandomTrackUrlAsync();
            if (url == null) return null;

            var track = await _youtubeService.GetCompleteTrackAsync(url, "AutoPlaylist", "autoplaylist");
            return track;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting autoplaylist track");
            return null;
        }
    }
    
    public async Task AddAndPlayAsync(Track track, ulong channelId)
    {
        _voiceService.SetMessageChannel(channelId);
        _queue.Enqueue(track);
        
        if (!_queue.IsPlaying)
        {
            await PlayNextAsync();
        }
    }
    
    public async Task AddNextAsync(Track track)
    {
        _queue.EnqueueNext(track);
        
        if (!_queue.IsPlaying)
        {
            await PlayNextAsync();
        }
    }
    
    public async Task SkipAsync()
    {
        // Cancel current playback
        _voiceService.StopPlayback();
        _queue.Skip();
        ThreadPool.QueueUserWorkItem(_ => {
            PlayNextAsync().Wait();
        });

    }
    
    public async Task PlayNowAsync(Track track)
    {
        _queue.EnqueueNext(track);
        _queue.Skip();
        await PlayNextAsync();
    }
    
    public void ClearQueue()
    {
        _queue.ClearQueue();
    }
}