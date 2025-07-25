using MusicBro.Models;
using NetCord;
using NetCord.Rest;

namespace MusicBro.Helpers;

public static class VoiceHelper
{
    public static async Task<ulong?> GetUserVoiceChannelIdAsync(CommandContext context)
    {
        try
        {
            var guild = await context.Client.Rest.GetGuildAsync(context.Message.GuildId!.Value);
            var voiceState = await guild.GetUserVoiceStateAsync(context.Message.Author.Id);
            return voiceState.ChannelId;
        }
        catch
        {
            return null;
        }
    }
}