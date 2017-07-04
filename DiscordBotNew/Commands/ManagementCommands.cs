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
        public static async Task ChangeUsername(SocketMessage message, [JoinRemainingParameters] string username)
        {
            if (username.Length > 32)
            {
                await message.ReplyError($"Username must be no more than 32 characters in length. ({username.Length} > 32)");
                return;
            }

            await DiscordBot.Client.CurrentUser.ModifyAsync(bot => bot.Username = username);
            await message.Reply("Changed username successfully");
        }

        [Command("avatar"), HelpText("Changes the bot's avatar image"), Permissions(ownerOnly: true)]
        public static async Task ChangeAvatar(SocketMessage message, [JoinRemainingParameters] string url)
        {
            try
            {
                await DiscordBot.Client.CurrentUser.ModifyAsync(async bot => bot.Avatar = new Image(await new HttpClient().GetStreamAsync(url)));
                await message.Reply("Avatar changed successfully!");
            }
            catch (Exception ex)
            {
                await message.ReplyError(ex);
            }
        }

        [Command("delete"), HelpText("Deletes a specified number of messages (up to 99)"), Permissions(channelPermissions: new[] { ChannelPermission.ManageMessages })]
        public static async Task Delete(SocketMessage message, byte number)
        {
            if (number >= 100)
            {
                await message.ReplyError("The maximum number of messages to delete is 99 (plus the command message)");
                return;
            }
            await message.Channel.DeleteMessagesAsync(await message.Channel.GetMessagesAsync(number + 1).Flatten());
        }
    }
}
