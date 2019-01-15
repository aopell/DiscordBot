using Discord;
using DiscordBotNew.CommandLoader;
using DiscordBotNew.CommandLoader.CommandContext;
using DiscordBotNew.CommandLoader.CommandResult;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
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
            return new SuccessResult("Repository linked successfully. GitHub helper has been enabled. Run the `github` command for more information.");
        }

        [Command("unlinkrepo", "unlinkgithub", "githubunlink"), HelpText("Unlinks a GitHub repository from the current server; disables GitHub helper"), Permissions(guildPermissions: new[] { GuildPermission.Administrator })]
        public static ICommandResult UnlinkRepository(DiscordUserMessageContext context)
        {
            if (context.Guild == null) return new ErrorResult("Must be in a guild");
            context.Bot.GithubRepos.Repositories[context.Guild.Id] = null;
            context.Bot.GithubRepos.SaveConfig();
            return new SuccessResult("Repository unlinked successfully");
        }

        [Command("github"), HelpText("Shows help information for GitHub helper"), CommandScope(ChannelType.Text)]
        public static ICommandResult Github(DiscordUserMessageContext context)
        {
            if (context.Guild == null) return new ErrorResult("Must be in a guild");
            return new SuccessResult($"**GitHub Helper**" +
                                     $"\nLinked Repository: {(context.Bot.GithubRepos.Repositories.ContainsKey(context.Guild.Id) && context.Bot.GithubRepos.Repositories[context.Guild.Id] != null ? $"https://github.com/{context.Bot.GithubRepos.Repositories[context.Guild.Id]}" : "None")}" +
                                     $"\n\n**Commands**" +
                                     $"\nUse the `linkrepo` command to link a GitHub repository to this server" +
                                     $"\nUse the `unlinkrepo` command to unlink the currently linked repository" +
                                     $"\n\nOnce a repository is linked, you can use the following features of GitHub Helper:" +
                                     $"\n\n**Display Summaries of Issues, Users, and Commits**" +
                                     $"\nType `GH#NNN` anywhere in your message to display a summary of issue number NNN" +
                                     $"\nType `GH@[USER]` anywhere in your message to display a summary of GitHub user [USER]" +
                                     $"\nType `GH:[COMMIT SHA1]` anywhere in your message to display a summary of the commit with the hash [COMMIT SHA1]");
        }
    }

    public enum GithubType
    {
        Issue = 0,
        Issues = 0,
        Milestone = 1,
        Milestones = 1
    }

    public static class GithubHelper
    {
        private static string MDLink(string text, string url)
        {
            return $"[{text}]({url})";
        }

        private static string ProcessGithubMarkdown(string text, string repository)
        {
            // Link mentioned issues
            text = Regex.Replace(text, @"(^|\W)(#)([1-9]\d*)(\b)", match => match.Groups[1].Value + MDLink(match.Groups[2].Value + match.Groups[3].Value, $"https://github.com/{repository}/issues/{match.Groups[3].Value}") + match.Groups[4].Value);
            // Link mentioned users
            text = Regex.Replace(text, @"(^|\W)(@)([a-zA-Z\d](?:[a-zA-Z\d]|-(?=[a-zA-Z\d])){0,38})(\b)", match => match.Groups[1].Value + MDLink(match.Groups[2].Value + match.Groups[3].Value, $"https://github.com/{match.Groups[3].Value}") + match.Groups[4].Value);
            // Replace markdown checkboxes with emoji checkboxes
            text = text.Replace("- [ ]", "⬜");
            text = Regex.Replace(text, @"- \[[xX]\]", "✅");
            return text;
        }

        public static async Task Run(DiscordUserMessageContext context, string username, string token, string repository)
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Token {token}");
                client.DefaultRequestHeaders.Add("User-Agent", "Netcat Discord Bot");

                const string ApiBaseUrl = "https://api.github.com";
                string RepositoryBaseUrl = ApiBaseUrl + $"/repos/{repository}";
                string IssueBaseUrl = RepositoryBaseUrl + "/issues";
                string CommitBaseUrl = RepositoryBaseUrl + "/commits";
                string UserBaseUrl = ApiBaseUrl + "/users";

                var issues = Regex.Matches(context.Message.Content, @"\b[gG][hH]#([1-9]\d*)\b").Select(x => int.Parse(x.Groups[1].Value));
                var commits = Regex.Matches(context.Message.Content, @"\b[gG][hH]:([0-9a-fA-F]{40}|[0-9a-fA-F]{7})\b").Select(x => x.Groups[1].Value);
                var users = Regex.Matches(context.Message.Content, @"\b[gG][hH]@([a-zA-Z\d](?:[a-zA-Z\d]|-(?=[a-zA-Z\d])){0,38})\b").Select(x => x.Groups[1].Value);
                users = users.Concat(Regex.Matches(context.Message.Content, @"[gG][hH]<@(\d+)>").Select(x => context.Bot.Client.GetUser(ulong.Parse(x.Groups[1].Value)).Username));

                foreach (int issue in issues)
                {
                    try
                    {
                        var response = await client.GetStringAsync($"{IssueBaseUrl}/{issue}");
                        var github_issue = JsonConvert.DeserializeObject<GithubIssue>(response);
                        github_issue.body = ProcessGithubMarkdown(github_issue.body, repository);
                        var embed = new EmbedBuilder()
                            .WithTitle($"#{github_issue.number}: {github_issue.title}")
                            .WithAuthor(github_issue.user.login, github_issue.user.avatar_url, github_issue.user.html_url)
                            .WithDescription(github_issue.body.Length > 1950 ? $"{github_issue.body.Substring(0, 1950)}..." : github_issue.body)
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
                    catch (HttpRequestException) { }
                }

                foreach (string commit in commits)
                {
                    try
                    {
                        var response = await client.GetStringAsync($"{CommitBaseUrl}/{commit}");
                        var github_commit = JsonConvert.DeserializeObject<GithubCommit>(response);
                        string commit_body = ProcessGithubMarkdown(github_commit.commit.other_lines, repository);
                        var embed = new EmbedBuilder()
                            .WithTitle(github_commit.commit.first_line)
                            .WithAuthor(github_commit.author.login, github_commit.author.avatar_url, github_commit.author.html_url)
                            .WithDescription(commit_body.Length > 1950 ? $"{commit_body.Substring(0, 1950)}..." : commit_body)
                            .WithUrl(github_commit.html_url)
                            .WithColor(Color.LightGrey)
                            .WithFooter($"Commit {commit.Substring(0, 7)}")
                            .WithTimestamp(github_commit.commit.committer.date)
                            .AddField("Total Changes", github_commit.stats.ToString(), inline: true)
                            .AddField("Files Changed", string.Join('\n', github_commit.files.Select(x => x.ToString())), inline: true);
                        if (github_commit.commit.comment_count > 0)
                        {
                            embed.AddField("Comments", github_commit.commit.comment_count, inline: true);
                        }

                        await context.Reply("", embed: embed.Build());
                    }
                    catch (HttpRequestException) { }
                }

                foreach (string user in users)
                {
                    try
                    {
                        var response = await client.GetStringAsync($"{UserBaseUrl}/{user}");
                        var github_user = JsonConvert.DeserializeObject<GithubUserFull>(response);
                        var embed = new EmbedBuilder()
                            .WithAuthor(github_user.name == null ? github_user.login : $"{github_user.name} ({github_user.login})", github_user.avatar_url, github_user.html_url)
                            .WithDescription(github_user.bio == null  ? "" : github_user.bio.Length > 1950 ? $"{github_user.bio.Substring(0, 1950)}..." : github_user.bio)
                            .WithColor(Color.LightGrey)
                            .AddField("Followers", github_user.followers, inline: true)
                            .AddField("Following", github_user.following, inline: true)
                            .AddField("Public Repositories", github_user.public_repos, inline: true)
                            .AddField("Public Gists", github_user.public_gists, inline: true);

                        if (!string.IsNullOrEmpty(github_user.company))
                        {
                            embed.AddField("Company", github_user.company, inline: true);
                        }
                        if (!string.IsNullOrEmpty(github_user.blog))
                        {
                            embed.AddField("Website", MDLink(github_user.blog, github_user.blog), inline: true);
                        }
                        if (!string.IsNullOrEmpty(github_user.location))
                        {
                            embed.AddField("Location", github_user.location, inline: true);
                        }

                        await context.Reply("", embed: embed.Build());
                    }
                    catch (HttpRequestException) { }
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
    public class GithubUserFull
    {
        public string name { get; set; }
        public string login { get; set; }
        public string html_url { get; set; }
        public string avatar_url { get; set; }
        public string blog { get; set; }
        public string company { get; set; }
        public string bio { get; set; }
        public int public_repos { get; set; }
        public int public_gists { get; set; }
        public int followers { get; set; }
        public int following { get; set; }
        public string location { get; set; }
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
    public class GithubCommit
    {
        public string html_url { get; set; }
        public GithubUser author { get; set; }
        public GithubCommitObject commit { get; set; }
        public GithubCommitStats stats { get; set; }
        public GithubCommitStats[] files { get; set; }
    }
    public class GithubCommitObject
    {
        public GithubCommitAuthor author { get; set; }
        public GithubCommitAuthor committer { get; set; }
        public string message { get; set; }
        public int comment_count { get; set; }
        public string first_line => message.Split('\n')[0];
        public string other_lines => message.Contains('\n') ? message.Substring(message.IndexOf('\n')) : "";
    }
    public class GithubCommitAuthor
    {
        public string name { get; set; }
        public string email { get; set; }
        public DateTimeOffset date { get; set; }
    }
    public class GithubCommitStats
    {
        public int additions { get; set; }
        public int deletions { get; set; }
        public int total_changes => additions + deletions;
        public string filename { get; set; }

        public override string ToString() => $"{(filename == null ? "" : $"`{filename}` - ")}{total_changes} (+{additions}/-{deletions})";
    }
    public enum GithubIssueStatus
    {
        open,
        closed
    }
}
