using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using DiscordBotNew.CommandLoader.CommandContext;
using DiscordBotNew.CommandLoader.CommandResult;

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
                                            .Where(method => method.IsDefined(typeof(CommandAttribute)))
                                            .OrderBy(method => method.GetCustomAttribute<CommandAttribute>().Names[0])
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

        public static async Task<ICommandResult> RunTimer(string commandMessage, ICommandContext context, string prefix, bool awaitResult, ulong tick)
        {
            var args = commandMessage.Trim().Substring(prefix.Length).Split(' ');
            string commandName = args[0];
            args = Regex.Matches(string.Join(" ", args.Skip(1)), @"[\""].+?[\""]|[^ ]+")
                        .Cast<Match>()
                        .Select(m => m.Value)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Select(text => text.StartsWith("\"") && text.EndsWith("\"") ? text.Substring(1, text.Length - 2) : text)
                        .ToArray();
            MethodInfo command = GetCommand(commandName, args.Length);

            if (tick % (command.GetCustomAttribute<ChannelDescriptionDelayAttribute>()?.DelaySeconds ?? 10) == 0)
            {
                return await Run(commandMessage, context, prefix, awaitResult);
            }

            return null;
        }

        public static async Task<ICommandResult> Run(string commandMessage, ICommandContext context, string prefix, bool awaitResult)
        {
            var args = commandMessage.Trim().Substring(prefix.Length).Split(' ');
            string commandName = args[0];
            args = CommandTools.ParseArguments(string.Join(" ", args.Skip(1)));
            MethodInfo command = GetCommand(commandName, args.Length);

            await DiscordBot.Log(context.LogMessage(commandName));

            if (command == null)
            {
                var commands = GetCommands(commandName);
                if (commands != null && commands.Any())
                {
                    string msg = $"The syntax of the command was incorrect. The following parameters are required: `{string.Join("`, `", commands.First().GetParameters().Where(param => !param.IsOptional).Select(param => param.GetCustomAttribute<DisplayNameAttribute>()?.Name ?? param.Name).Skip(args.Length + 1))}`\nUse `!help {prefix}{commandName}` for command info";
                    await context.ReplyError(msg, "Syntax Error");
                    return new ErrorResult(msg, "Syntax Error");
                }

                string message = $"The command `{prefix}{commandName}` does not exist";
                await context.ReplyError(message, "Command Not Found");
                return new ErrorResult(message, "Command Not Found");
            }

            var parameters = command.GetParameters();
            var requiredParameters = parameters.Where(param => !param.IsOptional).ToArray();

            Type contextType = context.GetType();
            if (!parameters[0].ParameterType.IsAssignableFrom(contextType))
            {
                string message = $"That command is not valid in the context {context.GetType().Name}";
                await context.ReplyError(message, "Invalid Context");
                return new ErrorResult(message, "Invalid Context");
            }

            if (args.Length < requiredParameters.Length - 1)
            {
                string message = $"The syntax of the command was incorrect. The following parameters are required: `{string.Join("`, `", requiredParameters.Select(param => param.GetCustomAttribute<DisplayNameAttribute>()?.Name ?? param.Name).Skip(args.Length + 1))}`\nUse `{prefix}help {commandName}` for command info";
                await context.ReplyError(message, "Syntax Error");
                return new ErrorResult(message, "Syntax Error");
            }

            if (context is DiscordMessageContext discordContext)
            {
                if (!(command.GetCustomAttribute<CommandScopeAttribute>()?.ChannelTypes.Contains(discordContext.ChannelType) ?? true))
                {
                    string message = $"The command `{prefix}{commandName}` is not valid in the scope {discordContext.ChannelType}";
                    await context.ReplyError(message, "Scope Error");
                    return new ErrorResult(message, "Scope Error");
                }

                string permissionError = command.GetCustomAttribute<PermissionsAttribute>()?.GetPermissionError(discordContext);
                if (permissionError != null)
                {
                    await context.ReplyError(permissionError, "Permission Error");
                    return new ErrorResult(permissionError, "Permission Error");
                }
            }

            var values = new List<object>
            {
                context
            };

            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].IsOptional && i > args.Length)
                {
                    values.Add(Type.Missing);
                    continue;
                }

                if (i == parameters.Length - 1 && parameters[i].IsDefined(typeof(JoinRemainingParametersAttribute)))
                {
                    if (parameters[i].ParameterType == typeof(string))
                    {
                        values.Add(string.Join(" ", args));
                    }
                    else if (parameters[i].ParameterType == typeof(string[]))
                    {
                        values.Add(args.Skip(i - 1).ToArray());
                    }
                    else
                    {
                        object converted = ConvertToType(parameters[i].ParameterType, string.Join(" ", args.Skip(i - 1)));
                        if (converted == null)
                        {
                            return await ThrowTypeError(context, string.Join(" ", args.Skip(i - 1)), parameters[i]);
                        }
                        values.Add(converted);
                    }
                    break;
                }

                var result = ConvertToType(parameters[i].ParameterType, args[i - 1]);
                if (result == null)
                {
                    return await ThrowTypeError(context, args[i - 1], parameters[i]);
                }

                values.Add(result);
            }

            if (awaitResult)
                return await RunCommand(context, command, values.ToArray());

            RunCommand(context, command, values.ToArray());
            return new SuccessResult();
        }

        private static async Task<ICommandResult> ThrowTypeError(ICommandContext context, string value, ParameterInfo parameter)
        {
            string message = $"The value `{value}` of parameter `{parameter.Name}` should be type `{Nullable.GetUnderlyingType(parameter.ParameterType)?.Name ?? parameter.ParameterType.Name}`";
            string title = "Argument Type Error";
            await context.ReplyError(message, title);
            return new ErrorResult(message, title);
        }

        private static async Task<ICommandResult> RunCommand(ICommandContext context, MethodInfo command, object[] parameters)
        {
            try
            {
                var result = command.Invoke(null, parameters);

                if (result == null) return new SuccessResult();

                if (!result.GetType().IsGenericType && result is Task)
                {
                    await (Task)result;
                    return new SuccessResult();
                }

                ICommandResult commandResult = result as ICommandResult ?? await (Task<ICommandResult>)result;

                switch (commandResult)
                {
                    case SuccessResult successResult:
                        if (successResult.HasContent)
                        {
                            switch (context)
                            {
                                case DiscordMessageContext messageContext:
                                    await messageContext.Reply(successResult.Message, successResult.IsTTS, successResult.Embed, successResult.Options);
                                    break;
                                default:
                                    await context.Reply(successResult.Message);
                                    break;
                            }
                        }
                        break;
                    case ErrorResult errorResult:
                        await context.ReplyError(errorResult.Message, errorResult.Title);
                        break;
                    default:
                        await context.Reply(commandResult.Message);
                        break;
                }

                return commandResult;
            }
            catch (Exception ex)
            {
                await context.ReplyError(ex);
                return new ErrorResult(ex);
            }
        }

        private static object ConvertToType(Type type, string text)
        {
            try
            {
                if (type.IsEnum)
                    return Enum.Parse(type, text, true);
                if (type == typeof(string))
                    return text;
                Type underlyingType;
                if ((underlyingType = Nullable.GetUnderlyingType(type)) != null)
                {
                    MethodInfo parse = underlyingType.GetMethod("Parse", new[] { typeof(string) });
                    return parse?.Invoke(null, new object[] { text });
                }

                MethodInfo parseMethod = type.GetMethod("Parse", new[] { typeof(string) });
                return parseMethod?.Invoke(null, new object[] { text });
            }
            catch
            {
                return null;
            }
        }

        [Command("help", "man"), HelpText("Gets help text for all commands or a specific command")]
        public static ICommandResult Help(DiscordMessageContext context, [HelpText("Gets help for this specific command, or all commands if set to '*'")]string command = null, [HelpText("Whether or not to show parameter descriptions")] Verbosity verbosity = Verbosity.Standard)
        {
            string commandPrefix = CommandTools.GetCommandPrefix(context, context.Channel);

            var builder = new EmbedBuilder
            {
                Title = "Help",
                Color = new Color(33, 150, 243)
            };

            var commands = command == null || command == "*"
                           ? commandMethods.Where(method => method.GetCustomAttribute<CommandScopeAttribute>()
                                                                  ?.ChannelTypes.Contains(context.ChannelType)
                                                                  ?? true)
                                           .Where(method => method.GetCustomAttribute<PermissionsAttribute>()?.GetPermissionError(context) == null)
                                           .ToList()
                           : GetCommands(command.StartsWith(commandPrefix)
                                                ? command.Substring(commandPrefix.Length)
                                                : command)
                           .ToList();

            if (commands[0] == null)
            {
                return new ErrorResult($"The requested command {commandPrefix}{command} was not found");
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
                                                                     ? $"[{param.GetCustomAttribute<DisplayNameAttribute>()?.Name ?? param.Name}{(param.IsDefined(typeof(JoinRemainingParametersAttribute)) ? "..." : "")}{(param.DefaultValue == null ? "" : $" = {param.DefaultValue}")}]"
                                                                     : $"<{param.GetCustomAttribute<DisplayNameAttribute>()?.Name ?? param.Name}{(param.IsDefined(typeof(JoinRemainingParametersAttribute)) ? "..." : "")}>")));
                title.Append("`");
                var text = new StringBuilder();
                text.AppendLine(method.GetCustomAttribute<HelpTextAttribute>()?.Text ?? "*No help text found*");
                if (verbosity == Verbosity.Verbose)
                {
                    if (method.GetParameters().Length > 1)
                    {
                        text.AppendLine("**Parameters**");
                    }

                    foreach (var parameter in method.GetParameters().Skip(1))
                    {
                        text.Append("`");

                        text.Append(parameter.IsOptional ? "Optional " : "");
                        if (parameter.ParameterType.IsGenericType)
                        {
                            text.Append(parameter.ParameterType.GetGenericTypeDefinition()?.Name.Split('`')[0] ?? "UnknownGenericType");
                            text.Append(" ");
                            text.Append(parameter.ParameterType.GetGenericArguments()[0].Name);
                        }
                        else
                        {
                            text.Append(parameter.ParameterType.Name);
                        }
                        text.Append(" ");
                        text.Append(parameter.GetCustomAttribute<DisplayNameAttribute>()?.Name ?? parameter.Name);
                        text.Append(parameter.IsDefined(typeof(JoinRemainingParametersAttribute)) ? "..." : "");


                        if (parameter.IsOptional && parameter.DefaultValue != null)
                        {
                            text.Append($" = {parameter.DefaultValue}");
                        }

                        text.Append("`");
                        var helpText = parameter.GetCustomAttribute<HelpTextAttribute>();
                        if (helpText != null)
                        {
                            text.Append(": ");
                            text.Append(helpText.Text);
                        }

                        text.AppendLine();
                    }

                    foreach (var parameter in method.GetParameters().Where(param => param.ParameterType.IsEnum))
                    {
                        text.AppendLine($"**{parameter.ParameterType.Name} Options**");
                        var names = Enum.GetNames(parameter.ParameterType);
                        foreach (string name in names)
                        {
                            text.Append($"`{name}`");
                            var helpText = parameter.ParameterType.GetMember(name).FirstOrDefault().GetCustomAttribute<HelpTextAttribute>();
                            if (helpText != null)
                            {
                                text.Append($": {helpText.Text}");
                            }
                            text.AppendLine();
                        }
                    }
                }
                builder.AddField(title.ToString(), text.ToString());
            }


            return new SuccessResult(embed: builder);
        }

        public enum Verbosity
        {
            [HelpText("Shows the command, its help text, and its parameters")] Standard,
            [HelpText("Shows help text for individual parameters")] Verbose
        }
    }
}
