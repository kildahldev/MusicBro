using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MusicBro.Models;

namespace MusicBro.Services;

public class DownloadService
{
    private readonly ILogger<DownloadService> _logger;
    private readonly string _ytDlpPath;

    public DownloadService(ILogger<DownloadService> logger)
    {
        _logger = logger;
        _ytDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "yt-dlp");
    }

    public async Task<string?> DownloadAudioAsync(string videoUrl, string title)
    {
        _logger.LogDebug("Starting yt-dlp download for: {Title} ({VideoUrl})", title, videoUrl);
        
        try
        {
            var downloadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
            Directory.CreateDirectory(downloadsDir);
            // Return the expected filename based on the actual title
            var sanitizedTitle = SanitizeFilename(title);
            var filename = Path.Combine(downloadsDir, sanitizedTitle + ".mp3");
            
            // Check if file already exists
            if (File.Exists(filename))
            {
                _logger.LogDebug("File already exists, skipping download: {Title}", title);
                return filename;
            }
            
            var startInfo = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = $"-f bestaudio --extract-audio --audio-format mp3 -o \"{filename}\" --no-playlist \"{videoUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _logger.LogDebug("Starting yt-dlp process for: {Title}", title);
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start yt-dlp download process");
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("yt-dlp download failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                return null;
            }

            _logger.LogDebug("yt-dlp download completed for: {Title}", title);
            return filename;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading audio for {VideoUrl}", videoUrl);
            return null;
        }
    }

    private static string SanitizeFilename(string filename)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(filename.Where(c => !invalidChars.Contains(c)).ToArray());
        
        // Replace problematic characters that might cause issues with yt-dlp
        sanitized = sanitized.Replace("\"", "").Replace("'", "").Replace("`", "");
        
        // Trim whitespace and dots
        sanitized = sanitized.Trim().Trim('.');
        
        // Limit filename length to avoid filesystem issues
        if (sanitized.Length > 100)
        {
            sanitized = sanitized.Substring(0, 100).Trim();
        }
        
        // Ensure we have a valid filename
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "track";
        }
        
        return sanitized;
    }
}