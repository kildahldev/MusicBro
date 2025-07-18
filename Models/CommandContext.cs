using NetCord;
using NetCord.Gateway;

namespace MusicBro.Models;

public class CommandContext
{
    public required Message Message { get; set; }
    public required GatewayClient Client { get; set; }
}