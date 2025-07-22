namespace MusicBro;

public static class Constants
{
    public static class Messages
    {
        public const string NotInVoiceChannel = "You need to be in a voice channel";
        public const string FailedToJoinVoiceChannel = "Failed to join voice channel";
        public const string ProvideQuery = "Please provide a YouTube URL or search term";
        public const string CouldNotProcessTrack = "Could not find or process the track";
        public const string AddedToQueue = "Added to queue: {0}";
        public const string NothingPlaying = "Nothing is currently playing";
        public const string AddedToFrontOfQueue = "Added to front of queue: {0}";
        public const string QueueEmpty = "Queue is empty";
        public const string QueueHeader = "**Queue:**\n";
        public const string NowPlayingWithProgress = "**Now Playing:** {0} (requested by {1}) `{2} / {3}`";
        public const string UpNext = "**Up Next:**\n";
        public const string QueueItem = "{0}. {1} (requested by {2})\n";
        public const string AndMoreTracks = "... and {0} more tracks";
        public const string NowPlayingImmediate = "Now playing: {0}";
        public const string CommandError = "An error occurred while executing the command.";
        public const string QueueCleared = "Queue cleared";
        public const string QueueShuffled = "Queue shuffled";
        public const string PlaylistAdded = "Added {0} tracks from playlist to queue";
        public const string Paused = "Playback paused";
        public const string Resumed = "Playback resumed";
        public const string AlreadyPaused = "Playback is already paused";
        public const string NotPaused = "Playback is not paused";
        public const string AutoPaused = "Auto-paused: no one else in voice channel";
        
        public const string HelpTitle = "**MusicBro Commands:**\n";
        public const string HelpSummon = "**{0}summon** / **{0}join** - Join your voice channel\n";
        public const string HelpPlay = "**{0}play** / **{0}p** `<query>` - Play a song from YouTube URL/search term or add entire playlist\n";
        public const string HelpSkip = "**{0}skip** / **{0}s** - Skip the current song\n";
        public const string HelpQueue = "**{0}queue** / **{0}q** - Show the current queue\n";
        public const string HelpPlayNext = "**{0}playnext** `<query>` - Add a song to the front of the queue\n";
        public const string HelpPlayNow = "**{0}playnow** / **{0}pnow** `<query>` - Play a song immediately, skipping current track\n";
        public const string HelpClear = "**{0}clear** - Clear the queue\n";
        public const string HelpShuffle = "**{0}shuffle** - Shuffle the queue\n";
        public const string HelpPause = "**{0}pause** - Pause playback\n";
        public const string HelpResume = "**{0}resume** - Resume playback\n";
        public const string HelpAutoPlaylist = "**{0}autoplaylist** / **{0}ap** `list|get <name>|set <name>|add <name> <url>|edit <name> [url]` - Manage autoplaylists\n";
        public const string HelpRestart = "**{0}restart** - Restart the bot (disconnect and reconnect)\n";
        public const string HelpHelp = "**{0}help** - Show this help message";

        public const string AutoPlaylistGetUsage = "Usage: `autoplaylist get <name>`";
        public const string AutoPlaylistSetUsage = "Usage: `autoplaylist set <name>`";
        public const string AutoPlaylistAddUsage = "Usage: `autoplaylist add <name> <url>`";
        public const string AutoPlaylistEditUsage = "Usage: `autoplaylist edit <name> [url]`";
        public const string NoAutoPlaylistsFound = "No autoplaylists found";
        public const string AutoPlaylistListHeader = "**Available autoplaylists:**\n";
        public const string AutoPlaylistListItem = "• {0}\n";
        public const string AutoPlaylistNotFound = "Autoplaylist '{0}' not found";
        public const string AutoPlaylistGetSuccess = "Autoplaylist '{0}' sent as {1}";
        public const string AutoPlaylistGetError = "Error reading autoplaylist '{0}'";
        public const string AutoPlaylistUpdated = "Autoplaylist '{0}' updated";
        public const string AutoPlaylistCreated = "Autoplaylist '{0}' created";
        public const string AutoPlaylistActivated = "Active autoplaylist set to '{0}'";
        
        // Button labels and messages
        public const string ButtonPause = "Pause";
        public const string ButtonPlay = "Resume";
        public const string ButtonSkip = "Skip";
        public const string SkippedBy = "{0} skipped by {1}";
        
        // Restart messages
        public const string RestartingBot = "Restarting bot...";
        
    }
}