using Discord;
using DiscordBotNew.CommandLoader;
using DiscordBotNew.CommandLoader.CommandContext;
using DiscordBotNew.CommandLoader.CommandResult;
using Octokit;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBotNew.Commands
{
    public static class GithubCommands
    {
        [Command("github"), HelpText("Runs various GitHub commands"), CommandScope(ChannelType.Text)]
        public static async Task<ICommandResult> Github(DiscordUserMessageContext context, GithubAction action = GithubAction.Info, string value = null)
        {
            if (context.Guild == null) return new ErrorResult("Must be in a guild");
            context.Bot.GithubRepos.Repositories.TryGetValue(context.Guild.Id, out string linkedRepository);
            string linkedRepoOwner = linkedRepository?.Split('/')[0];
            string linkedRepoName = linkedRepository?.Split('/')[1];
            var client = new GitHubClient(new ProductHeaderValue("Netcat-Discord-Bot"));
            client.Credentials = new Credentials(context.Bot.GithubRepos.Token);

            switch (action)
            {
                case GithubAction.Info:
                    return new SuccessResult($"**GitHub Helper**" +
                                             $"\nLinked Repository: {(linkedRepository != null ? $"https://github.com/{linkedRepository}" : "None")}" +
                                             $"\nUse the `github {(linkedRepository == null ? "" : "un")}link` command to {(linkedRepository == null ? "link a GitHub repository to this server" : "unlink the currently linked repository")}" +
                                             $"\n\nOnce a repository is linked, you can use the following features of GitHub Helper:" +
                                             $"\n\n**Commands**" +
                                             $"\nUse the `github milestone [MILESTONE]` command to display the status of a milestone" +
                                             $"\n\n**Display Summaries of Issues, Users, and Commits**" +
                                             $"\nType `GH#[NNN]` anywhere in your message to display a summary of issue number [NNN]" +
                                             $"\nType `GH@[USER]` anywhere in your message to display a summary of GitHub user [USER]" +
                                             $"\nType `GH:[COMMIT SHA1]` anywhere in your message to display a summary of the commit with the hash [COMMIT SHA1]");
                case GithubAction.Link:
                    if (!context.HasGuildPermissions(GuildPermission.Administrator))
                    {
                        return new ErrorResult("Must be an administrator to link/unlink a repository", "Permission error");
                    }
                    if (!Regex.IsMatch(value, @"^[a-zA-Z\d](?:[a-zA-Z\d]|-(?=[a-zA-Z\d])){0,38}/[\w.-]+$"))
                    {
                        return new ErrorResult("Repository name was not in the correct format. Please use the format `user/repository` or `organization/repository`");
                    }

                    try
                    {
                        await client.Repository.Get(value.Split('/')[0], value.Split('/')[1]);
                    }
                    catch (NotFoundException)
                    {
                        return new ErrorResult("That repository was not found. This feature currently does not support private repositories unless the GitHub user `Netcat-Bot` is a repository member, sorry for the inconvenience.");
                    }

                    context.Bot.GithubRepos.Repositories[context.Guild.Id] = value;
                    context.Bot.GithubRepos.SaveConfig();
                    return new SuccessResult("Repository linked successfully. GitHub helper has been enabled. Run the `github` command for more information.");
                case GithubAction.Unlink:
                    if (!context.HasGuildPermissions(GuildPermission.Administrator))
                    {
                        return new ErrorResult("Must be an administrator to link/unlink a repository", "Permission error");
                    }
                    context.Bot.GithubRepos.Repositories[context.Guild.Id] = null;
                    context.Bot.GithubRepos.SaveConfig();
                    return new SuccessResult("Repository unlinked successfully");
                case GithubAction.Milestone:
                    using (context.Channel.EnterTypingState())
                    {
                        var allIssuesInRepo = await client.Issue.GetAllForRepository(linkedRepoOwner, linkedRepoName,
                            new RepositoryIssueRequest {State = ItemStateFilter.All}, new ApiOptions {PageSize = 100});
                        if (!int.TryParse(value, out int milestone))
                        {
                            var allMilestones = await client.Issue.Milestone.GetAllForRepository(linkedRepoOwner, linkedRepoName,
                                new MilestoneRequest {State = ItemStateFilter.All}, new ApiOptions {PageSize = 100});
                            milestone = allMilestones.FirstOrDefault(x => x.Title == value)?.Number ??
                                        throw new ArgumentException("That milestone does not exist");
                        }

                        Milestone githubMilestone = await client.Issue.Milestone.Get(linkedRepoOwner, linkedRepoName, milestone);
                        var issuesInMilestone = allIssuesInRepo.Where(x => x.Milestone?.Number == milestone).ToList();
                        var open = issuesInMilestone.Where(x => x.State.Value == ItemState.Open).ToList();
                        var closed = issuesInMilestone.Where(x => x.State.Value == ItemState.Closed).ToList();

                        EmbedBuilder embed = new EmbedBuilder()
                            .WithTitle(githubMilestone.Title)
                            .WithDescription(githubMilestone.Description == null
                                ? ""
                                : githubMilestone.Description.Length > 1950
                                    ? $"{githubMilestone.Description.Substring(0, 1950)}..."
                                    : githubMilestone.Description)
                            .WithUrl(githubMilestone.HtmlUrl)
                            .WithFooter(
                                $"Milestone #{githubMilestone.Number} • {githubMilestone.State.Value.ToString()}")
                            .WithColor(githubMilestone.State.Value == ItemState.Open ? Color.Green : Color.Red)
                            .WithTimestamp(githubMilestone.CreatedAt);

                        if (githubMilestone.DueOn.HasValue)
                        {
                            embed.Timestamp = githubMilestone.DueOn.Value;
                        }

                        if (open.Count > 0)
                        {
                            StringBuilder openIssueString = new StringBuilder();

                            for (int i = 0; i < Math.Min(open.Count, 7); i++)
                            {
                                openIssueString.AppendLine(GithubHelper.MDLink(
                                    $"#{open[i].Number}: {(open[i].Title.Length > 50 ? open[i].Title.Substring(0, 50) + "..." : open[i].Title)}",
                                    open[i].HtmlUrl));
                            }

                            if (open.Count - 7 > 0)
                            {
                                openIssueString.AppendLine(GithubHelper.MDLink($"...and {open.Count - 7} more",
                                    githubMilestone.HtmlUrl));
                            }

                            embed.AddField($"{open.Count} Open Issue{(open.Count == 1 ? "" : "s")}", openIssueString);
                        }

                        if (closed.Count > 0)
                        {
                            StringBuilder closedIssueString = new StringBuilder();

                            for (int i = 0; i < Math.Min(closed.Count, 7); i++)
                            {
                                closedIssueString.AppendLine(GithubHelper.MDLink(
                                    $"#{closed[i].Number}: {(closed[i].Title.Length > 50 ? closed[i].Title.Substring(0, 50) + "..." : closed[i].Title)}",
                                    closed[i].HtmlUrl));
                            }

                            if (closed.Count - 7 > 0)
                            {
                                closedIssueString.AppendLine(GithubHelper.MDLink($"...and {closed.Count - 7} more",
                                    githubMilestone.HtmlUrl + "?closed=1"));
                            }

                            embed.AddField($"{closed.Count} Closed Issue{(closed.Count == 1 ? "" : "s")}",
                                closedIssueString);
                        }
                        return new SuccessResult(embed: embed.Build());
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        public enum GithubAction
        {
            [HelpText("Lists information about the GitHub helper")] Info,
            [HelpText("Links a GitHub repository to the current server; enables GitHub helper")] Link,
            [HelpText("Unlinks the linked repository from the current server; disables GitHub helper")] Unlink,
            [HelpText("Lists information about a GitHub milestone")] Milestone
        }
    }

    public static class GithubHelper
    {
        public static string MDLink(string text, string url)
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
                var issues = Regex.Matches(context.Message.Content, @"\b[gG][hH]#([1-9]\d*)\b").Select(x => int.Parse(x.Groups[1].Value));
                var commits = Regex.Matches(context.Message.Content, @"\b[gG][hH]:([0-9a-fA-F]{40}|[0-9a-fA-F]{7})\b").Select(x => x.Groups[1].Value);
                var users = Regex.Matches(context.Message.Content, @"\b[gG][hH]@([a-zA-Z\d](?:[a-zA-Z\d]|-(?=[a-zA-Z\d])){0,38})\b").Select(x => x.Groups[1].Value);
                users = users.Concat(Regex.Matches(context.Message.Content, @"[gG][hH]<@(\d+)>").Select(x => context.Bot.Client.GetUser(ulong.Parse(x.Groups[1].Value)).Username));

                string repoOwner = repository.Split('/')[0];
                string repoName = repository.Split('/')[1];

                if (!issues.Any() && !commits.Any() && !users.Any())
                {
                    return;
                }

                using (context.Channel.EnterTypingState())
                {
                    var client = new GitHubClient(new ProductHeaderValue("Netcat-Discord-Bot"));
                    client.Credentials = new Credentials(token);

                    foreach (int issue in issues)
                    {
                        try
                        {
                            Issue githubIssue = await client.Issue.Get(repoOwner, repoName, issue);
                            PullRequest pr = githubIssue.PullRequest != null
                                ? await client.PullRequest.Get(repoOwner, repoName,
                                    int.Parse(githubIssue.PullRequest.Url.Split('/').Last()))
                                : null;

                            string body = ProcessGithubMarkdown(githubIssue.Body, repository);
                            EmbedBuilder embed = new EmbedBuilder()
                                .WithTitle($"#{githubIssue.Number}: {githubIssue.Title}")
                                .WithAuthor(githubIssue.User.Login, githubIssue.User.AvatarUrl,
                                    githubIssue.User.HtmlUrl)
                                .WithDescription(body.Length > 1950 ? $"{body.Substring(0, 1950)}..." : body)
                                .WithUrl(githubIssue.HtmlUrl)
                                .WithColor(pr != null && pr.Merged ? Color.DarkPurple :
                                    githubIssue.State.Value == ItemState.Open ? Color.Green : Color.Red)
                                .WithFooter(
                                    (pr != null && pr.Merged
                                        ? "Merged"
                                        : char.ToUpper(githubIssue.State.StringValue[0]) +
                                          githubIssue.State.StringValue.Substring(1)) +
                                    (pr != null ? " Pull Request" : " Issue"))
                                .WithTimestamp(githubIssue.CreatedAt);

                            if (githubIssue.Labels != null && githubIssue.Labels.Count > 0)
                            {
                                embed.AddField("Labels", string.Join(", ", githubIssue.Labels.Select(x => x.Name)),
                                    inline: true);
                            }

                            if (githubIssue.Milestone != null)
                            {
                                embed.AddField("Milestone",
                                    MDLink(githubIssue.Milestone.Title, githubIssue.Milestone.HtmlUrl), inline: true);
                            }

                            if (githubIssue.Assignees != null && githubIssue.Assignees.Count > 0)
                            {
                                embed.AddField("Assignees",
                                    string.Join(", ", githubIssue.Assignees.Select(x => MDLink(x.Login, x.HtmlUrl))),
                                    inline: true);
                            }

                            if (githubIssue.Comments > 0)
                            {
                                embed.AddField("Comments", githubIssue.Comments, inline: true);
                            }

                            if (pr != null)
                            {
                                embed.AddField("Branches", $"{pr.Base.Label} ← {pr.Head.Label}");
                                embed.AddField("Stats", $"{pr.Commits} commit{(pr.Commits == 1 ? "" : "s")}\n" +
                                                        $"{pr.ChangedFiles} file{(pr.ChangedFiles == 1 ? "" : "s")} changed\n" +
                                                        $"{pr.Additions + pr.Deletions} changes (+{pr.Additions} / -{pr.Deletions})",
                                    inline: true);
                            }

                            await context.Reply("", embed: embed.Build());
                        }
                        catch (NotFoundException) { }
                    }

                    foreach (string commit in commits)
                    {
                        try
                        {
                            GitHubCommit githubCommit = await client.Repository.Commit.Get(repoOwner, repoName, commit);
                            string commitBody = ProcessGithubMarkdown(
                                githubCommit.Commit.Message.Contains('\n')
                                    ? githubCommit.Commit.Message.Substring(githubCommit.Commit.Message.IndexOf('\n'))
                                    : "", repository);
                            EmbedBuilder embed = new EmbedBuilder()
                                .WithTitle(githubCommit.Commit.Message.Split('\n')[0])
                                .WithAuthor(githubCommit.Author.Login, githubCommit.Author.AvatarUrl,
                                    githubCommit.Author.HtmlUrl)
                                .WithDescription(commitBody.Length > 1950
                                    ? $"{commitBody.Substring(0, 1950)}..."
                                    : commitBody)
                                .WithUrl(githubCommit.HtmlUrl)
                                .WithColor(Color.LightGrey)
                                .WithFooter($"Commit {commit.Substring(0, 7)}")
                                .WithTimestamp(githubCommit.Commit.Committer.Date)
                                .AddField("Total Changes",
                                    $"{githubCommit.Stats.Total} (+{githubCommit.Stats.Additions} / -{githubCommit.Stats.Deletions})",
                                    inline: true)
                                .AddField("Files Changed",
                                    string.Join('\n',
                                        githubCommit.Files.Select(x =>
                                            $"{MDLink("🔗", x.BlobUrl)} `{x.Filename}` • {githubCommit.Stats.Total} (+{githubCommit.Stats.Additions} / -{githubCommit.Stats.Deletions})")),
                                    inline: true);
                            if (githubCommit.Commit.CommentCount > 0)
                            {
                                embed.AddField("Comments", githubCommit.Commit.CommentCount, inline: true);
                            }

                            await context.Reply("", embed: embed.Build());
                        }
                        catch (NotFoundException) { }
                    }

                    foreach (string user in users)
                    {
                        try
                        {
                            User githubUser = await client.User.Get(user);
                            EmbedBuilder embed = new EmbedBuilder()
                                .WithAuthor(
                                    githubUser.Name == null
                                        ? githubUser.Login
                                        : $"{githubUser.Name} ({githubUser.Login})", githubUser.AvatarUrl,
                                    githubUser.HtmlUrl)
                                .WithDescription(githubUser.Bio == null ? "" :
                                    githubUser.Bio.Length > 1950 ? $"{githubUser.Bio.Substring(0, 1950)}..." :
                                    githubUser.Bio)
                                .WithColor(Color.LightGrey)
                                .AddField("Followers", githubUser.Followers, inline: true)
                                .AddField("Following", githubUser.Following, inline: true)
                                .AddField("Public Repositories", githubUser.PublicRepos, inline: true)
                                .AddField("Public Gists", githubUser.PublicGists, inline: true);

                            if (!string.IsNullOrEmpty(githubUser.Company))
                            {
                                embed.AddField("Company", githubUser.Company, inline: true);
                            }

                            if (!string.IsNullOrEmpty(githubUser.Blog))
                            {
                                embed.AddField("Website", MDLink(githubUser.Blog, githubUser.Blog), inline: true);
                            }

                            if (!string.IsNullOrEmpty(githubUser.Location))
                            {
                                embed.AddField("Location", githubUser.Location, inline: true);
                            }

                            await context.Reply("", embed: embed.Build());
                        }
                        catch (NotFoundException) { }
                    }
                }
            }
            catch (Exception ex)
            {
                await context.ReplyError(ex);
            }
        }
    }
}
