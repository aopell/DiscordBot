using Discord;
using DiscordBotNew.CommandLoader;
using DiscordBotNew.CommandLoader.CommandContext;
using DiscordBotNew.CommandLoader.CommandResult;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBotNew.Commands
{
    public static class GithubCommands
    {
        [Command("linkrepo", "linkgithub", "githublink"), HelpText("Links a GitHub repository to the current server; enables GitHub helper"), CommandScope(ChannelType.Text), Permissions(guildPermissions: new[] { GuildPermission.Administrator })]
        public static async Task<ICommandResult> LinkRepository(DiscordUserMessageContext context, string repository)
        {
            if (context.Guild == null) return new ErrorResult("Must be in a guild");
            if (!Regex.IsMatch(repository, @"^[a-zA-Z\d](?:[a-zA-Z\d]|-(?=[a-zA-Z\d])){0,38}/[\w.-]+$"))
            {
                return new ErrorResult("Repository name was not in the correct format. Please use the format `user/repository` or `organization/repository`");
            }
            else if ((await new HttpClient().GetAsync($"https://github.com/{repository}")).StatusCode == HttpStatusCode.NotFound)
            {
                return new ErrorResult("That repository was not found. This feature currently does not support private repositories, sorry for the inconvenience.");
            }

            context.Bot.GithubRepos.Repositories[context.Guild.Id] = repository;
            context.Bot.GithubRepos.SaveConfig();
            return new SuccessResult("Repository linked successfully. GitHub helper has been enabled. Run the `githubhelper` command for more information.");
        }

        [Command("unlinkrepo", "unlinkgithub", "githubunlink"), HelpText("Unlinks a GitHub repository from the current server; disables GitHub helper"), Permissions(guildPermissions: new[] { GuildPermission.Administrator })]
        public static ICommandResult UnlinkRepository(DiscordUserMessageContext context)
        {
            if (context.Guild == null) return new ErrorResult("Must be in a guild");
            context.Bot.GithubRepos.Repositories[context.Guild.Id] = null;
            context.Bot.GithubRepos.SaveConfig();
            return new SuccessResult("Repository unlinked successfully");
        }

        [Command("githubhelper"), HelpText("Shows help information for GitHub helper"), CommandScope(ChannelType.Text)]
        public static ICommandResult GithubHelper(DiscordUserMessageContext context)
        {
            if (context.Guild == null) return new ErrorResult("Must be in a guild");
            return new SuccessResult($"**GitHub Helper**\nLinked Repo: {(context.Bot.GithubRepos.Repositories.ContainsKey(context.Guild.Id) && context.Bot.GithubRepos.Repositories[context.Guild.Id] != null ? $"https://github.com/{context.Bot.GithubRepos.Repositories[context.Guild.Id]}" : "None")}");
        }
    }

    public static class GithubHelper
    {
        private static string MDLink(string text, string url)
        {
            return $"[{text}]({url})";
        }

        public static async Task Run(DiscordUserMessageContext context, string username, string token, string repository)
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Token {token}");
                client.DefaultRequestHeaders.Add("User-Agent", "Netcat Discord Bot");
                string IssueBaseUrl = $"https://api.github.com/repos/{repository}/issues";

                var issues = Regex.Matches(context.Message.Content, @"\bGH#([1-9]\d*)\b").Select(x => int.Parse(x.Groups[1].Value));
                var users = Regex.Matches(context.Message.Content, @"\bGH@([a-zA-Z\d](?:[a-zA-Z\d]|-(?=[a-zA-Z\d])){0,38})\b").Select(x => x.Groups[1].Value);

                foreach (int issue in issues)
                {
                    try
                    {
                        var response = await client.GetStringAsync($"{IssueBaseUrl}/{issue}");
                        var github_issue = JsonConvert.DeserializeObject<GithubIssue>(response);
                        var embed = new EmbedBuilder()
                            .WithTitle($"{github_issue.title} #{github_issue.number}")
                            .WithAuthor(github_issue.user.login, github_issue.user.avatar_url, github_issue.user.html_url)
                            .WithDescription(github_issue.body.Length > 1000 ? $"{github_issue.body.Substring(0, 1000)}..." : github_issue.body)
                            .WithUrl(github_issue.html_url)
                            .WithColor(github_issue.state == GithubIssueStatus.open ? Color.Green : Color.Red)
                            .WithFooter(github_issue.state.ToString()[0].ToString().ToUpper() + github_issue.state.ToString().Substring(1) + (github_issue.pull_request != null ? " Pull Request" : " Issue"))
                            .WithTimestamp(github_issue.created_at);

                        if (github_issue.labels != null && github_issue.labels.Length > 0)
                        {
                            embed.AddField("Labels", string.Join(", ", github_issue.labels.Select(x => x.name)), inline: true);
                        }
                        if (github_issue.milestone != null)
                        {
                            embed.AddField("Milestone", MDLink(github_issue.milestone.title, github_issue.milestone.html_url), inline: true);
                        }
                        if (github_issue.assignees != null && github_issue.assignees.Length > 0)
                        {
                            embed.AddField("Assignees", string.Join(", ", github_issue.assignees.Select(x => MDLink(x.login, x.html_url))), inline: true);
                        }
                        await context.Reply("", embed: embed.Build());
                    }
                    catch (HttpRequestException ex)
                    {
                        await context.ReplyError(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                await context.ReplyError(ex);
            }
        }
    }

    public class GithubIssue
    {
        public string html_url { get; set; }
        public int number { get; set; }
        public string title { get; set; }
        public GithubIssueStatus state { get; set; }
        public string body { get; set; }
        public int comments { get; set; }
        public DateTimeOffset created_at { get; set; }
        public GithubUser user { get; set; }
        public GithubMilestone milestone { get; set; }
        public GithubLabel[] labels { get; set; }
        public GithubUser[] assignees { get; set; }
        public GithubPullRequest pull_request { get; set; }
    }
    public class GithubUser
    {
        public string login { get; set; }
        public string html_url { get; set; }
        public string avatar_url { get; set; }
    }
    public class GithubLabel
    {
        public string name { get; set; }
    }
    public class GithubPullRequest
    {
        public string html_url { get; set; }
    }
    public class GithubMilestone
    {
        public string html_url { get; set; }
        public string title { get; set; }
        public int open_issues { get; set; }
        public int closed_issues { get; set; }
        public GithubIssueStatus state { get; set; }
        public DateTimeOffset? due_on { get; set; }
    }
    public enum GithubIssueStatus
    {
        open,
        closed
    }
}
