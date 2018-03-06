using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBotNew.CommandLoader;
using DiscordBotNew.CommandLoader.CommandContext;
using DiscordBotNew.CommandLoader.CommandResult;
using DiscordBotNew.Settings;

namespace DiscordBotNew.Commands
{
    public static class ManagementCommands
    {
        [Command("username"), HelpText("Changes the bot's username"), Permissions(ownerOnly: true)]
        public static async Task<ICommandResult> ChangeUsername(DiscordUserMessageContext context, [JoinRemainingParameters, HelpText("The new username for the bot")] string username)
        {
            if (username.Length > 32)
            {
                return new ErrorResult($"Username must be no more than 32 characters in length. ({username.Length} > 32)");
            }

            await context.Bot.Client.CurrentUser.ModifyAsync(bot => bot.Username = username);
            return new SuccessResult("Changed username successfully");
        }

        [Command("avatar"), HelpText("Changes the bot's avatar image"), Permissions(ownerOnly: true)]
        public static async Task<ICommandResult> ChangeAvatar(DiscordUserMessageContext context, [JoinRemainingParameters, HelpText("The image URL for the bot's avatar")] string url)
        {
            try
            {
                await context.Message.AddReactionAsync(CommandTools.LoadingEmote);

                using (var fileStream = System.IO.File.Create("avatar.png"))
                {
                    var stream = await new HttpClient().GetStreamAsync(url);
                    stream.CopyTo(fileStream);
                }
                await context.Bot.Client.CurrentUser.ModifyAsync(bot => bot.Avatar = new Image("avatar.png"));
                await context.Message.Channel.SendFileAsync("avatar.png", "Updated avatar successfully");

                await context.Message.RemoveReactionAsync(CommandTools.LoadingEmote, context.Bot.Client.CurrentUser);
                return new SuccessResult();
            }
            catch (Exception ex)
            {
                return new ErrorResult(ex);
            }
        }

        [Command("delete"), HelpText("Deletes a specified number of messages (up to 99)"), Permissions(channelPermissions: new[] { ChannelPermission.ManageMessages })]
        public static async Task<ICommandResult> Delete(DiscordUserMessageContext context, [HelpText("The number of message to delete (up to 99)"), JoinRemainingParameters] byte number)
        {
            if (number >= 100)
            {
                return new ErrorResult("The maximum number of messages to delete is 99 (plus the command message)");
            }

            var toDelete = (await context.Channel.GetMessagesAsync(number + 1).FlattenAsync()).ToArray();

            var bulkDelete = toDelete.Where(msg => msg.Timestamp + TimeSpan.FromDays(14) > DateTimeOffset.Now);
            var remaining = toDelete.Except(bulkDelete);

            await ((ITextChannel)context.Channel).DeleteMessagesAsync(bulkDelete);
            foreach (IMessage msg in remaining)
            {
                await msg.DeleteAsync();
            }

            return new SuccessResult();
        }

        [Command("kill"), HelpText("Kills the bot"), Permissions(ownerOnly: true)]
        public static async void Kill(DiscordUserMessageContext context)
        {
            await context.Reply("Why??? Why do you do this to meeeeeeeeeeeeeeeeeeeeeeeeeeeee");
            context.Bot.Settings.StartupReplyChannel = context.Channel.Id;
            context.Bot.Settings.SaveConfig();
            Environment.Exit(0);
        }

        [Command("game"), HelpText("Sets the current game for the bot"), Permissions(ownerOnly: true)]
        public static async Task<ICommandResult> Game(DiscordUserMessageContext context, [JoinRemainingParameters] string game)
        {
            await context.Bot.Client.SetGameAsync(game);
            context.Bot.Settings.Game = game;
            context.Bot.Settings.SaveConfig();
            return new SuccessResult("Game updated");
        }

        [Command("file"), HelpText("Gets, puts, or lists bot settings files"), Permissions(ownerOnly: true)]
        public static async Task<ICommandResult> File(DiscordUserMessageContext context, FileAction action, string filename = null)
        {
            switch (action)
            {
                case FileAction.Get:
                    if (filename == null) return new ErrorResult("Please provide a filename");
                    if (!context.Bot.FileNames.Contains(filename)) return new ErrorResult("The provided filename was not a valid option");
                    await context.Channel.SendFileAsync(Config.BasePath + filename);
                    return new SuccessResult();
                case FileAction.Put:
                    if (filename == null) return new ErrorResult("Please provide a filename");
                    if (!context.Bot.FileNames.Contains(filename)) return new ErrorResult("The provided filename was not a valid option");
                    if (context.Message.Attachments.Count == 0) return new ErrorResult("Please attach a file with your message");
                    var attachment = context.Message.Attachments.First();
                    using (var fileStream = System.IO.File.Create(Config.BasePath + filename))
                    {
                        await (await new HttpClient().GetStreamAsync(attachment.Url)).CopyToAsync(fileStream);
                    }
                    return new SuccessResult("File successfully replaced");
                case FileAction.List:
                    return new SuccessResult($"```\nAvailable Files:\n\n{string.Join("\n", context.Bot.FileNames)}\n```");
                default:
                    return new ErrorResult("Unknown option");
            }
        }

        [Command("grammar"), HelpText("Enables or disables grammar bot"), Permissions(ownerOnly: true)]
        public static async Task<ICommandResult> Grammar(DiscordUserMessageContext context, GrammarAction command)
        {
            switch (command)
            {
                case GrammarAction.Enable:
                    await context.Bot.Grammar.Start();
                    break;
                case GrammarAction.Disable:
                    await context.Bot.Grammar.Stop();
                    break;
                default:
                    return new ErrorResult("Not an option");
            }

            return new SuccessResult();
        }

        public enum FileAction
        {
            [HelpText("Gets the file with the provided name")] Get,
            [HelpText("Replaces the file with the provided name with the provided file")] Put,
            [HelpText("Lists all files that can be accessed")] List
        }

        public enum GrammarAction
        {
            [HelpText("Enables the grammar police bot")] Enable,
            [HelpText("Disables the grammar police bot")] Disable
        }
    }
}
