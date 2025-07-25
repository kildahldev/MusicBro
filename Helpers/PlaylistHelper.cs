using Microsoft.Extensions.Logging;
using MusicBro.Services;
using NetCord;
using static MusicBro.Constants.Messages;

namespace MusicBro.Helpers;

public static class PlaylistHelper
{
    public static async Task<string> ProcessPlaylistAsync(
        string query, 
        User author, 
        ILogger logger,
        YouTubeService youtubeService,
        QueueManager queueManager,
        VoiceService voiceService,
        ulong messageChannelId)
    {
        logger.LogDebug("Processing playlist for query: {Query}", query);
        var playlistTracks = await youtubeService.GetPlaylistTracksAsync(query, author.Username, author.Id.ToString());
        if (playlistTracks.Count == 0)
        {
            logger.LogDebug("No tracks found in playlist: {Query}", query);
            return CouldNotProcessTrack;
        }
        
        var wasEmpty = !queueManager.Queue.IsPlaying && queueManager.Queue.Count == 0;
        
        // Add all tracks to queue
        foreach (var track in playlistTracks)
        {
            queueManager.Queue.Enqueue(track);
        }
        
        // Set message channel and start playing if queue was empty
        voiceService.SetMessageChannel(messageChannelId);
        if (wasEmpty)
        {
            await queueManager.PlayNextAsync();
        }
        
        logger.LogDebug("Playlist processing completed for query: {Query}", query);
        return string.Format(PlaylistAdded, playlistTracks.Count);
    }

    public static bool IsPlaylistUrl(string query)
    {
        return query.Contains("playlist?list=");
    }
}