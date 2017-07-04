using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;

namespace DiscordBotNew.CommandLoader
{
    public static class CommandRunner
    {
        private static List<MethodInfo> commandMethods;
        public static void LoadCommands()
        {
            commandMethods = typeof(DiscordBot).Assembly
                                            .GetTypes()
                                            .Where(type => type.IsAbstract && type.IsSealed) // Static types only
                                            .SelectMany(type => type.GetMethods())
                                            .Where(method => method.GetCustomAttribute<CommandAttribute>() != null)
                                            .ToList();
        }

        private static MethodInfo GetCommand(string name, int parameters) => commandMethods.Where(method => method.GetCustomAttribute<CommandAttribute>()
                                                                                           .Names.Contains(name.ToLower()))
                                                                                                 .OrderByDescending(method => method.GetParameters().Count(param => !param.IsOptional))
                                                                                                 .FirstOrDefault(method => method.GetParameters()
                                                                                                                                .Count(param => !param.IsOptional) - 1 <= parameters);
        private static IEnumerable<MethodInfo> GetCommands(string name) => commandMethods.Where(method => method.GetCustomAttribute<CommandAttribute>()
                                                                                                                .Names.Contains(name.ToLower()))
                                                                                                                .OrderBy(method => method.GetParameters().Count(param => !param.IsOptional));

        public static async Task Run(SocketMessage message, string prefix)
        {
            var args = message.Content.Substring(prefix.Length).Split(' ');
            string commandName = args[0];
            args = Regex.Matches(string.Join(" ", args.Skip(1)), @"[\""].+?[\""]|[^ ]+")
                        .Cast<Match>()
                        .Select(m => m.Value)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Select(text => text.StartsWith("\"") && text.EndsWith("\"") ? text.Substring(1, text.Length - 2) : text)
                        .ToArray();
            MethodInfo command = GetCommand(commandName, args.Length);

            await DiscordBot.Log(new LogMessage(LogSeverity.Info, "Command", $"@{message.Author.Username}#{message.Author.Discriminator} in {(message.Channel as IGuildChannel)?.Guild.Name ?? "DM"} #{message.Channel.Name}: [{commandName}] {message.Content}"));

            if (command == null)
            {
                await message.ReplyError($"The command `{prefix}{commandName}` does not exist", "Command Not Found");
                return;
            }

            if (!(command.GetCustomAttribute<CommandScopeAttribute>()?.ChannelTypes.Contains(message.GetChannelType()) ?? true))
            {
                await message.ReplyError($"The command `{prefix}{commandName}` is not valid in the scope {message.GetChannelType()}", "Scope Error");
                return;
            }

            string permissionError = command.GetCustomAttribute<PermissionsAttribute>()?.GetPermissionError(message);
            if (permissionError != null)
            {
                await message.ReplyError(permissionError, "Permission Error");
                return;
            }

            var parameters = command.GetParameters();
            var requiredParameters = parameters.Where(param => !param.IsOptional).ToArray();

            if (args.Length < requiredParameters.Length - 1)
            {
                await message.ReplyError($"The syntax of the command was incorrect. The following parameters are required: `{string.Join("`, `", requiredParameters.Select(param => param.GetCustomAttribute<HelpTextAttribute>()?.Text ?? param.Name).Skip(args.Length + 1))}`\nUse `!help {prefix}{commandName}` for command info", "Syntax Error");
                return;
            }

            var values = new List<object>
            {
                message
            };

            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].IsOptional && i > args.Length)
                {
                    values.Add(Type.Missing);
                    continue;
                }

                if (i == parameters.Length - 1 && parameters[i].GetCustomAttribute<JoinRemainingParametersAttribute>() != null)
                {
                    if (parameters[i].ParameterType == typeof(string))
                        values.Add(string.Join(" ", args.Skip(i - 1)));
                    else if (parameters[i].ParameterType == typeof(string[]))
                        values.Add(args.Skip(i - 1).ToArray());
                    else
                        values.Add(null);
                    break;
                }

                var result = ConvertToType(parameters[i].ParameterType, args[i - 1]);
                if (result == null)
                {
                    await message.ReplyError($"The value `{args[i - 1]}` of parameter `{parameters[i].Name}` should be type `{parameters[i].ParameterType.Name}`", "Argument Type Error");
                    return;
                }

                values.Add(result);
            }

            try
            {
                command.Invoke(null, values.ToArray());
            }
            catch (Exception ex)
            {
                await message.ReplyError(ex);
            }
        }

        private static object ConvertToType(Type type, string text)
        {
            try
            {
                if (type.IsEnum)
                    return Enum.Parse(type, text);
                if (type == typeof(string))
                    return text;

                MethodInfo parseMethod = type.GetMethod("Parse", new[] { typeof(string) });
                return parseMethod?.Invoke(null, new object[] { text });
            }
            catch
            {
                return null;
            }
        }

        [Command("help", "man"), HelpText("Gets help text for all commands or a specific command")]
        public static async Task Help(SocketMessage message, string command = null)
        {
            string commandPrefix = CommandTools.GetCommandPrefix(message.Channel);

            var builder = new EmbedBuilder
            {
                Title = "Help",
                Color = new Color(33, 150, 243)
            };

            var commands = command == null
                           ? commandMethods.Where(method => method.GetCustomAttribute<CommandScopeAttribute>()
                                                                  ?.ChannelTypes.Contains(message.GetChannelType())
                                                                  ?? true)
                                           .Where(method => method.GetCustomAttribute<PermissionsAttribute>()?.GetPermissionError(message) == null)
                                           .ToList()
                           : GetCommands(command.StartsWith(commandPrefix)
                           ? command.Substring(commandPrefix.Length)
                           : command)
                           .ToList();

            if (commands[0] == null)
            {
                await message.ReplyError($"The requested command {commandPrefix}{command} was not found");
                return;
            }

            foreach (MethodInfo method in commands)
            {
                var title = new StringBuilder();
                title.Append("`");
                title.Append(string.Join("|", method.GetCustomAttribute<CommandAttribute>().Names.Select(name => $"{commandPrefix}{name}")));
                title.Append(" ");
                title.Append(string.Join(" ", method.GetParameters()
                                                    .Skip(1)
                                                    .Select(param => param.IsOptional
                                                                     ? $"[{param.GetCustomAttribute<HelpTextAttribute>()?.Text ?? param.Name}{(param.GetCustomAttribute<JoinRemainingParametersAttribute>() != null ? "..." : "")}]"
                                                                     : $"<{param.GetCustomAttribute<HelpTextAttribute>()?.Text ?? param.Name}{(param.GetCustomAttribute<JoinRemainingParametersAttribute>() != null ? "..." : "")}>")));
                title.Append("`");
                builder.AddField(title.ToString(), method.GetCustomAttribute<HelpTextAttribute>()?.Text ?? "");
            }


            await message.Reply("", embed: builder);
        }
    }
}
