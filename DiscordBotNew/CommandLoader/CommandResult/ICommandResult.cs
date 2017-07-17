namespace DiscordBotNew.CommandLoader.CommandResult
{
    public interface ICommandResult
    {
        string Message { get; set; }

        string ToString();
    }
}
