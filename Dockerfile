# Use the official .NET 9.0 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app

# Install required dependencies for audio processing
RUN apk add --no-cache \
    ffmpeg \
    python3 \
    py3-pip \
    opus-dev

# Install yt-dlp
RUN pip3 install --break-system-packages yt-dlp

# Use the SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["MusicBro.csproj", "."]
RUN dotnet restore "MusicBro.csproj"

# Copy all source files
COPY . .

# Build the application
RUN dotnet build "MusicBro.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "MusicBro.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app

# Copy the published application
COPY --from=publish /app/publish .

# Create directories for tools and data
RUN mkdir -p /app/tools /app/autoplaylists /app/downloads

# Copy tools directory if it exists in build context
COPY tools/ /app/tools/
COPY autoplaylists/ /app/autoplaylists/

# Make sure ffmpeg and yt-dlp are executable
RUN chmod +x /app/tools/ffmpeg /app/tools/yt-dlp || true

# Create a non-root user
RUN addgroup -g 1000 musicbot && adduser -u 1000 -G musicbot -s /bin/sh -D musicbot
RUN chown -R musicbot:musicbot /app
USER musicbot

ENTRYPOINT ["dotnet", "MusicBro.dll"]