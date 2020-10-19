using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Npgsql;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;
using TeaBot.ReactionCallbackCommands.PagedCommands;
using TeaBot.ReactionCallbackCommands.ReactionRole;
using TeaBot.Services;
using TeaBot.Services.ReactionRole;
using TeaBot.Utilities;

namespace TeaBot.Modules
{
    [Name("ReactionRole")]
    [Group(Group)]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "You need to be an administrator to manage the reaction-role system.")]
    [Alias("rr")]
    [Summary("Commands for managing the reaction-role system.\nFor help on how to set this up, refer to [this guide](https://docs.google.com/document/d/1s7uhKmnxKj25CaB0-92jjikhCXw_Z_P2NmPdRGeyBls).")]
    public class ReactionRoles : TeaInteractiveBase
    {
        private const string IndexSummary = "The index of the reaction-role message. The indexes are available using `{prefix}rr list`.";
        private const string OptionalIndexSummary = IndexSummary + " Leave empty for the latest reaction-role message.";

        private const string CustomSpecificCommand = "This command is only applicable to **custom** reaction-role messages.";
        private const string NonCustomSpecificCommand = "This command is only applicable to **non-custom** reaction-role messages.";

        private const string PermissionNotice = "\n\nIf you can't get roles after placing the needed reactions, look into the bot's permissions or the channel's permissions. It won't work if the bot doesn't have `Manage Roles`, " +
            "or if no reactions are placed on the message after displaying the message, check the `Add Reaction` permission, and so on. After making sure everything is correct, just run the command again.";

        public const string Group = "rr";

        private readonly DatabaseService _database;
        private readonly ReactionRoleService _rrservice;

        public ReactionRoles(DatabaseService database, ReactionRoleService rrservice)
        {
            _database = database;
            _rrservice = rrservice;
        }

        [Command("guide")]
        [Alias("help")]
        [Summary("Detailed walkthrough on how to set up the reaction-role system in your server.")]
        [Ratelimit(4)]
        public async Task Help()
        {
            await ReplyAsync("https://docs.google.com/document/d/1s7uhKmnxKj25CaB0-92jjikhCXw_Z_P2NmPdRGeyBls");
        }

        [Command("list")]
        [Alias("l")]
        [Summary("Shows all reaction-role messages for this guild alongside some other information.")]
        [Ratelimit(4)]
        public async Task List()
        {
            string query = @"
            SELECT reaction_role_messages.get_reaction_role_message(rrid)
            FROM reaction_role_messages.reaction_roles
            WHERE guildid = @gid
            ORDER BY rrid;

            SELECT reaction_role_messages.get_reaction_limit(limitid)
            FROM reaction_role_messages.reaction_limit_relations rlr
            LEFT JOIN reaction_role_messages.reaction_roles rr
            ON rr.rrid = rlr.rrid
            WHERE guildid = @gid
            ";

            await using var cmd = _database.GetCommand(query);
            cmd.Parameters.AddWithValue("gid", (long)Context.Guild.Id);
            await using var reader = await cmd.ExecuteReaderAsync();

            List<ReactionRoleMessage> rrmsgs = new List<ReactionRoleMessage>();
            //for(int i;  await reader.ReadAsync(); i++)
            while (await reader.ReadAsync())
                rrmsgs.Add(await ReactionRoleMessage.CreateAsync(_rrservice, _rrservice.DeserealizeReactionRoleMessage(reader.GetString(0))));

            await reader.NextResultAsync();

            Dictionary<int, ReactionLimits> limits = new Dictionary<int, ReactionLimits>();
            while (await reader.ReadAsync())
            {
                var rl = Newtonsoft.Json.JsonConvert.DeserializeObject<ReactionLimits>(reader.GetString(0));
                limits[rl.LimitId] = rl;
            }

            await reader.CloseAsync();

            List<string> rrmsgsDescriptions = new List<string>();
            for (int i = 0; i < rrmsgs.Count; i++)
            {
                var rrmsg = rrmsgs[i];

                List<string> descriptors = new List<string>
                {
                    $"Pairs: **{rrmsg.EmoteRolePairs.Count}**",
                    rrmsg.Message is null ? "Not displayed" : $"[Message]({rrmsg.Message.GetJumpUrl()})"
                };

                if (rrmsg.LimitId != null)
                {
                    var limit = limits[rrmsg.LimitId.Value];
                    if (limit.ReactionRoleMesageRRIDs.Count() == 1)
                        descriptors.Add($"Limit: **{limit.Limit}**");
                    else
                        descriptors.Add($"Shared limit (see below)");
                }
                else
                    descriptors.Add("No limit");

                if (rrmsg is FullReactionRoleMessage frrmsg)
                {
                    descriptors.Add($"Color: **{frrmsg.Data.Color?.ToString() ?? "Default"}**");
                    rrmsgsDescriptions.Add($"**{i + 1}**. {frrmsg.Data.Name ?? "No name yet!"}\n{string.Join(" | ", descriptors)}");
                }
                else
                {
                    rrmsgsDescriptions.Add($"**{i + 1}**. `Custom message`\n{string.Join(" | ", descriptors)}");
                }
            }

            var embed = new EmbedBuilder();

            List<string> limitsDescriptions = new List<string>();
            foreach (var limit in limits.Values.Where(x => x.ReactionRoleMesageRRIDs.Count() > 1))
            {
                List<int> indexes = new List<int>();
                foreach (int rrid in limit.ReactionRoleMesageRRIDs)
                    indexes.Add(rrmsgs.FindIndex(x => x.RRID == rrid) + 1);

                limitsDescriptions.Add($"Limit: {limit.Limit}\nApplies to: {string.Join(", ", indexes.Select(x => $"**{x}**"))}");
            }

            embed.WithColor(TeaEssentials.MainColor)
                .WithTitle("Reaction-role messages for this server")
                .WithDescription(rrmsgs.Count == 0 ? $"None yet! Create a reaction-role message using {ReactionRoleServiceMessages.ReactionRoleMessageCommandString(Context.Prefix, "create")}." : string.Join("\n\n", rrmsgsDescriptions))
                .AddField("Limits", limitsDescriptions.Count > 0 ? string.Join("\n\n", limitsDescriptions) : $"No chained limits yet! Set a limit to a message using {ReactionRoleServiceMessages.ReactionRoleMessageCommandString(Context.Prefix, "limit")} " +
                $"or chain limits between multiple messages using {ReactionRoleServiceMessages.ReactionRoleMessageCommandString(Context.Prefix, "chainlimit")}")
                .WithFooter($"Use {Context.Prefix}rr info [index] to see detailed info about a reaction-role message");

            await ReplyAsync(embed: embed.Build());
        }

        [Name("Reaction-role messages")]
        public class ReactionRoleMessages : TeaInteractiveBase
        {
            private readonly ReactionRoleService _rrservice;

            public ReactionRoleMessages(ReactionRoleService rrservice)
            {
                _rrservice = rrservice;
            }

            [Command("create")]
            [Alias("c")]
            [Summary("Create an empty reaction-role message. There's a limit of 5 reaction-role messages per guild.")]
            [Ratelimit(10)]
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
            [Ratelimit(10)]
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

            [Command("display")]
            [Alias("d")]
            [Summary("Commit all changes made to a reaction-role message and display it in the specified channel OR modify its properties if it's already present in the channel." + PermissionNotice)]
            [Note(NonCustomSpecificCommand)]
            [Ratelimit(4)]
            public async Task Display(
                [Summary("The channel to display the message in.")] ITextChannel channel,
                [Summary(OptionalIndexSummary)] int? index = null
                )
            {
                try
                {
                    var rrmsg = await _rrservice.DisplayFullReactionRoleMessageAsync(Context.Guild, index, channel);
                    await ReplyAsync($"Successfully displayed {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.\nMessage: {rrmsg.Message.GetJumpUrl()}");
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

            [Command("display")]
            [Alias("d", "displaycustom")]
            [Summary("Commit all changes made to **custom** a reaction-role message and add all reactions to it." + PermissionNotice)]
            [Note(CustomSpecificCommand)]
            [Ratelimit(4)]
            public async Task DisplayCustom(
                [Summary("The link of the message to add reactions and callback to. This is a link of the format `https://discordapp.com/channels/{guild}/{channel}/{id}`." +
                "You can get the link by hovering over the message, going to its `More` tab (the 3 dots) and pressing `Copy Message Link`")] string link,
                [Summary(OptionalIndexSummary)] int? index = null
                )
            {
                IUserMessage message;
                try
                {
                    message = await MessageUtilities.ParseMessageFromLinkAsync(Context, link) as IUserMessage;
                }
                catch (FormatException fe)
                {
                    await ReplyAsync(fe.Message);
                    return;
                }
                catch (ChannelNotFoundException cnfe)
                {
                    await ReplyAsync(cnfe.Message);
                    return;
                }
                catch (HttpException)
                {
                    await ReplyAsync("Couldn't access the channel. Consider changing the channel's permissions.");
                    return;
                }

                if (message is null)
                {
                    await ReplyAsync("The provided link does not lead to any message.");
                    return;
                }

                try
                {
                    var rrmsg = await _rrservice.DisplayCustomReactionRoleMessageAsync(Context.Guild, index, message);
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

            [Command("display")]
            [Summary("Same as the other ones, except this one primarily serves the purpose of updating the reaction-role message, considering it has already been displayed." + PermissionNotice)]
            [Note("This command can be used for both **custom** and **non-custom** messages.")]
            [Ratelimit(4)]
            public async Task Display(
                [Summary(OptionalIndexSummary)] int? index = null
                )
            {
                try
                {
                    await _rrservice.DisplayReactionRoleMessageAsync(Context.Guild, index);
                    await ReplyAsync($"{ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index, true)} has been updated.");
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
                    await ReplyAsync($"Could not access the channel or send the message. Look into the channel's permissions.");
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
                    bool custom = await _rrservice.ToggleCustomAsync(Context.Guild, index);
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
            [Summary("Changes the description of the specified reaction-role message.")]
            [Ratelimit(4)]
            public async Task ChangeReactionRoleMessageDescription(
                [Summary(OptionalIndexSummary)] int? index,
                [Summary("The description to attach to this reaction-role message.\n" +
                "Use `{prefix}rr description delete` or `{prefix}rr description remove` to remove the description.")] [Remainder] string description
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
            [Summary("Changes the description of the latest reaction-role message.")]
            [Ratelimit(4)]
            public async Task ChangeReactionRoleMessageDescription(
                [Summary("The description to attach to this reaction-role message.\n" +
                "Use `{prefix}rr description delete` or `{prefix}rr description remove` to remove the description.")] string description
                ) => await ChangeReactionRoleMessageDescription(null, description);

            [Command("name")]
            [Alias("n")]
            [Summary("Change the name of the specified reaction-role message.")]
            [Ratelimit(4)]
            public async Task SetName(
                [Summary(IndexSummary)] int? index,
                [Summary("The new name to set.\nUse `{prefix}rr name delete` or `{prefix}rr name remove` to set the name back to default.")] [Remainder] string name
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
                ) => await SetName(null, name);

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
        }

        [Name("Emote-role pairs")]
        public class EmoteRolePairs : TeaInteractiveBase
        {
            private readonly ReactionRoleService _rrservice;

            public EmoteRolePairs(ReactionRoleService rrservice)
            {
                _rrservice = rrservice;
            }

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

                try
                {
                    await _rrservice.AddPairAsync(Context.Guild, index, emote, role);
                    await ReplyAsync($"Pair {emote} - `{role.Name}` has been added to {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}.");
                }
                catch (PostgresException pe)
                {
                    await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index)
                        .Replace("{emote}", emote.ToString())
                        .Replace("{role}", $"`{role.Name}`"));
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
                    await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index).Replace("{role}", $"`{role.Name}`"));
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
                    await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index).Replace("{role}", $"`{existingRole.Name}`"));
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

            [Command("pairdescription")]
            [Alias("pd", "pdesc", "pairdesc")]
            [Summary("Changes the description of an emote-role pair in the latest reaction-role message.")]
            [Ratelimit(4)]
            public async Task ChangeEmoteRolePairDescription(
                [Summary("The emote of an existing pair.")] IEmote emote,
                [Summary("The description to attach to the emote-role pair.\n" +
                "Use `{prefix}rr description delete` or `{prefix}rr description remove` to remove the description.")] [Remainder] string description
                ) => await ChangeEmoteRolePairDescription(null, emote, description);

            [Command("pairdescription")]
            [Alias("pd", "pdesc", "pairdesc")]
            [Summary("Changes the description of an emote-role pair.")]
            [Ratelimit(4)]
            public async Task ChangeEmoteRolePairDescription(
                [Summary(IndexSummary)] int? index,
                [Summary("The emote of an existing pair.")] IEmote emote,
                [Summary("The description to attach to the emote-role pair.\n" +
                "Use `{prefix}rr description delete` or `{prefix}rr description remove` to remove the description.")] string description
                )
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
        }

        [Name("Reaction limits")]
        public class ReactionLimitManaging : TeaInteractiveBase
        {
            private readonly ReactionRoleService _rrservice;

            public ReactionLimitManaging(ReactionRoleService rrservice)
            {
                _rrservice = rrservice;
            }

            [Command("limit")]
            [Alias("setlimit, lim")]
            [Summary("Sets the amount of roles a user can get from a reaction-role message.")]
            [Ratelimit(4)]
            public async Task SetLimit(
                [Summary("The limit to set.")] int limit,
                [Summary(OptionalIndexSummary)] int? index = null)
            {
                try
                {
                    await _rrservice.SetLimitAsync(Context.Guild, index, limit);
                    await ReplyAsync($"Successfully set the limit to **{limit}** for {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                }
                catch (PostgresException pe)
                {
                    await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index)
                        .Replace("{limit}", GeneralUtilities.Pluralize(limit, "emote-role pairs"))
                        );
                }
            }

            [Command("removelimit")]
            [Alias("rl", "rlim", "removelim", "remlim", "deletelim", "deletelimit", "dl")]
            [Ratelimit(4)]
            public async Task RemoveLimit(int? index = null)
            {
                try
                {
                    await _rrservice.RemoveLimitAsync(Context.Guild, index);
                    await ReplyAsync($"Successfully removed the limit for {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                }
                catch (PostgresException pe)
                {
                    await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index));
                }
            }

            [Command("chainlimit")]
            [Alias("sharelimit", "chainlim", "sharelim", "cl", "sl")]
            [Ratelimit(4)]
            public async Task ChainLimit(int index1, int index2)
            {
                try
                {
                    bool exists = await _rrservice.ChainLimits(Context.Guild, index1, index2);
                    if (exists)
                        await ReplyAsync($"Successfully chained limits for messages at indexes **{index1}** and **{index2}**");
                    else
                        await ReplyAsync($"No limit exists for {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index1)}");
                }
                catch (PostgresException pe)
                {
                    await ReplyAsync(ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index1));
                }
            }
        }

        [Name("Role restrictions")]
        public class RoleRestrictions : TeaInteractiveBase
        {
            private readonly ReactionRoleService _rrservice;

            public RoleRestrictions(ReactionRoleService rrservice)
            {
                _rrservice = rrservice;
            }

            public enum RestrictionAction
            {
                Add,
                Remove
            }

            public enum RestrictionType
            {
                Allowed,
                Prohibited
            }

            [Command("rolerestrictions")]
            [Alias("restrictions", "restriction", "res", "r")]
            [Summary("Manages role restrictions/limitations of a specific emote-role pair. For more information regarding how this works, refer to the guide found in `{prefix}rr guide`")]
            [Ratelimit(4)]
            public async Task ManageRoleRestrictions(RestrictionAction ra, RestrictionType rt, IEmote emote, IRole role, int? index = null)
            {
                try
                {
                    switch ((ra, rt))
                    {
                        case (RestrictionAction.Add, RestrictionType.Allowed):
                            await _rrservice.AddAllowedRoleAsync(Context.Guild, index, emote, role);
                            await ReplyAsync($"Successfully added `{role.Name}` to allowed roles of emote-role pair with emote {emote} in " +
                                $"{ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            break;

                        case (RestrictionAction.Add, RestrictionType.Prohibited):
                            await _rrservice.AddProhibitedRoleAsync(Context.Guild, index, emote, role);
                            await ReplyAsync($"Successfully added `{role.Name}` to prohibited roles of emote-role pair with emote {emote} in " +
                                $"{ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            break;

                        case (RestrictionAction.Remove, RestrictionType.Allowed):
                            bool deletedAllowed = await _rrservice.RemoveAllowedRoleAsync(Context.Guild, index, emote, role);
                            if (deletedAllowed)
                                await ReplyAsync($"Successfully removed `{role.Name}` from allowed roles of emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            else
                                await ReplyAsync($"`{role.Name}` is not present in the list of allowed roles of emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            break;

                        case (RestrictionAction.Remove, RestrictionType.Prohibited):
                            bool deletedProhibited = await _rrservice.RemoveProhibitedRole(Context.Guild, index, emote, role);
                            if (deletedProhibited)
                                await ReplyAsync($"Successfully removed `{role.Name}` from allowed roles of emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            else
                                await ReplyAsync($"`{role.Name}` is not present in the list of prohibited roles of emote-role pair with emote {emote} in {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            break;
                    }
                }
                catch (PostgresException pe)
                {
                    await ReplyAsync(
                        ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index)
                        .Replace("{role}", $"`{role.Name}`")
                        .Replace("{emote}", emote.ToString())
                        );
                }
            }

            [Command("globalrolerestrictions")]
            [Alias("globalrestrictions", "globalrestriction", "gres", "gr")]
            [Summary("Manages role restrictions/limitations of a reaction-role message. For more information regarding how this works, refer to the guide found in `{prefix}rr guide`")]
            [Ratelimit(4)]
            public async Task ManageGlobalRoleRestrictions(RestrictionAction ra, RestrictionType rt, IRole role, int? index = null)
            {
                try
                {
                    switch ((ra, rt))
                    {
                        case (RestrictionAction.Add, RestrictionType.Allowed):
                            await _rrservice.AddGlobalAllowedRoleAsync(Context.Guild, index, role);
                            await ReplyAsync($"Successfully added `{role.Name}` to global allowed roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            break;

                        case (RestrictionAction.Add, RestrictionType.Prohibited):
                            await _rrservice.AddGlobalProhibitedRoleAsync(Context.Guild, index, role);
                            await ReplyAsync($"Successfully added `{role.Name}` to global prohibited roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            break;

                        case (RestrictionAction.Remove, RestrictionType.Allowed):
                            bool deletedAllowed = await _rrservice.RemoveGlobalAllowedRoleAsync(Context.Guild, index, role);
                            if (deletedAllowed)
                                await ReplyAsync($"Successfully removed `{role.Name}` from global allowed roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            else
                                await ReplyAsync($"`{role.Name}` is not present in the list of global allowed roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            break;

                        case (RestrictionAction.Remove, RestrictionType.Prohibited):
                            bool deletedProhibited = await _rrservice.RemoveGlobalProhibitedRole(Context.Guild, index, role);
                            if (deletedProhibited)
                                await ReplyAsync($"Successfully removed `{ role.Name}` from global prohibited roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            else
                                await ReplyAsync($"`{role.Name}` is not present in the list of global prohibited roles of {ReactionRoleServiceMessages.SpecifyReactionRoleMessage(index)}");
                            break;
                    }
                }
                catch (PostgresException pe)
                {
                    await ReplyAsync(
                        ReactionRoleServiceMessages.GetErrorMessageFromPostgresException(pe, Context.Prefix, index)
                        .Replace("{role}", $"`{role.Name}`")
                        );
                }
            }
        }
    }
}
