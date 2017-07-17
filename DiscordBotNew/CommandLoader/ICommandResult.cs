namespace DiscordBotNew.CommandLoader
{
    public interface ICommandResult
    {
        string Message { get; set; }

        string ToString();
    }
}
