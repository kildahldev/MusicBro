# MusicBro

A simple selfhosted Discord music bot built with C# and .NET 9.0 that plays music from YouTube.
No fancy features, just straightforward music playback, autoplaylists and queue system.

Features inspired by https://github.com/Just-Some-Bots/MusicBot

## Version History
#### v1.4 (Latest)
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
```

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
- `.autoplaylist` / `.ap` - Manage autoplaylists (list|get|set|add|edit)
- `.help` - Show help message

## Source Code

Available (soon) at: https://github.com/kildahldev/musicbro

## License

This project is open source and available under the GPL-3.0 License.