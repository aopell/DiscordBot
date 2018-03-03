using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DiscordBotNew.CommandLoader;
using DiscordBotNew.CommandLoader.CommandContext;
using DiscordBotNew.CommandLoader.CommandResult;

namespace DiscordBotNew.Commands
{
    public static class ChannelDescriptionCommands
    {
        [Command("description"), HelpText("Sets a dynamic channel description for the current channel"), CommandScope(ChannelType.Text), Permissions(false, new[] { ChannelPermission.ManageChannel })]
        public static async Task<ICommandResult> Description(DiscordUserMessageContext context, ChannelDescriptionAction action, [HelpText("The dynamic description text, surround commands in {{ }}"), JoinRemainingParameters] string text = "")
        {
            var channelDescriptions = context.Bot.ChannelDescriptions.Descriptions ?? new Dictionary<ulong, string>();

            var textChannel = (ITextChannel)context.Channel;

            switch (action)
            {
                case ChannelDescriptionAction.Get:
                    if (channelDescriptions.ContainsKey(textChannel.Id))
                        return new SuccessResult($"```{channelDescriptions[textChannel.Id].Replace("```", "`​`​`​")}```");
                    else
                        return new ErrorResult("This channel does not have a dynamic description");
                case ChannelDescriptionAction.Set:
                    await textChannel.ModifyAsync(x => x.Topic = "Loading dynamic description...");
                    if (channelDescriptions.ContainsKey(textChannel.Id))
                    {
                        channelDescriptions[textChannel.Id] = text;
                    }
                    else
                    {
                        channelDescriptions.Add(textChannel.Id, text);
                    }

                    context.Bot.ChannelDescriptions.Descriptions = channelDescriptions;
                    context.Bot.ChannelDescriptions.SaveConfig();
                    return new SuccessResult("Description updated sucessfully");
                case ChannelDescriptionAction.Remove:
                    if (channelDescriptions.ContainsKey(textChannel.Id))
                    {
                        await textChannel.ModifyAsync(x => x.Topic = "");
                        channelDescriptions.Remove(textChannel.Id);
                        context.Bot.ChannelDescriptions.Descriptions = channelDescriptions;
                        context.Bot.ChannelDescriptions.SaveConfig();
                        return new SuccessResult("Description removed sucessfully");
                    }
                    else
                    {
                        return new ErrorResult("This channel does not have a dynamic description");
                    }
                default:
                    return new ErrorResult("That option doesn't exist (not really sure how you got here)");
            }
        }

        public enum ChannelDescriptionAction
        {
            [HelpText("Gets the current dynamic description text")] Get,
            [HelpText("Sets the dynamic text of the channel description")] Set,
            [HelpText("Prevents the channel description from updating dynamically")] Remove,
            [HelpText("Alias for remove")] Delete = Remove
        }
    }
}
