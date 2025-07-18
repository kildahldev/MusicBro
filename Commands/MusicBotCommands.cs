using Microsoft.Extensions.Logging;
using MusicBro.Models;
using MusicBro.Services;
using Microsoft.Extensions.Configuration;
using static MusicBro.Constants.Messages;

namespace MusicBro.Commands;

public class MusicBotCommands
{
    private readonly ILogger<MusicBotCommands> _logger;
    private readonly QueueManager _queueManager;
    private readonly YouTubeService _youtubeService;
    private readonly VoiceService _voiceService;
    private readonly AutoPlaylistService _autoPlaylistService;

    public MusicBotCommands(ILogger<MusicBotCommands> logger, QueueManager queueManager, YouTubeService youtubeService, VoiceService voiceService, AutoPlaylistService autoPlaylistService)
    {
        _logger = logger;
        _queueManager = queueManager;
        _youtubeService = youtubeService;
        _voiceService = voiceService;
        _autoPlaylistService = autoPlaylistService;
    }

    [Command("summon", "join")]
    public async Task<string> SummonAsync(CommandContext context)
    {
        var result = await _voiceService.JoinVoiceChannelAsync(
            context.Message.Author, 
            context.Message.GuildId!.Value, 
            context.Client, 
            context.Message.ChannelId).ConfigureAwait(false);

        if (result == JoinedVoiceChannel)
        {
            // Start autoplaylist if queue is empty
            if (!_queueManager.Queue.IsPlaying && _queueManager.Queue.Count == 0)
            {
                ThreadPool.QueueUserWorkItem(_ => {
                    _queueManager.PlayNextAsync().Wait();
                });
            }
        }

        return result;
    }
    
    [Command("play", "p")]
    public async Task<string> PlayAsync(CommandContext context)
    {
        _logger.LogDebug("PlayAsync command started for query: {Query}", context.Message.Content);
        
        var parts = context.Message.Content.Split(' ', 2);
        if (parts.Length < 2)
        {
            return ProvideQuery;
        }
        
        var query = parts[1].Trim();
        var author = context.Message.Author;
        
        // Auto-join voice channel if bot is not in one
        if (_voiceService.VoiceClient == null)
        {
            _logger.LogDebug("Bot not in voice channel, attempting to join");
            var joinResult = await _voiceService.JoinVoiceChannelAsync(
                context.Message.Author, 
                context.Message.GuildId!.Value, 
                context.Client, 
                context.Message.ChannelId);
            if (joinResult != JoinedVoiceChannel)
            {
                _logger.LogDebug("Failed to join voice channel: {Result}", joinResult);
                return joinResult;
            }
        }
        
        // Check if it's a playlist URL
        if (query.Contains("playlist?list="))
        {
            _logger.LogDebug("Processing playlist for query: {Query}", query);
            var playlistTracks = await _youtubeService.GetPlaylistTracksAsync(query, author.Username, author.Id.ToString());
            if (playlistTracks.Count == 0)
            {
                _logger.LogDebug("No tracks found in playlist: {Query}", query);
                return CouldNotProcessTrack;
            }
            
            var wasEmpty = !_queueManager.Queue.IsPlaying && _queueManager.Queue.Count == 0;
            
            // Add all tracks to queue
            foreach (var track in playlistTracks)
            {
                _queueManager.Queue.Enqueue(track);
            }
            
            // Set message channel and start playing if queue was empty
            _voiceService.SetMessageChannel(context.Message.ChannelId);
            if (wasEmpty)
            {
                await _queueManager.PlayNextAsync();
            }
            
            _logger.LogDebug("Playlist processing completed for query: {Query}", query);
            return string.Format(PlaylistAdded, playlistTracks.Count);
        }
        
        // Single track handling
        _logger.LogDebug("Processing single track for query: {Query}", query);
        var singleTrack = await _youtubeService.GetCompleteTrackAsync(query, author.Username, author.Id.ToString());
        if (singleTrack == null)
        {
            _logger.LogDebug("Failed to get track for query: {Query}", query);
            return CouldNotProcessTrack;
        }
        
        var wasEmptyBeforeAdd = !_queueManager.Queue.IsPlaying && _queueManager.Queue.Count == 0;
        await _queueManager.AddAndPlayAsync(singleTrack, context.Message.ChannelId);
        
        _logger.LogDebug("PlayAsync command completed for query: {Query}", query);
        if (wasEmptyBeforeAdd)
        {
            return string.Empty; // QueueManager will send "now playing" message
        }
        else
        {
            return string.Format(AddedToQueue, singleTrack.Title);
        }
    }
    
    [Command("skip", "s")]
    public async Task<string> Skip(CommandContext context)
    {
        var trackToSkip = _queueManager.Queue.CurrentTrack.Title;
        if (_queueManager.Queue.CurrentTrack == null)
        {
            return NothingPlaying;
        }

        await _queueManager.SkipAsync();
        return $"{context.Message.Author.GlobalName} skipped {trackToSkip}";
    }
    
    [Command("playnext")]
    public async Task<string> PlayNext(CommandContext context)
    {
        var parts = context.Message.Content.Split(' ', 2);
        if (parts.Length < 2)
        {
            return ProvideQuery;
        }
        
        var query = parts[1].Trim();
        var author = context.Message.Author;
        
        var track = await _youtubeService.GetCompleteTrackAsync(query, author.Username, author.Id.ToString());
        if (track == null)
        {
            return CouldNotProcessTrack;
        }
        
        await _queueManager.AddNextAsync(track).ConfigureAwait(false);
        return string.Format(AddedToFrontOfQueue, track.Title);
    }
    
    [Command("queue", "q")]
    public Task<string> Queue(CommandContext context)
    {
        var queue = _queueManager.Queue;
        
        var upcomingTracks = queue.GetTracks();
        
        if (upcomingTracks.Count == 0)
        {
            return Task.FromResult(QueueEmpty);
        }
        
        var result = QueueHeader;
        
        if (upcomingTracks.Count > 0)
        {
            result += UpNext;
            for (int i = 0; i < Math.Min(upcomingTracks.Count, 10); i++)
            {
                var track = upcomingTracks[i];
                result += string.Format(QueueItem, i + 1, track.Title, track.RequestedBy);
            }
            
            if (upcomingTracks.Count > 10)
            {
                result += string.Format(AndMoreTracks, upcomingTracks.Count - 10);
            }
        }
        
        return Task.FromResult(result);
    }
    
    [Command("playnow", "pnow")]
    public async Task<string> PlayNow(CommandContext context)
    {
        var parts = context.Message.Content.Split(' ', 2);
        if (parts.Length < 2)
        {
            return ProvideQuery;
        }
        
        var query = parts[1].Trim();
        var author = context.Message.Author;
        
        var track = await _youtubeService.GetCompleteTrackAsync(query, author.Username, author.Id.ToString());
        if (track == null)
        {
            return CouldNotProcessTrack;
        }
        
        await _queueManager.PlayNowAsync(track).ConfigureAwait(false);
        return string.Format(NowPlayingImmediate, track.Title);
    }
    
    [Command("clear")]
    public Task<string> Clear(CommandContext context)
    {
        _queueManager.ClearQueue();
        return Task.FromResult(Constants.Messages.QueueCleared);
    }
    
    [Command("shuffle")]
    public Task<string> Shuffle(CommandContext context)
    {
        if (_queueManager.Queue.Count == 0)
        {
            return Task.FromResult(Constants.Messages.QueueEmpty);
        }
        
        _queueManager.Queue.Shuffle();
        return Task.FromResult(Constants.Messages.QueueShuffled);
    }
    
    [Command("pause")]
    public Task<string> Pause(CommandContext context)
    {
        if (!_queueManager.Queue.IsPlaying)
        {
            return Task.FromResult(NothingPlaying);
        }
        
        if (_voiceService.IsPaused)
        {
            return Task.FromResult(AlreadyPaused);
        }
        
        _voiceService.Pause();
        return Task.FromResult(Paused);
    }
    
    [Command("resume")]
    public Task<string> Resume(CommandContext context)
    {
        if (!_queueManager.Queue.IsPlaying)
        {
            return Task.FromResult(NothingPlaying);
        }
        
        if (!_voiceService.IsPaused)
        {
            return Task.FromResult(NotPaused);
        }
        
        _voiceService.Resume();
        return Task.FromResult(Resumed);
    }
    
    [Command("autoplaylist", "ap")]
    public async Task<string> AutoPlaylist(CommandContext context)
    {
        var parts = context.Message.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return HelpAutoPlaylist;
        }
        
        var subcommand = parts[1].ToLower();
        
        switch (subcommand)
        {
            case "list":
                var playlists = await _autoPlaylistService.GetAvailablePlaylistsAsync();
                if (playlists.Count == 0)
                {
                    return NoAutoPlaylistsFound;
                }
                
                var activePlaylist = await _autoPlaylistService.GetActivePlaylistAsync();
                var result = AutoPlaylistListHeader;
                foreach (var playlist in playlists)
                {
                    var isActive = playlist == activePlaylist;
                    var displayName = isActive ? $"{playlist} (active)" : playlist;
                    result += string.Format(AutoPlaylistListItem, displayName);
                }
                return result;
                
            case "get":
                if (parts.Length < 3)
                {
                    return AutoPlaylistGetUsage;
                }
                
                var playlistPath = await _autoPlaylistService.GetPlaylistPathAsync(parts[2]);
                if (playlistPath == null)
                {
                    return string.Format(AutoPlaylistNotFound, parts[2]);
                }
                
                try
                {
                    var fileName = $"{parts[2]}.txt";
                    return string.Format(AutoPlaylistGetSuccess, parts[2], fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading autoplaylist file {Name}", parts[2]);
                    return string.Format(AutoPlaylistGetError, parts[2]);
                }
                
            case "set":
                if (parts.Length < 3)
                {
                    return AutoPlaylistSetUsage;
                }
                
                var setSuccess = await _autoPlaylistService.SetActivePlaylistAsync(parts[2]);
                return setSuccess 
                    ? string.Format(AutoPlaylistActivated, parts[2])
                    : string.Format(AutoPlaylistNotFound, parts[2]);
                    
            case "add":
                if (parts.Length < 4)
                {
                    return AutoPlaylistAddUsage;
                }
                
                var addSuccess = await _autoPlaylistService.SetPlaylistAsync(parts[2], parts[3]);
                return addSuccess 
                    ? string.Format(AutoPlaylistUpdated, parts[2]) 
                    : string.Format(AutoPlaylistCreated, parts[2]);
                    
            case "edit":
                if (parts.Length < 3)
                {
                    return AutoPlaylistEditUsage;
                }
                
                // Check if playlist exists first
                var editPlaylistPath = await _autoPlaylistService.GetPlaylistPathAsync(parts[2]);
                if (editPlaylistPath == null)
                {
                    return string.Format(AutoPlaylistNotFound, parts[2]);
                }
                
                var playlistUrl = parts.Length > 3 ? parts[3] : null;
                await _autoPlaylistService.SetPlaylistAsync(parts[2], playlistUrl);
                
                return string.Format(AutoPlaylistUpdated, parts[2]);
                
            default:
                return HelpAutoPlaylist;
        }
    }
    
    [Command("help")]
    public Task<string> Help(CommandContext context)
    {
        var prefix = Environment.GetEnvironmentVariable("DISCORD_PREFIX") ?? ".";

        var helpMessage = HelpTitle + 
                         string.Format(HelpSummon, prefix) + 
                         string.Format(HelpPlay, prefix) + 
                         string.Format(HelpSkip, prefix) + 
                         string.Format(HelpQueue, prefix) + 
                         string.Format(HelpPlayNext, prefix) + 
                         string.Format(HelpPlayNow, prefix) + 
                         string.Format(HelpClear, prefix) + 
                         string.Format(HelpShuffle, prefix) + 
                         string.Format(HelpPause, prefix) + 
                         string.Format(HelpResume, prefix) + 
                         string.Format(HelpAutoPlaylist, prefix) + 
                         string.Format(HelpHelp, prefix);
        
        return Task.FromResult(helpMessage);
    }
}