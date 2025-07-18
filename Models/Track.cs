namespace MusicBro.Models;

public class Track
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string RequestedById { get; set; } = string.Empty;
    public string? LocalFilePath { get; set; }
    public string Thumbnail { get; set; } = string.Empty;
}