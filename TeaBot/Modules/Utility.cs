using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;
using TeaBot.ReactionCallbackCommands;
using TeaBot.ReactionCallbackCommands.PagedCommands;
using TeaBot.Services;
using TeaBot.Utilities;

namespace TeaBot.Modules
{
    [EssentialModule]
    [Summary("Commands for information regarding guilds or users")]
    public class Utility : TeaInteractiveBase
    {
        private readonly DatabaseService _database;

        public Utility(DatabaseService database)
        {
            _database = database;
        }

        [Command("avatar")]
        [Alias("av")]
        [Summary("Get someone's avatar")]
        [Ratelimit(3)]
        public async Task Avatar(
            [Summary("The user whose avatar you wish to see in full size.")] IUser user
            )
        {
            var embed = new EmbedBuilder();
            string avatarUrl = user.GetAvatarUrl(size: 2048);

            var footer = new EmbedFooterBuilder()
            {
                Text = Context.User.ToString(),
                IconUrl = Context.User.GetAvatarUrl()
            };

            embed.WithImageUrl(avatarUrl)
                .WithColor(TeaEssentials.MainColor)
                .WithTitle($"Avatar of {user}")
                .WithUrl(avatarUrl)
                .WithFooter(footer);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("avatar")]
        [Alias("av")]
        [Summary("Get your avatar.")]
        [Ratelimit(3)]
        public async Task Avatar() => await Avatar(Context.User);

        [Command("userinfo", RunMode = RunMode.Async)]
        [Alias("ui")]
        [Summary("Miscellaneous info about a user")]
        [Ratelimit(3)]
        public async Task UserInfo(
            [Summary("The user who you wish to know more about.")] IUser user
            )
        {
            var embed = new EmbedBuilder();

            string activity = user.Activity?.Type switch
            {
                null => "-",
                ActivityType.CustomStatus => $"{user.Activity.Name}",
                ActivityType.Listening => $"Listening to **{user.Activity.Name}**",
                _ => $"{user.Activity.Type} **{user.Activity.Name}**"
            };

            var footer = new EmbedFooterBuilder()
            {
                Text = Context.User.ToString(),
                IconUrl = Context.User.GetAvatarUrl()
            };

            embed.WithFooter(footer)
                .WithColor(TeaEssentials.MainColor)
                .WithCurrentTimestamp()
                .WithTitle($"Information about {user}")
                .WithThumbnailUrl(user.GetAvatarUrl(size: 2048))
                .AddField("Current status", user.Status, true)
                .AddField("Activity", activity, true)
                .AddField("ID", Context.User.Id)
                .AddField("Account creation date", TimeUtilities.DateString(user.CreatedAt.DateTime), true);

            if (!Context.IsPrivate)
            {
                var guildUser = Context.Guild.GetUser(user.Id);

                string query = $"SELECT last_message_timestamp, guildid, channelid, messageid FROM guildusers WHERE userid=@uid AND guildid=@gid";
                var cmd = _database.GetCommand(query);

                cmd.Parameters.AddWithValue("uid", (long)guildUser.Id);
                cmd.Parameters.AddWithValue("gid", (long)Context.Guild.Id);

                // Get last message
                var reader = await cmd.ExecuteReaderAsync();
                string lastMessage = "No information";
                if (reader.HasRows)
                {
                    await reader.ReadAsync();

                    DateTime sent = reader.GetDateTime(0);
                    long gid = reader.GetInt64(1);
                    long cid = reader.GetInt64(2);
                    long mid = reader.GetInt64(3);

                    string messageUrl = $"https://discordapp.com/channels/{gid}/{cid}/{mid}";
                    lastMessage = $"{TimeUtilities.DateString(sent, true)}\n[Click here to jump to the message!]({messageUrl})";
                }
                await reader.CloseAsync();

                // Roles of the user
                IEnumerable<SocketRole> roles = guildUser.Roles.Where(x => !x.IsEveryone).OrderByDescending(x => x.Position);

                // User join date
                DateTime joined = guildUser.JoinedAt.Value.DateTime;

                // Roles (in case there's a need for them to be shortened to fit the embed field limit
                var shortenedRoles = roles.Shorten(x => x.Mention, 1024, " ");

                // Determine join position
                int joinPosition;
                var orderedUsers = Context.Guild.Users.OrderBy(x => x.JoinedAt.Value.DateTime);
                for (joinPosition = 0; joinPosition < orderedUsers.Count(); joinPosition++)
                    if (orderedUsers.ElementAt(joinPosition).Id == user.Id)
                        break;

                embed.AddField($"Joined {Context.Guild}", TimeUtilities.DateString(joined), true)
                    .AddField($"Last message in {Context.Guild.Name}", lastMessage)
                    .AddField($"Roles{(shortenedRoles.Count() != roles.Count() ? $" (shortened, displaying highest {shortenedRoles.Count()} roles)" : "")}", guildUser.Roles.Count == 1 ? "-" : string.Join(" ", shortenedRoles))
                    .AddField("Main permissions", PermissionUtilities.MainGuildPermissionsString(guildUser.GuildPermissions))
                    .AddField("Join position", joinPosition + 1, true)
                    .AddField("Guild nickname", guildUser.Nickname ?? "-", true);
            }

            await ReplyAsync(embed: embed.Build());
        }

        [Command("userinfo")]
        [Alias("ui")]
        [Summary("Miscellaneous info about your account")]
        [Ratelimit(3)]
        public async Task UserInfo() => await UserInfo(Context.User);

        [Command("guildinfo")]
        [Alias("gi")]
        [Summary("Miscellaneous info about a guild")]
        [RequireContext(ContextType.Guild, ErrorMessage = "")]
        [Ratelimit(3)]
        public async Task GuildInfo()
        {
            var embed = new EmbedBuilder();
            var guild = Context.Guild;

            int botsCount = guild.Users.Where(user => user.IsBot).Count();

            string query = $"SELECT COUNT(last_message_timestamp) FROM guildusers WHERE guildid=@gid AND" +
                                "(now()::timestamp - last_message_timestamp) <= interval '2 weeks'";
            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            long activeMembersCount = reader.GetInt64(0);
            await reader.CloseAsync();

            int totalUserCount = guild.Users.Where(user => !user.IsBot).Count();
            double activeUsersPercentage = ((double)activeMembersCount / totalUserCount);

            int userRolesCount = guild.Roles.Where(x => !x.IsEveryone && !x.IsManaged).Count();

            TimeSpan timeDifference = DateTime.UtcNow - guild.GetUser(Context.Client.CurrentUser.Id).JoinedAt.Value.DateTime;

            embed.WithTitle($"{guild}")
                .WithColor(TeaEssentials.MainColor)
                .WithCurrentTimestamp()
                .WithThumbnailUrl(guild.IconUrl)
                .WithDescription(
                $"Member count: {guild.MemberCount - botsCount} (+ {botsCount} bots)\n" +
                $"Online members: {guild.Users.Where(user => user.Status == UserStatus.Online || user.Status == UserStatus.Idle || user.Status == UserStatus.DoNotDisturb).Count() - botsCount}\n" +
                $"Channel categories: {guild.CategoryChannels.Count}\n" +
                $"Text channels: {guild.Channels.Where(x => x is ITextChannel).Count()}\n" +
                $"Voice channels: {guild.Channels.Where(x => x is IVoiceChannel).Count()}\n" +
                $"Roles: {userRolesCount} (+ {guild.Roles.Count - userRolesCount} integrated or Discord managed roles)\n")
                .AddField("Created", TimeUtilities.DateString(guild.CreatedAt.DateTime))
                .AddField("Owner", guild.Owner.Mention, true)
                .AddField("System channel", guild.SystemChannel is null ? "-" : guild.SystemChannel.Mention, true)
                .AddField("Region", guild.VoiceRegionId, true)
                .AddField("Member activity", $"{activeMembersCount} active members out of {totalUserCount} // {activeUsersPercentage:#0.00%}\n" +
                $"`Note: only includes people who have sent a message " +
                $"{(timeDifference > TimeSpan.FromDays(14) ? "in the last two weeks" : $"since the bot's join date ({(timeDifference.TotalDays >= 1 ? TimeUtilities.Pluralize(timeDifference.Days, "day") : "Today")})")}`");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("roles")]
        [Summary("Show all of the roles and amount of users who have a specific role")]
        [RequireContext(ContextType.Guild, ErrorMessage = "")]
        [Ratelimit(3)]
        public async Task Roles()
        {
            var rolesMessage = new RolesMessage(Interactive, Context, 15);
            await rolesMessage.DisplayAsync();
        }

        [Command("joinpostop", true)]
        [Alias("jpt", "joinpos")]
        [Summary("Display all the users of the guild sorted by their join date")]
        [Ratelimit(3)]
        public async Task JoinPosTop(
            [Summary("The options to use for the top. Currently there's only `nobots` which removes bots from the list.")] string options = ""
            )
        {
            bool ignoreBots = options == "nobots";

            var joinPosMessage = new JoinPositionMessage(Interactive, Context, 15, ignoreBots);
            await joinPosMessage.DisplayAsync();
        }

        [Command("role")]
        [Summary("Displays people who have a certain role. Also shows information about the role itself.")]
        [Note("If a role's name contains spaces, cover its name with `\"` from both sides")]
        [Ratelimit(3)]
        public async Task Role(
            [Summary("The role to retrieve information about.")] IRole role
            )
        {
            var roleMessage = new RoleMessage(Interactive, Context, role, 40);
            await roleMessage.DisplayAsync();
        }
    }
}
