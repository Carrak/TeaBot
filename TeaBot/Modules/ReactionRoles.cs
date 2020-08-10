using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Npgsql;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;
using TeaBot.ReactionCallbackCommands.PagedCommands;
using TeaBot.ReactionCallbackCommands.ReactionRole;
using TeaBot.Services;
using TeaBot.Services.ReactionRole;

namespace TeaBot.Modules
{
    [Name("ReactionRole")]
    [Group("rr")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.Administrator)]
    [Summary("Commands for managing the reaction-role system.\nFor help on how to set this up, refer to [this guide](https://docs.google.com/document/d/1s7uhKmnxKj25CaB0-92jjikhCXw_Z_P2NmPdRGeyBls).")]
    public class ReactionRoles : TeaInteractiveBase
    {
        private const string OptionalIndexSummary = IndexSummary + " Leave empty for the latest reaction-role message.";
        private const string IndexSummary = "The index of the reaction-role message. The indexes are available using `{prefix}rr list`.";

        private const string CustomSpecificCommand = "This command is only applicable to **custom** reaction-role messages.";
        private const string NonCustomSpecificCommand = "This command is only applicable to **non-custom** reaction-role messages.";

        private const string PermissionNotice = "\n\nIf you can't get roles after placing the needed reactions, look into the bot's permissions or the channel's permissions. It won't work if the bot doesn't have `Manage Roles`, " +
            "or if no reactions are placed on the message after displaying the message, check the `Add Reaction` permission, and so on. After making sure everything is correct, just run the command again.";

        private readonly DatabaseService _database;
        private readonly ReactionRoleService _rrservice;
        private readonly DiscordSocketClient _client;

        public ReactionRoles(DatabaseService database, ReactionRoleService rrservice, DiscordSocketClient client)
        {
            _database = database;
            _rrservice = rrservice;
            _client = client;
        }

        [Command("info")]
        [Alias("i")]
        [Summary("Gets the information/properties of a reaction-role message.")]
        [Ratelimit(4)]
        public async Task Info(
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            ReactionRoleMessage rrmsg;
            try
            {
                rrmsg = await _rrservice.GetReactionRoleMessageAsync(Context.Guild, index);
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
                return;
            }

            var info = new ReactionRoleMessageInfoPaged(Interactive, Context, rrmsg, index);
            await info.DisplayAsync();
        }

        [Command("preview")]
        [Alias("p")]
        [Summary("Take a look at what the message would look like after being displayed.")]
        [Ratelimit(4)]
        public async Task Preview(
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            ReactionRoleMessage rrmsg;
            try
            {
                rrmsg = await _rrservice.GetReactionRoleMessageAsync(Context.Guild, index);
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
                return;
            }

            if (!(rrmsg is FullReactionRoleMessage frrmsg))
            {
                await ReplyAsync($"{ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index, true)} is custom.");
                return;
            }

            if (rrmsg.EmoteRolePairs.Count == 0)
            {
                await ReplyAsync("This reaction-role message does not contain any emote-role pairs. Add them before viewing the preview.");
                return;
            }

            try
            {
                await ReplyAsync("This is how the message would look like:", embed: frrmsg.ConstructEmbed());
            }
            catch (ArgumentException)
            {
                await ReplyAsync($"This reaction-role message is too big! Consider changing its properties (descriptions, role limitations, etc) or create a new one using `{Context.Prefix}rr create`.");
            }
        }

        [Command("list")]
        [Alias("l")]
        [Summary("Displays all reaction-role messages for this guild.")]
        [Ratelimit(4)]
        public async Task List()
        {
            string query = @$"
            WITH rr AS (SELECT rrid FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            SELECT erp.rrid, COUNT(*) FROM reaction_role_messages.emote_role_pairs erp, rr WHERE erp.rrid = rr.rrid GROUP BY erp.rrid;

            SELECT rr.rrid, channelid, messageid, reaction_limit, iscustom, name
            FROM reaction_role_messages.reaction_roles rr 
            LEFT JOIN reaction_role_messages.reaction_roles_data rrd ON rr.rrid = rrd.rrid
            WHERE guildid = @gid
            ORDER BY rr.rrid
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)Context.Guild.Id);

            await using var reader = await cmd.ExecuteReaderAsync();

            // Emote-role pair counts, key is RRID
            Dictionary<int, int> counts = new Dictionary<int, int>();

            // Retrieve the counts
            while (await reader.ReadAsync())
                counts.Add(reader.GetInt32(0), reader.GetInt32(1));

            await reader.NextResultAsync();

            // Retrieve info about reaction-role messages
            List<string> rrmsgs = new List<string>();
            for (int index = 1; await reader.ReadAsync(); index++)
            {
                List<string> descriptors = new List<string>();

                ulong? channelid = await reader.IsDBNullAsync(1) ? (ulong?)null : (ulong)reader.GetInt64(1);
                ulong? messageid = await reader.IsDBNullAsync(2) ? (ulong?)null : (ulong)reader.GetInt64(2);
                int? limit = await reader.IsDBNullAsync(3) ? (int?)null : reader.GetInt32(3);
                bool iscustom = reader.GetBoolean(4);
                string name = await reader.IsDBNullAsync(5) ? null : reader.GetString(5);

                descriptors.Add($"Pairs: **{counts.GetValueOrDefault(reader.GetInt32(0))}**");
                descriptors.Add($"Limit: **{limit?.ToString() ?? "-"}**");

                if (channelid.HasValue && _client.GetChannel(channelid.Value) is ITextChannel channel)
                {
                    descriptors.Add(channel.Mention);
                    if (messageid.HasValue)
                    {
                        try
                        {
                            if (await channel.GetMessageAsync(messageid.Value) is IUserMessage message)
                                descriptors.Add($"[Message]({message.GetJumpUrl()})");
                        }
                        catch (HttpException)
                        {
                            descriptors.Add("Couldn't access the message.\nCheck permissions and display this reaction-role message again.");
                        }
                    }
                    else
                        descriptors.Add("Not displayed");
                }
                else
                    descriptors.Add("Not displayed");

                rrmsgs.Add($"**{index}**. {(iscustom ? "`Custom message`" : $"{(string.IsNullOrEmpty(name) ? $"No name yet!" : name)}")}{(descriptors.Count == 0 ? "" : $"\n{string.Join(" | ", descriptors)}")}");
            }

            await reader.CloseAsync();

            var embed = new EmbedBuilder();

            embed.WithColor(TeaEssentials.MainColor)
                .WithTitle("Reaction-role messages for this server")
                .WithDescription(rrmsgs.Count == 0 ? $"None yet!" : string.Join("\n\n", rrmsgs))
                .WithFooter($"Use {Context.Prefix}rr info [index] to see detailed info about a reaction-role message");

            await ReplyAsync(embed: embed.Build());

        }

        [Command("display")]
        [Alias("d")]
        [Summary("Commit all changes made to a reaction-role message and display it in the message's channel OR modify its properties if it's already present in the channel" + PermissionNotice)]
        [Note(NonCustomSpecificCommand)]
        [Ratelimit(4)]
        public async Task Display(
            [Summary("The channel to display the message in.")] ITextChannel channel,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                await _rrservice.ChangeChannelAsync(Context.Guild, index, channel);
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
                return;
            }

            await Display(index);
        }

        [Command("display")]
        [Alias("d")]
        [Summary("Commit all changes made to a reaction-role message and display it in the message's channel OR modify its properties if it's already present in the channel" + PermissionNotice)]
        [Note(NonCustomSpecificCommand)]
        [Ratelimit(4)]
        public async Task Display(
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                var rrmsg = await _rrservice.DisplayFullReactionRoleMessageAsync(Context.Guild, index);
                await ReplyAsync($"Displayed {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)} in channel <#{rrmsg.Channel.Id}>.");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
            catch (ReactionRoleMessageException rrme)
            {
                await ReplyAsync(rrme.Message.Replace("{prefix}", Context.Prefix));
            }
            catch (HttpException)
            {
                await ReplyAsync($"Could not access the channel or send the message.\n" +
                    $"Check the bot's channel permissions or set a different channel using {ReactionRoleServiceMessages.ReactionRoleMessageCommandString(Context.Prefix, "channel [channel]", index)}");
            }
        }

        [Command("displaycustom")]
        [Alias("dc")]
        [Summary("Commit all changes made to **custom** a reaction-role message and add all reactions to it." + PermissionNotice)]
        [Note(CustomSpecificCommand)]
        [Ratelimit(4)]
        public async Task DisplayCustom(
            [Summary("The link of the message to add reactions and callback to. This is a link of the format `https://discordapp.com/channels/{guild}/{channel}/{id}`." +
            "You can get the link by hovering over the message, going to its `More` tab (the 3 dots) and pressing `Copy Message Link`")] string link,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            string pattern = @"(http|https?:\/\/)?(www\.)?(discord\.(gg|io|me|li|com)|discord(app)?\.com\/channels)\/(?<Guild>\w+)\/(?<Channel>\w+)\/(?<Message>\w+)";
            var match = System.Text.RegularExpressions.Regex.Match(link, pattern);

            // Check if the given link matches the pattern
            if (!match.Success)
            {
                await ReplyAsync("The given link does not match Discord message link pattern - `https://discordapp.com/channels/{guild}/{channel}/{id}`");
                return;
            }

            // Check if the guild in the link is valid
            if (!ulong.TryParse(match.Groups["Guild"].Value, out var guildid))
            {
                await ReplyAsync("Invalid guild ID.");
                return;
            }

            // Check if the guild is the same as this one
            if (Context.Guild.Id != guildid)
            {
                await ReplyAsync("The specified message is in another guild.");
                return;
            }

            // Check if the channel is valid
            if (!ulong.TryParse(match.Groups["Channel"].Value, out var channelid))
            {
                await ReplyAsync("Invalid channel ID.");
                return;
            }

            // Check if the channel exists
            if (!(_client.GetChannel(channelid) is ITextChannel channel))
            {
                await ReplyAsync($"Channel with ID `{channelid}` does not exist.");
                return;
            }

            // Check if the message is valid
            if (!ulong.TryParse(match.Groups["Message"].Value, out var messageid))
            {
                await ReplyAsync("Invalid message ID.");
                return;
            }

            // Try to get the message
            IUserMessage message;
            try
            {
                message = await channel.GetMessageAsync(messageid) as IUserMessage;
            }
            catch (HttpException)
            {
                await ReplyAsync("Couldn't access to the channel. Consider changing channel permissions before displaying the message");
                return;
            }

            // Check if the message exists
            if (message is null)
            {
                await ReplyAsync($"Message with ID `{messageid}` does not exist.");
                return;
            }

            try
            {
                await _rrservice.ChangeMessageAsync(Context.Guild, index, message);
                var rrmsg = await _rrservice.DisplayCustomReactionRoleMessage(Context.Guild, index);
                await ReplyAsync($"Successfully displayed {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.\nMessage: {rrmsg.Message.GetJumpUrl()}");
            }
            catch (ReactionRoleMessageException rrme)
            {
                await ReplyAsync(rrme.Message.Replace("{prefix}", Context.Prefix));
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("displaycustom")]
        [Alias("dc")]
        [Summary("Commit all changes made to **custom** a reaction-role message and add all reactions to it." + PermissionNotice)]
        [Note(CustomSpecificCommand)]
        [Ratelimit(4)]
        public async Task DisplayCustom(
            ITextChannel channel,
            ulong messageId,
            int? index = null
            )
        {
            IUserMessage message;
            try
            {
                message = await channel.GetMessageAsync(messageId) as IUserMessage;
            }
            catch (HttpException)
            {
                await ReplyAsync("Couldn't access to the channel. Consider changing channel permissions before displaying the message");
                return;
            }
            catch (ReactionRoleMessageException rrme)
            {
                await ReplyAsync(rrme.Message.Replace("{prefix}", Context.Prefix));
                return;
            }

            try
            {
                await _rrservice.ChangeMessageAsync(Context.Guild, index, message);
                await _rrservice.DisplayCustomReactionRoleMessage(Context.Guild, index);
                await ReplyAsync($"Successfully displayed {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.\n" +
                    $"Message link: {message.GetJumpUrl()}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
            catch (ArgumentException)
            {
                await ReplyAsync($"This reaction-role message is too big! Consider changing its properties (descriptions, role limitations, etc) or create a new one using {ReactionRoleServiceMessages.ReactionRoleMessageCommandString(Context.Prefix, "create")}.");
            }
        }

        [Command("displaycustom")]
        [Summary("Same as the other ones, except this one primarily serves the purpose of updating the reaction-role message, considering it has already been displayed." + PermissionNotice)]
        public async Task DisplayCustom(int? index = null)
        {
            try
            {
                await _rrservice.DisplayCustomReactionRoleMessage(Context.Guild, index);
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
            catch (ReactionRoleMessageException rrme)
            {
                await ReplyAsync(rrme.Message.Replace("{prefix}", Context.Prefix));
            }
        }

        [Command("create")]
        [Alias("c")]
        [Summary("Create an empty reaction-role message. There's a limit of 5 reaction-role messages per guild.")]
        [Ratelimit(4)]
        public async Task Create()
        {
            try
            {
                await _rrservice.CreateReactionRoleMessage(Context.Guild);
                await ReplyAsync($"Successfully created an empty reaction-role message! Use `{Context.Prefix}rr list` to see all reaction-role messages.");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix));
            }
        }

        [Command("createcustom")]
        [Alias("cc")]
        [Summary("Create an empty custom reaction-role message. There's a limit of 5 reaction-role messages per guild.")]
        public async Task CreateCustom()
        {
            try
            {
                await _rrservice.CreateCustomReactionRoleMessage(Context.Guild);
                await ReplyAsync($"Successfully created an empty custom reaction-role message! Use `{Context.Prefix}rr list` to see all reaction-role messages.");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix));
            }
        }

        [Command("delete")]
        [Alias("remove", "del", "r")]
        [Summary("Entirely deletes a reaction-role message with all of its contents.")]
        [Ratelimit(4)]
        public async Task Delete(
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            try
            {
                await _rrservice.RemoveReactionRoleMessage(Context.Guild, index);
                await ReplyAsync($"Successfully removed {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("changeemote")]
        [Alias("ce")]
        [Summary("Changes the emote of an emote-role pair.")]
        [Ratelimit(4)]
        public async Task ChangeEmote(
            [Summary("The existing role in emote-role pairs of a reaction role message. (this field remains the same after the change)")] IRole existingRole,
            [Summary("The new emote to set.")] IEmote newEmote,
            [Summary(OptionalIndexSummary)] int? index = null
            )
            => await ChangeEmote(newEmote, existingRole, index);

        [Command("changeemote")]
        [Alias("ce")]
        [Summary("Changes the emote of an emote - role pair.")]
        [Ratelimit(4)]
        public async Task ChangeEmote(
            [Summary("The new emote to set.")] IEmote newEmote,
            [Summary("The existing role in emote-role pairs of a reaction role message. (this field remains the same after the change)")] IRole existingRole,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                await _rrservice.ChangeEmote(Context.Guild, index, existingRole, newEmote);
                await ReplyAsync($"Successfully changed the emote for role `{existingRole.Name}` to {newEmote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index).Replace("{role}", existingRole.Name));
            }
        }

        [Command("changerole")]
        [Alias("cr")]
        [Summary("Changes the role of an emote-role pair.")]
        [Ratelimit(4)]
        public async Task ChangeRole(
            [Summary("The new role to set.")] IRole newRole,
            [Summary("The existing emote in emote-role pairs of a reaction role message. (this field remains the same after the change)")] IEmote existingEmote,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                await _rrservice.ChangeRole(Context.Guild, index, newRole, existingEmote);
                await ReplyAsync($"Successfully changed the role for emote {existingEmote} to `{newRole.Name}` in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index).Replace("{emote}", existingEmote.ToString()));
            }
        }

        [Command("changerole")]
        [Alias("cr")]
        [Summary("Changes the role of an emote-role pair.")]
        [Ratelimit(4)]
        public async Task ChangeRole(
            [Summary("The existing emote in emote-role pairs of a reaction role message. (this field remains the same after the change)")] IEmote existingEmote,
            [Summary("The new role to set.")] IRole newRole,
            [Summary(OptionalIndexSummary)] int? index = null
            )
            => await ChangeRole(newRole, existingEmote, index);

        [Command("addpair")]
        [Alias("ap")]
        [Summary("Adds an emote-role pair to a reaction-role message.")]
        [Ratelimit(4)]
        public async Task AddPair(
            [Summary("The emote of the reaction-role pair.")] IEmote emote,
            [Summary("The role of the reaction-role pair.")] IRole role,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            if (emote is Emote e && !Context.Guild.Emotes.Contains(e))
            {
                await ReplyAsync("I know nitro is cool and all, but please use emotes from this guild.");
                return;
            }

            var highestRole = Context.Guild.CurrentUser.Roles.OrderByDescending(x => x.Position).First();
            if (role.Id == highestRole.Id)
            {
                await ReplyAsync($"Cannot add {role.Name} because it is the bot's highest role.");
                return;
            }
            else if (role.Position >= highestRole.Position)
            {
                await ReplyAsync($"Cannot add `{role.Name}` because it is placed higher than the bot's highest role - `{highestRole.Name}`.");
                return;
            }

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {

                await _rrservice.AddPairAsync(Context.Guild, index, emote, role);
                Console.WriteLine($"{sw.ElapsedMilliseconds}ms - Pair added");
                await ReplyAsync($"Successfully added the pair to {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.");
                Console.WriteLine($"{sw.ElapsedMilliseconds}ms - Message sent");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("removepair")]
        [Alias("rp")]
        [Summary("Removes a pair with the given emote from a reaction-role message.")]
        [Ratelimit(4)]
        public async Task RemovePair(
            [Summary("The emote of the reaction-role pair.")] IEmote emote,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                await _rrservice.RemovePairAsync(Context.Guild, index, emote);
                await ReplyAsync($"Successfully removed the pair with emote {emote} from {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index).Replace("{emote}", emote.ToString()));
            }
        }

        [Command("removepair")]
        [Alias("rp")]
        [Summary("Removes a pair with the given role from a reaction-role message.")]
        [Ratelimit(4)]
        public async Task RemovePair(
            [Summary("The role of the reaction-role pair.")] IRole role,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                await _rrservice.RemovePairAsync(Context.Guild, index, role);
                await ReplyAsync($"Successfully removed the pair with role `{role.Name}` from {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index).Replace("{role}", role.Name));
            }
        }

        [Command("channel")]
        [Alias("ch")]
        [Summary("Changes the channel of a reaction-role message.")]
        [Ratelimit(4)]
        public async Task ChangeChannel(
            [Summary("The new channel to set. This is where the message will be displayed after using `{prefix}rr display`")] ITextChannel channel,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                await _rrservice.ChangeChannelAsync(Context.Guild, index, channel);
                await ReplyAsync($"Successfully changed the channel to {channel.Mention} for {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("name")]
        [Alias("n")]
        [Summary("Change the name of the latest reaction-role message.")]
        [Ratelimit(4)]
        public async Task SetName(
            [Summary(IndexSummary)] int? index,
            [Summary("The new name to set.\nUse `{prefix}rr name delete` or `{prefix}rr name remove` to set the name back to default.")] [Remainder] string name
            ) => await SetName(name, index);

        [Command("name")]
        [Alias("n")]
        [Summary("Change the name of the reaction-role message at a given index.")]
        [Ratelimit(4)]
        public async Task SetName(
            [Summary("The new name to set.\nUse `{prefix}rr name delete` or `{prefix}rr name remove` to set the name back to default.")] string name,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            if (name.Length > 40)
            {
                await ReplyAsync("Names cannot be over 40 symbols long!");
                return;
            }

            if (name == "remove" || name == "delete")
                name = null;

            try
            {
                await _rrservice.ChangeNameAsync(Context.Guild, index, name);
                if (name is null)
                    await ReplyAsync($"Successfully reset the name back to default {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                else
                    await ReplyAsync($"Successfully changed the name for {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.\nNew name: {name}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("name")]
        [Alias("n")]
        [Summary("Change the name of the latest reaction-role message.")]
        [Ratelimit(4)]
        public async Task SetName(
            [Summary("The new name to set.\nUse `{prefix}rr name delete` or `{prefix}rr name remove` to set the name back to default.")] [Remainder] string name
            ) => await SetName(name, null);

        [Command("color")]
        [Alias("colour", "col")]
        [Summary("Modify the color of a reaction-role message's embed.")]
        [Ratelimit(4)]
        public async Task SetColor(
            [Summary("The new color to set.")] Color color,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                await _rrservice.ChangeColorAsync(Context.Guild, index, color == TeaEssentials.MainColor ? null : (Color?)color);
                await ReplyAsync($"Successfully changed the color to {color} for {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("swap")]
        [Alias("reorder", "order", "changeorder")]
        [Summary("Swaps the positions of emote-role pairs in a reaction-role message.")]
        [Ratelimit(4)]
        public async Task ChangeOrder(
            [Summary("The emote of an emote-role pair to swap.")] IEmote emote1,
            [Summary("The emote of an emote-role pair to swap.")] IEmote emote2,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                await _rrservice.ChangeOrder(Context.Guild, index, emote1, emote2);
                await ReplyAsync($"Successfully swapped the order of {emote1} and {emote2} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("limit")]
        [Alias("setlimit, lim")]
        [Summary("Sets the amount of roles a user can get from a reaction-role message.")]
        public async Task SetLimit(
            [Summary("The limit to set. To remove the limit, use **0**")] int limit,
            [Summary(OptionalIndexSummary)] int? index = null)
        {
            try
            {
                await _rrservice.ChangeLimit(Context.Guild, index, limit);
                if (limit == 0)
                    await ReplyAsync($"Successfully removed the limit for {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                else
                    await ReplyAsync($"Successfully set the limit to **{limit}** for {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index)
                    .Replace("{limit}", Utilities.GeneralUtilities.Pluralize(limit, "emote-role pairs"))
                    );
            }
        }

        [Command("togglecustom")]
        [Summary("Makes a reaction-role message custom or non-custom.\n**Warning**\nGoing from non-custom to custom deletes all data like descriptions/colours/names, but leaves the pairs and role limitations.")]
        [Ratelimit(4)]
        public async Task ToggleCustom(
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                bool custom = await _rrservice.ToggleCustom(Context.Guild, index);
                if (custom)
                    await ReplyAsync($"{ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index, true)} is now custom.");
                else
                    await ReplyAsync($"{ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index, true)} is no longer custom.");

            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("description")]
        [Alias("desc")]
        [Summary("Changes the description of a reaction-role message.")]
        [Ratelimit(4)]
        public async Task ChangeReactionRoleMessageDescription(
            [Summary(OptionalIndexSummary)] int? index,
            [Summary("The description to attach to this reaction-role message.\n" +
            "Use `{prefix}rr description delete` or `{prefix}rr description remove` to remove the description.")] [Remainder] string description
            ) => await ChangeReactionRoleMessageDescription(description, index);

        [Command("description")]
        [Alias("desc")]
        [Summary("Changes the description of a reaction-role message.")]
        [Ratelimit(4)]
        public async Task ChangeReactionRoleMessageDescription(
            [Summary("The description to attach to this reaction-role message.\n" +
            "Use `{prefix}rr description delete` or `{prefix}rr description remove` to remove the description.")] string description,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            if (description.Length > 200)
            {
                await ReplyAsync("Description length must be less than 200 symbols.");
                return;
            }

            if (description == "remove" || description == "delete")
                description = null;

            try
            {
                await _rrservice.ChangeReactionRoleMessageDescription(Context.Guild, index, description);
                if (description is null)
                    await ReplyAsync($"Succesfully set the description for {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}\nNew description: {description}");
                else
                    await ReplyAsync($"Successfully removed the description for {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("description")]
        [Alias("desc")]
        [Summary("Changes the description of a reaction-role message.")]
        [Ratelimit(4)]
        public async Task ChangeReactionRoleMessageDescription(
            [Summary("The description to attach to this reaction-role message.\n" +
            "Use `{prefix}rr description delete` or `{prefix}rr description remove` to remove the description.")] [Remainder] string description
            ) => await ChangeReactionRoleMessageDescription(description, null);

        [Command("pairdescription")]
        [Alias("pd", "pdesc", "pairdesc")]
        [Summary("Changes the description of an emote-role pair.")]
        [Ratelimit(4)]
        public async Task ChangeEmoteRolePairDescription(
            [Summary("The emote of an existing pair.")] IEmote emote,
            [Summary("The description to attach to the emote-role pair.\n" +
            "Use `{prefix}rr description delete` or `{prefix}rr description remove` to remove the description.")] string description,
            [Summary(OptionalIndexSummary)] int? index = null)
        {
            if (description.Length > 100)
            {
                await ReplyAsync("Description length for pairs must be less than 100 symbols.");
                return;
            }

            if (description == "remove" || description == "delete")
                description = null;

            try
            {
                await _rrservice.ChangePairDescription(Context.Guild, index, emote, description);
                if (description is null)
                    await ReplyAsync($"Successfully removed the description for emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                else
                    await ReplyAsync($"Succesffuly set the description for emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}\nNew description: {description}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("pairdescription")]
        [Alias("pd", "pdesc", "pairdesc")]
        [Summary("Changes the description of an emote-role pair.")]
        [Ratelimit(4)]
        public async Task ChangeEmoteRolePairDescription(
            [Summary("The emote of an existing pair.")] IEmote emote,
            [Summary("The description to attach to the emote-role pair.\n" +
            "Use `{prefix}rr description delete` or `{prefix}rr description remove` to remove the description.")] [Remainder] string description
            ) => await ChangeEmoteRolePairDescription(emote, description, null);

        [Command("pairdescription")]
        [Alias("pd", "pdesc", "pairdesc")]
        [Summary("Changes the description of an emote-role pair.")]
        [Ratelimit(4)]
        public async Task ChangeEmoteRolePairDescription(
            [Summary(IndexSummary)] int? index,
            [Summary("The emote of an existing pair.")] IEmote emote,
            [Summary("The description to attach to the emote-role pair.\n" +
            "Use `{prefix}rr description delete` or `{prefix}rr description remove` to remove the description.")] string description
            ) => await ChangeEmoteRolePairDescription(emote, description, index);

        [Command("addallowedrole")]
        [Alias("aar")]
        [Summary("Adds a role to the list of allowed roles.\n" +
            "Only the people who have all allowed roles will be able to claim the role from this pair.")]
        [Ratelimit(4)]
        public async Task AddAllowedRole(
            [Summary("The emote of an emote-role pair.")] IEmote emote,
            [Summary("The role to add the list of allowed roles.")] IRole role,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                await _rrservice.AddAllowedRole(Context.Guild, index, emote, role);
                await ReplyAsync($"Successfully added `{role.Name}` to allowed roles of emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(
                    ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index)
                    .Replace("{role}", role.Name)
                    .Replace("{emote}", emote.Name)
                    );
            }
        }

        [Command("addprohibitedrole")]
        [Alias("apr")]
        [Summary("Adds a role to the list of allowed roles of an emote-role pair.\n" +
            "Only the people who don't have any of prohibited roles will be able to claim the role from this pair.")]
        [Ratelimit(4)]
        public async Task AddProhibitedRole(
            [Summary("The emote of an emote-role pair.")] IEmote emote,
            [Summary("The role to add the list of prohibited roles.")] IRole role,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                await _rrservice.AddProhibitedRole(Context.Guild, index, emote, role);
                await ReplyAsync($"Successfully added `{role.Name}` to prohibited roles of emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(
                    ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index)
                    .Replace("{role}", role.Name)
                    .Replace("{emote}", emote.Name)
                    );
            }
        }

        [Command("addglobalallowedrole")]
        [Alias("agar")]
        [Summary("Adds a role to the list of global allowed roles of a reaction-role message.\n" +
            "This list is the same as allowed roles, except it applies to all pairs.\n" +
            "Only the people who have all allowed roles will be able to claim the role from this pair.")]
        [Ratelimit(4)]
        public async Task AddGlobalAllowedRole(
            [Summary("The role to add the list of global allowed roles.")] IRole role,
            [Summary(OptionalIndexSummary)]int? index = null
            )
        {
            try
            {
                await _rrservice.AddGlobalAllowedRole(Context.Guild, index, role);
                await ReplyAsync($"Successfully added `{role.Name}` to global allowed roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(
                    ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index)
                    .Replace("{role}", role.Name)
                    );
            }
        }

        [Command("addglobalprohibitedrole")]
        [Alias("agpr")]
        [Summary("Adds a role to the list of global prohibited roles of a reaction-role message.\n" +
            "This list is the same as prohibited roles, except it applies to all pairs." +
            "Only the people who don't have any of the global prohibited roles will be able to claim roles from this reaction-role message.")]
        [Ratelimit(4)]
        public async Task AddGlobalProhibitedRole(
            [Summary("The role to add the list of global prohibited roles.")] IRole role,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                await _rrservice.AddGlobalProhibitedRole(Context.Guild, index, role);
                await ReplyAsync($"Successfully added `{role.Name}` to global prohibited roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(
                    ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index)
                    .Replace("{role}", role.Name)
                    );
            }
        }

        [Command("removeallowedrole")]
        [Alias("rar")]
        [Summary("Removes a role from the list of allowed roles of an emote-role pair.")]
        [Ratelimit(4)]
        public async Task RemoveAllowedRole(
            [Summary("The emote of an emote-role pair.")] IEmote emote,
            [Summary("The role to remove from the list of allowed roles.")] IRole role,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                bool deleted = await _rrservice.RemoveAllowedRole(Context.Guild, index, emote, role);
                if (deleted)
                    await ReplyAsync($"Successfully removed `{role.Name}` from allowed roles of emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                else
                    await ReplyAsync($"`{role.Name}` is not present in the list of allowed roles of emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(
                    ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index)
                    .Replace("{role}", role.Name)
                    );
            }
        }

        [Command("removeprohibitedrole")]
        [Alias("rpr")]
        [Summary("Removes a role from the list of prohibited roles of an emote-role pair.")]
        [Ratelimit(4)]
        public async Task RemoveProhibitedRole(
            [Summary("The emote of an emote-role pair.")] IEmote emote,
            [Summary("The role to remove from the list of prohibited roles.")] IRole role,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                bool deleted = await _rrservice.RemoveProhibitedRole(Context.Guild, index, emote, role);
                if (deleted)
                    await ReplyAsync($"Successfully removed `{role.Name}` from allowed roles of emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                else
                    await ReplyAsync($"`{role.Name}` is not present in the list of prohibited roles of emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(
                    ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("removeglobalallowedrole")]
        [Alias("rgar")]
        [Summary("Removes a role from the list of global allowed roles of a reaction-role message.")]
        [Ratelimit(4)]
        public async Task RemoveGlobalAllowedRole(
            [Summary("The role to remove from the list of global allowed roles.")] IRole role,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                bool deleted = await _rrservice.RemoveGlobalAllowedRole(Context.Guild, index, role);
                if (deleted)
                    await ReplyAsync($"Successfully removed `{role.Name}` from global allowed roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                else
                    await ReplyAsync($"`{role.Name}` is not present in the list of global allowed roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(
                    ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }

        [Command("removeglobalprohibitedrole")]
        [Alias("rgpr")]
        [Summary("Removes a role from the list of global prohibited roles of a reaction-role message.")]
        [Ratelimit(4)]
        public async Task RemoveGlobalProhibitedRole(
            [Summary("The role to remove from the list of global prohibited roles.")] IRole role,
            [Summary(OptionalIndexSummary)] int? index = null
            )
        {
            try
            {
                bool deleted = await _rrservice.RemoveGlobalProhibitedRole(Context.Guild, index, role);
                if (deleted)
                    await ReplyAsync($"Successfully removed `{ role.Name}` from global prohibited roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                else
                    await ReplyAsync($"`{role.Name}` is not present in the list of global prohibited roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
            }
            catch (PostgresException pe)
            {
                await ReplyAsync(
                    ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
            }
        }
    }
}
