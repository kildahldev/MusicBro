using Microsoft.Extensions.Logging;

namespace MusicBro.Services;

public class AutoPlaylistService
{
    private readonly ILogger<AutoPlaylistService> _logger;
    private readonly string _playlistsPath;
    private readonly string _activePlaylistConfigPath;
    private readonly Random _random = new();

    public AutoPlaylistService(ILogger<AutoPlaylistService> logger)
    {
        _logger = logger;
        _playlistsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autoplaylists");
        _activePlaylistConfigPath = Path.Combine(_playlistsPath, "activeplaylist.config");
        
        // Create autoplaylists directory if it doesn't exist
        if (!Directory.Exists(_playlistsPath))
        {
            Directory.CreateDirectory(_playlistsPath);
            _logger.LogInformation("Created autoplaylists directory: {PlaylistsPath}", _playlistsPath);
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
            var playlistPath = Path.Combine(_playlistsPath, $"{name}.txt");
            return Task.FromResult(File.Exists(playlistPath) ? playlistPath : null);
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
            var playlistPath = Path.Combine(_playlistsPath, $"{name}.txt");
            var existed = File.Exists(playlistPath);
            
            if (playlistUrl != null)
            {
                // Write the playlist URL to the file
                await File.WriteAllTextAsync(playlistPath, playlistUrl);
            }
            else
            {
                // Create empty file if it doesn't exist
                if (!existed)
                {
                    await File.WriteAllTextAsync(playlistPath, "");
                }
            }
            
            _logger.LogInformation("Playlist {Name} {Action}", name, existed ? "updated" : "created");
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
            var playlistPath = Path.Combine(_playlistsPath, $"{name}.txt");
            if (!File.Exists(playlistPath))
            {
                return false;
            }
            
            await File.WriteAllTextAsync(_activePlaylistConfigPath, name);
            _logger.LogInformation("Active playlist set to: {Name}", name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active playlist to {Name}", name);
            return false;
        }
    }
}