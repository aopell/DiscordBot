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
        public static async Task<ICommandResult> ChangeUsername(SocketMessage message, [JoinRemainingParameters] string username)
        {
            if (username.Length > 32)
            {
                return new ErrorResult($"Username must be no more than 32 characters in length. ({username.Length} > 32)");
            }

            await DiscordBot.Client.CurrentUser.ModifyAsync(bot => bot.Username = username);
            return new SuccessResult("Changed username successfully");
        }

        [Command("avatar"), HelpText("Changes the bot's avatar image"), Permissions(ownerOnly: true)]
        public static async Task<ICommandResult> ChangeAvatar(SocketMessage message, [JoinRemainingParameters] string url)
        {
            try
            {
                await DiscordBot.Client.CurrentUser.ModifyAsync(async bot => bot.Avatar = new Image(await new HttpClient().GetStreamAsync(url)));
                return new SuccessResult("Avatar changed successfully!");
            }
            catch (Exception ex)
            {
                return new ErrorResult(ex);
            }
        }

        [Command("delete"), HelpText("Deletes a specified number of messages (up to 99)"), Permissions(channelPermissions: new[] { ChannelPermission.ManageMessages })]
        public static async Task<ICommandResult> Delete(SocketMessage message, byte number)
        {
            if (number >= 100)
            {
                return new ErrorResult("The maximum number of messages to delete is 99 (plus the command message)");
            }

            var toDelete = (await message.Channel.GetMessagesAsync(number + 1).Flatten()).ToArray();

            var bulkDelete = toDelete.Where(msg => msg.Timestamp + TimeSpan.FromDays(14) > DateTimeOffset.Now);
            var remaining = toDelete.Except(bulkDelete);

            await message.Channel.DeleteMessagesAsync(bulkDelete);
            foreach (IMessage msg in remaining)
            {
                await msg.DeleteAsync();
            }

            return new SuccessResult();
        }

        [Command("kill"), HelpText("Kills the bot"), Permissions(ownerOnly: true)]
        public static void Kill(SocketMessage message) => Environment.Exit(0);
    }
}
