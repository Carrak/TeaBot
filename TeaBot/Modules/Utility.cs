using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Npgsql;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.ReactionCallbackCommands;

namespace TeaBot.Modules
{
    [EssentialModule]
    [Summary("Commands for information regarding guilds or users")]
    public class Utility : TeaInteractiveBase
    {
        [Command("avatar")]
        [Alias("av")]
        [Summary("Get someone's avatar")]
        public async Task Avatar(IUser user)
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
        [Summary("Get your avatar")]
        public async Task Avatar() => await Avatar(Context.User);

        [Command("userinfo", RunMode = RunMode.Async)]
        [Alias("ui")]
        [Summary("Miscellaneous info about a user")]
        public async Task UserInfo(IUser user)
        {
            var embed = new EmbedBuilder();

            string activity = user.Activity?.Type switch
            {
                null => "-",
                ActivityType.CustomStatus => $"{user.Activity.Name}",
                ActivityType.Listening => $"Listening to **{user.Activity.Name}**",
                _ => $"{user.Activity.Type} **{user.Activity.Name}**"
            };

            embed.WithFooter($"Executed by {Context.User}")
                .WithColor(TeaEssentials.MainColor)
                .WithCurrentTimestamp()
                .WithTitle($"Information about {user}")
                .WithThumbnailUrl(user.GetAvatarUrl(size: 2048))
                .AddField("Current status", user.Status)
                .AddField("Activity", activity)
                .AddField("Account creation date", DateString(user.CreatedAt.DateTime));

            if (!Context.IsPrivate)
            {
                var guildUser = Context.Guild.GetUser(user.Id);

                string query = $"SELECT last_message_timestamp, guildid, channelid, messageid FROM guildusers WHERE userid = {guildUser.Id} AND guildid = {Context.Guild.Id}";

                var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
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
                    lastMessage = $"{DateString(sent, true)}\n[Click here to jump to the message!]({messageUrl})";
                }
                await reader.CloseAsync();

                IEnumerable<SocketRole> roles = guildUser.Roles.Where(x => !x.IsEveryone).OrderByDescending(x => x.Position);

                DateTime joined = guildUser.JoinedAt.Value.DateTime;

                embed.AddField($"Joined {Context.Guild}", DateString(joined))
                    .AddField($"Last message in {Context.Guild.Name}", lastMessage)
                    .AddField($"Roles {(roles.Count() > 20 ? "(displaying 20 highest roles)" : "")}", guildUser.Roles.Count == 1 ? "-" : string.Join(" ", roles.Where((x, index) => index < 20).Select(role => role.Mention)))
                    .AddField("Main permissions", MainPermissionsString(guildUser.GuildPermissions))
                    .AddField("Join position", Context.Guild.Users.OrderBy(x => x.JoinedAt.Value.DateTime).ToList().IndexOf(guildUser) + 1)
                    .AddField("Guild nickname", guildUser.Nickname ?? "-");

            }

            await ReplyAsync(embed: embed.Build());
        }

        [Command("userinfo")]
        [Alias("ui")]
        [Summary("Miscellaneous info about your account")]
        public async Task UserInfo() => await UserInfo(Context.User);

        [Command("guildinfo")]
        [Alias("gi")]
        [Summary("Miscellaneous info about a guild")]
        [RequireContext(ContextType.Guild, ErrorMessage = "")]
        public async Task GuildInfo()
        {
            var embed = new EmbedBuilder();
            var guild = Context.Guild;

            int botsCount = guild.Users.Where(user => user.IsBot).Count();

            string query = $"SELECT COUNT(last_message_timestamp) FROM guildusers WHERE guildid = {guild.Id} AND" +
                                "(now()::timestamp - last_message_timestamp) <= interval '2 weeks'";

            var reader = await new NpgsqlCommand(query, TeaEssentials.DbConnection).ExecuteReaderAsync();
            await reader.ReadAsync();
            long activeMembersCount = reader.GetInt64(0);
            await reader.CloseAsync();

            int totalUserCount = guild.Users.Where(user => !user.IsBot).Count();
            double activeUsersPercentage = ((double)activeMembersCount / totalUserCount);

            int userRolesCount = guild.Roles.Where(x => !x.IsEveryone && !x.IsManaged).Count();

            TimeSpan timeDifference = DateTime.UtcNow - guild.GetUser(Context.Client.CurrentUser.Id).JoinedAt.Value.DateTime;

            embed.WithTitle($"{guild} Info")
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
                .AddField("Created", DateString(guild.CreatedAt.DateTime))
                .AddField("Owner", guild.Owner.Mention)
                .AddField("System channel", guild.SystemChannel is null ? "-" : guild.SystemChannel.Mention)
                .AddField("Region", guild.VoiceRegionId)
                .AddField("Member activity", $"{activeMembersCount} active members out of {totalUserCount} // {activeUsersPercentage:#0.00%}\n" +
                $"`Note: only includes people who have sent a message {(timeDifference > TimeSpan.FromDays(14) ? "in the last two weeks" : $"since the bot's join date ({(timeDifference.TotalDays >= 1 ? PeriodToString(timeDifference.Days, "day", false) : "Today")})")}`");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("roles")]
        [Summary("Show all of the roles and amount of users who have a specific role")]
        [RequireContext(ContextType.Guild, ErrorMessage = "")]
        public async Task Roles()
        {
            var rolesMessage = new RolesMessage(Interactive, Context, 15);
            await rolesMessage.DisplayAsync();
        }

        [Command("joinpostop", true)]
        [Alias("jpt", "joinpos")]
        [Summary("Display all the users of the guild sorted by their join date")]
        [Note("Use `tea joinpostop true` to ignore bots appearing in the list")]
        public async Task JoinPosTop(string options = "")
        {
            bool ignoreBots = options == "nobots";
            var joinPosMessage = new JoinPositionMessage(Interactive, Context, 15, ignoreBots);
            await joinPosMessage.DisplayAsync();
        }

        [Command("role")]
        [Summary("Displays all people who have a given role")]
        [Note("If a role contains spaces, cover its name with `\"` from both sides")]
        public async Task Role(IRole role)
        {
            var roleMessage = new RoleMessage(Interactive, Context, role, 40);
            await roleMessage.DisplayAsync();
        }

        #region Utility methods

        /// <summary>
        ///     Creates a string containing the primary permissions of a user.
        /// </summary>
        /// <param name="gp">User permissions to create a string from.</param>
        /// <returns>String containing main permissions of a user.</returns>
        static string MainPermissionsString(GuildPermissions gp)
        {
            List<string> permissions = new List<string>();

            if (gp.Administrator) return "Administrator (all permissions)";
            if (gp.BanMembers) permissions.Add("Ban members");
            if (gp.KickMembers) permissions.Add("Kick members");
            if (gp.ManageChannels) permissions.Add("Manage channels");
            if (gp.ManageEmojis) permissions.Add("Manage emojis");
            if (gp.ManageGuild) permissions.Add("Manage guild");
            if (gp.ManageMessages) permissions.Add("Manage messages");
            if (gp.ManageNicknames) permissions.Add("Manage nicknames");
            if (gp.ManageRoles) permissions.Add("Manage roles");
            if (gp.ManageWebhooks) permissions.Add("Manage webhooks");
            if (gp.MentionEveryone) permissions.Add("Mention everyone");
            if (gp.MoveMembers) permissions.Add("Move members");
            if (gp.DeafenMembers) permissions.Add("Deafen members");
            if (gp.MuteMembers) permissions.Add("Mute members");
            if (gp.ViewAuditLog) permissions.Add("View audit log");
            if (gp.CreateInstantInvite) permissions.Add("Create invites");

            return permissions.Count != 0 ? string.Join(", ", permissions) : "-";
        }

        /// <summary>
        ///     Creates a string containing full information about a date.
        /// </summary>
        /// <param name="date"><see cref="DateTime"/> object to get info on.</param>
        /// <param name="displayTime">Bool value determining whether the hours, minutes and seconds should be displayed.</param>
        /// <returns></returns>
        static string DateString(DateTime date, bool displayTime = false)
        {
            if (displayTime)
                return $"{date:dd.MM.yyyy HH:mm:ss} UTC\n{SpanBetweenDatesString(date, DateTime.UtcNow)}";
            else
                return $"{date:dd.MM.yyyy} / {date.ToString("MMMM d, yyyy (ddd)", new CultureInfo("en-US"))}\n{SpanBetweenDatesString(date, DateTime.UtcNow)}";
        }

        /// <summary>
        ///     Calculates the amount of time between <paramref name="start"></paramref> and <paramref name="end"></paramref> 
        ///     and creates a string that represents that time in either years/months/days or hours/minutes/seconds
        /// </summary>
        /// <returns>String in format "X years X months X days ago" or "X hours X minutes X seconds ago"</returns>
        static string SpanBetweenDatesString(DateTime start, DateTime end)
        {
            if (start > end)
                return SpanBetweenDatesString(end, start);

            TimeSpan timePassed = end - start;

            if (timePassed.TotalSeconds < 1)
                return "Right now";

            DateTime span = DateTime.MinValue + timePassed;

            int years = span.Year - 1;
            int months = span.Month - 1;
            int days = span.Day - 1;

            if (years == 0 && months == 0 && days == 0)
                return $"{PeriodToString(span.Hour, "hour")}{PeriodToString(span.Minute, "minute")}{PeriodToString(span.Second, "second")}ago";
            else
                return $"{PeriodToString(years, "year")}{PeriodToString(months, "month")}{PeriodToString(days, "day")}ago";
        }

        /// <summary>
        ///   Creates a string that transforms a named period of time into a string, e.g. "3 days", "1 month", etc.
        /// </summary>
        /// <param name="timePassed">The amount of the given period time.</param>
        /// <param name="timePeriod">The name of the period, e.g. month, day, etc.</param>
        /// <param name="insertSpacebar">Bool value determining whether a spacebar should be inserted at the end of the end string.</param>
        /// <returns>Created string with the number and the period, or an empty string if <paramref name="timePassed"/> is zero.</returns>
        static string PeriodToString(int timePassed, string timePeriod, bool insertSpacebar = true)
        {
            if (timePassed == 0) return "";

            string result = $"{timePassed} {timePeriod}" +
                $"{(Math.Abs(timePassed) != 1 ? "s" : "")}" +
                $"{(insertSpacebar ? " " : "")}";

            return result;
        }

        #endregion
    }
}
