namespace MusicBro.Commands;

[AttributeUsage(AttributeTargets.Method)]
public class CommandAttribute : Attribute
{
    public string[] Names { get; }

    public CommandAttribute(params string[] names)
    {
        Names = names;
    }
}