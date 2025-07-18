using NetCord;
using NetCord.Rest;
using static MusicBro.Constants.Messages;

namespace MusicBro.Helpers;

public static class PlaybackButtonHelper
{
    public static ActionRowProperties CreatePlaybackButtons(bool isPaused)
    {
        // Create single pause/play toggle button based on current state
        var pausePlayButton = isPaused 
            ? new ButtonProperties("music_resume", ButtonPlay, ButtonStyle.Success)
            : new ButtonProperties("music_pause", ButtonPause, ButtonStyle.Primary);
            
        return new ActionRowProperties()
            .AddButtons(
                pausePlayButton,
                new ButtonProperties("music_skip", ButtonSkip, ButtonStyle.Secondary)
            );
    }
}