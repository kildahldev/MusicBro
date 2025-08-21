using Microsoft.Extensions.Logging;
using MusicBro.Helpers;

namespace MusicBro.Services;

public class AutoPlaylistService
{
    private readonly ILogger<AutoPlaylistService> _logger;
    private readonly YouTubeService _youtubeService;
    private readonly string _playlistsPath;
    private readonly string _activePlaylistConfigPath;
    private readonly Random _random = new();

    public AutoPlaylistService(ILogger<AutoPlaylistService> logger, YouTubeService youtubeService)
    {
        _logger = logger;
        _youtubeService = youtubeService;
        _playlistsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autoplaylists");
        _activePlaylistConfigPath = Path.Combine(_playlistsPath, "activeplaylist.config");
        
        // Create autoplaylists directory if it doesn't exist
        if (!Directory.Exists(_playlistsPath))
        {
            Directory.CreateDirectory(_playlistsPath);
            _logger.LogInformation("Created autoplaylists directory: {PlaylistsPath}", _playlistsPath);
        }
    }

    private string? FindActualPlaylistName(string name)
    {
        try
        {
            var playlistFiles = Directory.GetFiles(_playlistsPath, "*.txt");
            var actualFile = playlistFiles.FirstOrDefault(f => 
                Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase));
            return actualFile != null ? Path.GetFileNameWithoutExtension(actualFile) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetRandomTrackUrlAsync()
    {
        try
        {
            // Try to get the active playlist first
            var activePlaylistName = await GetActivePlaylistAsync();
            string? playlistFile = null;
            
            if (activePlaylistName != null)
            {
                playlistFile = await GetPlaylistPathAsync(activePlaylistName);
            }
            
            // If no active playlist or active playlist doesn't exist, fall back to first available
            if (playlistFile == null)
            {
                var playlistFiles = Directory.GetFiles(_playlistsPath, "*.txt");
                if (playlistFiles.Length == 0)
                {
                    _logger.LogWarning("No autoplaylist files found in {PlaylistsPath}", _playlistsPath);
                    return null;
                }
                playlistFile = playlistFiles[0];
            }

            _logger.LogInformation("Using autoplaylist: {PlaylistFile}", Path.GetFileName(playlistFile));

            var urls = await File.ReadAllLinesAsync(playlistFile);
            var validUrls = urls.Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#")).ToArray();

            if (validUrls.Length == 0)
            {
                _logger.LogWarning("No valid URLs found in autoplaylist: {PlaylistFile}", Path.GetFileName(playlistFile));
                return null;
            }

            var randomUrl = validUrls[_random.Next(validUrls.Length)];
            _logger.LogInformation("Selected random track from autoplaylist: {Url}", randomUrl);
            
            return randomUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading autoplaylist");
            return null;
        }
    }

    public Task<List<string>> GetAvailablePlaylistsAsync()
    {
        try
        {
            var playlistFiles = Directory.GetFiles(_playlistsPath, "*.txt");
            return Task.FromResult(playlistFiles.Select(f => Path.GetFileNameWithoutExtension(f)!).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available playlists");
            return Task.FromResult(new List<string>());
        }
    }

    public Task<string?> GetPlaylistPathAsync(string name)
    {
        try
        {
            var playlistFiles = Directory.GetFiles(_playlistsPath, "*.txt");
            var actualFile = playlistFiles.FirstOrDefault(f => 
                Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase));
            
            return Task.FromResult(actualFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playlist path for {Name}", name);
            return Task.FromResult<string?>(null);
        }
    }

    public async Task<bool> SetPlaylistAsync(string name, string? playlistUrl = null)
    {
        try
        {
            // Use actual filename case if exists, otherwise use provided name
            var actualName = FindActualPlaylistName(name) ?? name;
            var playlistPath = Path.Combine(_playlistsPath, $"{actualName}.txt");
            var existed = File.Exists(playlistPath);
            
            if (playlistUrl != null)
            {
                // Check if it's a playlist URL
                if (PlaylistHelper.IsPlaylistUrl(playlistUrl))
                {
                    // Fetch all tracks from the playlist and write their URLs to the file
                    _logger.LogInformation("Fetching tracks from playlist: {PlaylistUrl}", playlistUrl);
                    var playlistTracks = await _youtubeService.GetPlaylistTracksAsync(playlistUrl, "AutoPlaylist", "autoplaylist");
                    
                    if (playlistTracks.Count > 0)
                    {
                        var trackUrls = playlistTracks.Where(t => !string.IsNullOrEmpty(t.Url)).Select(t => t.Url).ToList();
                        await File.WriteAllLinesAsync(playlistPath, trackUrls);
                        _logger.LogInformation("Saved {TrackCount} tracks to autoplaylist {Name}", trackUrls.Count, name);
                    }
                    else
                    {
                        _logger.LogWarning("No tracks found in playlist: {PlaylistUrl}", playlistUrl);
                        await File.WriteAllTextAsync(playlistPath, "");
                    }
                }
                else
                {
                    // Single track URL or other content
                    await File.WriteAllTextAsync(playlistPath, playlistUrl);
                }
            }
            else
            {
                // Create empty file if it doesn't exist
                if (!existed)
                {
                    await File.WriteAllTextAsync(playlistPath, "");
                }
            }
            
            _logger.LogInformation("Playlist {Name} {Action}", actualName, existed ? "updated" : "created");
            return existed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting playlist {Name}", name);
            return false;
        }
    }

    public async Task<string?> GetActivePlaylistAsync()
    {
        try
        {
            if (File.Exists(_activePlaylistConfigPath))
            {
                var activePlaylistName = await File.ReadAllTextAsync(_activePlaylistConfigPath);
                return string.IsNullOrWhiteSpace(activePlaylistName) ? null : activePlaylistName.Trim();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading active playlist config");
            return null;
        }
    }

    public async Task<bool> SetActivePlaylistAsync(string name)
    {
        try
        {
            var actualName = FindActualPlaylistName(name);
            if (actualName == null)
            {
                return false;
            }
            
            await File.WriteAllTextAsync(_activePlaylistConfigPath, actualName);
            _logger.LogInformation("Active playlist set to: {Name}", actualName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active playlist to {Name}", name);
            return false;
        }
    }

    public async Task<bool> DeletePlaylistAsync(string name)
    {
        try
        {
            var actualName = FindActualPlaylistName(name);
            if (actualName == null)
            {
                return false;
            }
            
            var playlistPath = Path.Combine(_playlistsPath, $"{actualName}.txt");

            // Check if this is the active playlist and clear it if so
            var activePlaylist = await GetActivePlaylistAsync();
            if (activePlaylist?.Equals(actualName, StringComparison.OrdinalIgnoreCase) == true)
            {
                if (File.Exists(_activePlaylistConfigPath))
                {
                    File.Delete(_activePlaylistConfigPath);
                    _logger.LogInformation("Cleared active playlist as {Name} was deleted", actualName);
                }
            }

            File.Delete(playlistPath);
            _logger.LogInformation("Deleted playlist: {Name}", actualName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting playlist {Name}", name);
            return false;
        }
    }
}