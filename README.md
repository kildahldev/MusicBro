# MusicBro

A simple selfhosted Discord music bot built with C# and .NET 9.0 that plays music from YouTube.
No fancy features, just straightforward music playback, autoplaylists and queue system.

Features inspired by Just-Some-Bots/MusicBot

## Version History

#### v1.7 (Latest)
- Added optional YouTube cookies.txt support, as YouTube can be harsh with 403s
#### v1.6
- Commands now provide immediate feedback when requesting a song, doesn't wait for download to finish anymore
- When adding playlist URLs, all individual tracks are now properly saved to file
- `.ap get` command now sends autoplaylist files as Discord attachments
- Added `.ap delete` command to remove autoplaylists
- All autoplaylist commands now work case-insensitively
- Fixed cases where autoplaylist wouldn't start again after bot auto-leaving
- Added input validation for autoplaylist commands

#### v1.5
- Added .restart command for the bot
- Bot now automatically leaves voice channels when inactive
- Bot now automatically self-deafens when joining voice channels
- Various bug fixes

#### v1.4
- Changed audio format from WAV to MP3 for storage space efficiency

#### v1.3
- Fixed opus library support - resolved audio codec errors

#### v1.2
- Fixed file permissions - container now runs with UID/GID 1000
- Resolved volume mount permission issues

#### v1.1
- Optimized image size using Alpine

## Features

- Play music from YouTube URLs
- Queue system
- Auto-playlists support
- Lightweight Docker image

## Docker Quick Start

```bash
docker run -d \
  --name musicbro \
  -e DISCORD_TOKEN=YOUR_BOT_TOKEN \
  -e DISCORD_PREFIX=. \
  -v /mnt/docker/musicbro/autoplaylists:/app/autoplaylists \
  -v /mnt/docker/musicbro/downloads:/app/downloads \
  -v /mnt/docker/musicbro/cookies.txt:/app/cookies.txt \
  kildahldev/musicbro:latest
```

## Docker Compose

```yaml
version: '3.8'
services:
  musicbro:
    image: kildahldev/musicbro:latest
    container_name: musicbro
    restart: unless-stopped
    environment:
      - DISCORD_TOKEN=${DISCORD_TOKEN}
      - DISCORD_PREFIX=${DISCORD_PREFIX:-.}
      - LOGLEVEL=Information
    volumes:
      - /mnt/docker/musicbro/autoplaylists:/app/autoplaylists
      - /mnt/docker/musicbro/downloads:/app/downloads
      - /mnt/docker/musicbro/cookies.txt:/app/cookies.txt
```

## YouTube Cookies (Optional)

To avoid YouTube 403 errors and access age-restricted content, you can provide a cookies.txt file:

1. Export your YouTube cookies to a file named `cookies.txt` (use browser extensions like "Get cookies.txt")
2. Place it at `/mnt/docker/musicbro/cookies.txt` on your host system
3. The bot will automatically use it if the file exists and contains data

**Note:** The cookies.txt file is optional. The bot works fine without it for most content.

## Environment Variables

- `DISCORD_TOKEN` (Required): Your Discord bot token
- `DISCORD_PREFIX` (Optional): Command prefix, defaults to "."
- `LOGLEVEL` (Optional): Logging level, defaults to "Information"

## Commands

- `.summon` / `.join` - Join your voice channel
- `.play` / `.p <query>` - Play a song from YouTube URL/search term or add entire playlist
- `.skip` / `.s` - Skip the current song
- `.queue` / `.q` - Show the current queue
- `.playnext <query>` - Add a song to the front of the queue
- `.playnow` / `.pnow <query>` - Play a song immediately, skipping current track
- `.clear` - Clear the queue
- `.shuffle` - Shuffle the queue
- `.pause` - Pause playback
- `.resume` - Resume playback
- `.autoplaylist` / `.ap` - Manage autoplaylists (list|get|set|add|edit|delete)
- `.help` - Show help message

## Source Code

Available at: https://github.com/kildahldev/musicbro

## License

This project is open source and available under the GPL-3.0 License.
