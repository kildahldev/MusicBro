using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Playlists;
using Microsoft.Extensions.Logging;
using MusicBro.Models;
using YoutubeExplode.Common;

namespace MusicBro.Services;

public class YouTubeService
{
    private readonly YoutubeClient _youtubeClient;
    private readonly ILogger<YouTubeService> _logger;
    private readonly DownloadService _downloadService;

    public YouTubeService(ILogger<YouTubeService> logger, DownloadService downloadService)
    {
        _youtubeClient = new YoutubeClient();
        _logger = logger;
        _downloadService = downloadService;
    }

    public async Task<Track?> SearchAndCreateTrackAsync(string query, string requestedBy, string requestedById)
    {
        try
        {
            // Check if query is already a YouTube URL
            if (IsYouTubeUrl(query))
            {
                _logger.LogDebug("Starting YouTube video metadata fetch for URL: {Query}", query);
                var urlVideo = await _youtubeClient.Videos.GetAsync(query);
                _logger.LogDebug("Completed YouTube video metadata fetch for URL: {Query}", query);
                return CreateTrackFromVideo(urlVideo, requestedBy, requestedById);
            }
            
            // Search for videos
            _logger.LogDebug("Starting YouTube search for query: {Query}", query);
            var firstResult = await _youtubeClient.Search.GetVideosAsync(query).FirstOrDefaultAsync();

            if (firstResult == null)
            {
                _logger.LogWarning("No search results found for query: {Query}", query);
                return null;
            }
            _logger.LogDebug("YouTube search completed, fetching video metadata for: {VideoId}", firstResult.Id);
            var searchVideo = await _youtubeClient.Videos.GetAsync(firstResult.Id);
            _logger.LogDebug("Completed YouTube search and metadata fetch for query: {Query}", query);
            
            return CreateTrackFromVideo(searchVideo, requestedBy, requestedById);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching YouTube for query: {Query}", query);
            return null;
        }
    }

    private Track CreateTrackFromVideo(Video video, string requestedBy, string requestedById)
    {
        return new Track
        {
            Title = video.Title,
            Url = video.Url,
            Duration = video.Duration ?? TimeSpan.Zero,
            RequestedBy = requestedBy,
            RequestedById = requestedById,
            Thumbnail = video.Thumbnails.GetWithHighestResolution()?.Url ?? string.Empty
        };
    }

    private Track CreateTrackFromPlaylistVideo(YoutubeExplode.Playlists.PlaylistVideo video, string requestedBy, string requestedById)
    {
        return new Track
        {
            Title = video.Title,
            Url = video.Url,
            Duration = video.Duration ?? TimeSpan.Zero,
            RequestedBy = requestedBy,
            RequestedById = requestedById,
            Thumbnail = video.Thumbnails.GetWithHighestResolution()?.Url ?? string.Empty
        };
    }

    public async Task<Track?> GetCompleteTrackAsync(string query, string requestedBy, string requestedById)
    {
        _logger.LogDebug("Starting GetCompleteTrackAsync for query: {Query}", query);
        
        // Search YouTube for the track
        var track = await SearchAndCreateTrackAsync(query, requestedBy, requestedById);
        if (track == null)
        {
            _logger.LogDebug("GetCompleteTrackAsync failed - no track found for query: {Query}", query);
            return null;
        }
        
        // Only download if not already downloaded
        if (string.IsNullOrEmpty(track.LocalFilePath))
        {
            _logger.LogDebug("Starting audio download for track: {Title}", track.Title);
            // Download the audio file using yt-dlp
            var filePath = await _downloadService.DownloadAudioAsync(track.Url, track.Title);
            if (filePath == null)
            {
                _logger.LogDebug("GetCompleteTrackAsync failed - download failed for track: {Title}", track.Title);
                return null;
            }
            
            track.LocalFilePath = filePath;
            _logger.LogDebug("Audio download completed for track: {Title}", track.Title);
        }
        else
        {
            _logger.LogDebug("Using existing file for track: {Title}", track.Title);
        }
        
        _logger.LogDebug("GetCompleteTrackAsync completed for query: {Query}", query);
        return track;
    }

    private bool IsYouTubeUrl(string query)
    {
        return query.Contains("youtube.com") || query.Contains("youtu.be");
    }
    
    private bool IsPlaylistUrl(string query)
    {
        return query.Contains("playlist?list=");
    }
    
    public async Task<List<Track>> GetPlaylistTracksAsync(string playlistUrl, string requestedBy, string requestedById)
    {
        var tracks = new List<Track>();
        
        try
        {
            var playlist = await _youtubeClient.Playlists.GetAsync(playlistUrl);
            var playlistVideos = await _youtubeClient.Playlists.GetVideosAsync(playlist.Id).ToListAsync();
            
            _logger.LogInformation("Found {Count} videos in playlist: {Title}", playlistVideos.Count, playlist.Title);
            
            foreach (var video in playlistVideos)
            {
                try
                {
                    var track = CreateTrackFromPlaylistVideo(video, requestedBy, requestedById);
                    tracks.Add(track);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process video {VideoId} from playlist", video.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing playlist: {PlaylistUrl}", playlistUrl);
        }
        
        return tracks;
    }
}