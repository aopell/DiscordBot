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
                await context.Bot.Client.CurrentUser.ModifyAsync(async bot => bot.Avatar = new Image(await new HttpClient().GetStreamAsync(url)));
                return new SuccessResult("Avatar changed successfully!");
            }
            catch (Exception ex)
            {
                return new ErrorResult(ex);
            }
        }

        [Command("delete"), HelpText("Deletes a specified number of messages (up to 99)"), Permissions(channelPermissions: new[] { ChannelPermission.ManageMessages })]
        public static async Task<ICommandResult> Delete(DiscordUserMessageContext context, [HelpText("The number of message to delete (up to 99)")] byte number)
        {
            if (number >= 100)
            {
                return new ErrorResult("The maximum number of messages to delete is 99 (plus the command message)");
            }

            var toDelete = (await context.Channel.GetMessagesAsync(number + 1).Flatten()).ToArray();

            var bulkDelete = toDelete.Where(msg => msg.Timestamp + TimeSpan.FromDays(14) > DateTimeOffset.Now);
            var remaining = toDelete.Except(bulkDelete);

            await context.Channel.DeleteMessagesAsync(bulkDelete);
            foreach (IMessage msg in remaining)
            {
                await msg.DeleteAsync();
            }

            return new SuccessResult();
        }

        [Command("kill"), HelpText("Kills the bot"), Permissions(ownerOnly: true)]
        public static void Kill(DiscordUserMessageContext context) => Environment.Exit(0);
    }
}
