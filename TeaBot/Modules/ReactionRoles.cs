using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.ReactionCallbackCommands;
using TeaBot.Services;
using TeaBot.Services.ReactionRole;
using TeaBot.Attributes;
using TeaBot.Preconditions;

namespace TeaBot.Modules
{
    [Name("ReactionRole")]
    [Group("rr")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.Administrator)]
    [Summary("Commands for managing the reaction-role system")]
    public class ReactionRoles : TeaInteractiveBase
    {
        private readonly DatabaseService _database;
        private readonly ReactionRoleService _rrservice;

        public ReactionRoles(DatabaseService database, ReactionRoleService rrservice)
        {
            _database = database;
            _rrservice = rrservice;
        }

        [Command("info")]
        [Alias("i")]
        [Summary("Gets the information/properties of a reaction-role message.")]
        [Ratelimit(4)]
        public async Task Info(
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            ReactionRoleMessage rrmsg;
            try
            {
                rrmsg = await _rrservice.GetReactionRoleMessageAsync(Context.Guild, index);
            }
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
                return;
            }

            var embed = new EmbedBuilder();

            embed.WithColor(rrmsg.Color ?? TeaEssentials.MainColor)
                .WithTitle(index.HasValue ? $"RR Message at index {index.Value} info" : "Latest RR Message info")
                .WithFooter($"Use {Context.Prefix}rr preview{(index.HasValue ? $" {index.Value}" : "")} to see how this message would look like when displayed")
                .AddField("Color", rrmsg.Color.HasValue ? rrmsg.Color.ToString() : $"Default color ({TeaEssentials.MainColor})")
                .AddField("Name", rrmsg.Name ?? "-")
                .AddField("Channel", rrmsg.Channel?.Mention ?? "-")
                .AddField("Message", rrmsg.Message is null ? "-" : $"[Click here to jump to the message!]({rrmsg.Message.GetJumpUrl()})")
                .AddField("Emote-role pairs", rrmsg.EmoteRolePairs.Count > 0 ? string.Join("\n", rrmsg.EmoteRolePairs.Select(x => $"{x.Key} - {x.Value.Role.Mention}")) : "-");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("preview")]
        [Alias("p")]
        [Summary("Take a look at the message would look like after being displayed.")]
        [Ratelimit(4)]
        public async Task Preview(
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            ReactionRoleMessage rrmsg;
            try
            {
                rrmsg = await _rrservice.GetReactionRoleMessageAsync(Context.Guild, index);
            } 
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
                return;
            }

            if (rrmsg.EmoteRolePairs.Count == 0)
            {
                await ReplyAsync("This reaction-role message does not contain any emote-role pairs. Add them before viewing the preview.");
                return;
            }    

            await ReplyAsync("This is how this message would look like:", embed: rrmsg.ConstructEmbed());
        }

        [Command("list")]
        [Alias("l")]
        [Summary("Displays all reaction-role messages for this guild.")]
        [Ratelimit(4)]
        public async Task List()
        {
            string query = $"SELECT name FROM reaction_role_messages.reaction_roles WHERE guildid=@gid ORDER BY rrid";
            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)Context.Guild.Id);

            await using var reader = await cmd.ExecuteReaderAsync();

            List<string> rrmsgs = new List<string>();
            for (int index = 1; await reader.ReadAsync(); index++)
                rrmsgs.Add($"`{index}`. {(await reader.IsDBNullAsync(0) ? "`No name for this reaction role message!`" : reader.GetString(0))}");

            var embed = new EmbedBuilder();

            embed.WithColor(TeaEssentials.MainColor)
                .WithTitle("Reaction role messages for this server")
                .WithDescription(rrmsgs.Count == 0 ? $"None yet!" : string.Join("\n", rrmsgs))
                .WithFooter($"Use {Context.Prefix}rr create to create an empty reaction-role message");

            await ReplyAsync(embed: embed.Build());

        }

        [Command("display")]
        [Alias("d")]
        [Summary("Commit all changes made to a reaction-role message and display it in the message's channel OR modify its properties if it's already present in the channel.")]
        [Note("If you can't get roles after placing the needed reactions, look into the bot's permissions or the channel's permissions. It won't work if the bot doesn't have `Manage Roles`, " +
            "or if no reactions are placed on the message after displaying the message, check the `Add Reaction` permission, and so on. After these actions, just run the command again.")]
        [Ratelimit(4)]
        public async Task Display(
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            try
            {
                var rrmsg = await _rrservice.UpdateOrDisplayReactionRoleMessageAsync(Context.Guild, index);
                await ReplyAsync($"Displayed {(index.HasValue ? $"reaction-role message with index `{index.Value}`" : "the latest reaction-role message")} in channel <#{rrmsg.Channel.Id}>.");
            }
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
            catch (HttpException)
            {
                await ReplyAsync($"Could not access the channel or send the message.\n" +
                    $"Check the bot's channel permissions or set a different channel using `{Context.Prefix}channel [channel]{(index.HasValue ? $" {index.Value}" : "")}`");
            }
        }

        [Command("display")]
        [Alias("d")]
        [Summary("Commit all changes made to a reaction-role message and display it in the given channel OR modify its properties if it's already present in the channel..")]
        [Note("If you can't get roles after placing the needed reactions, look into the bot's permissions or the channel's permissions. It won't work if the bot doesn't have `Manage Roles`, " +
            "or if no reactions are placed on the message after displaying the message, check the `Add Reaction` permission, and so on. After these actions, just run the command again.")]
        [Ratelimit(4)]
        public async Task Display(
            [Summary("The channel to display the reaction-role message in.")] ITextChannel channel,
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            try
            {
                await _rrservice.ChangeChannelAsync(Context.Guild, index, channel.Id);
                var newRrmsg = await _rrservice.UpdateOrDisplayReactionRoleMessageAsync(Context.Guild, index);
                await ReplyAsync($"Displayed {ReactionRoleService.SpecifyReactionRoleMessage(index)} in channel <#{newRrmsg.Channel.Id}>.");
            }
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
            catch (HttpException)
            {
                await ReplyAsync($"Could not access the channel or send the message.\n" +
                    $"Check the bot's channel permissions or set a different channel using `{Context.Prefix}channel [channel]{(index.HasValue ? $" {index.Value}" : "")}`");
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
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        }

        [Command("delete")]
        [Alias("remove", "d", "r")]
        [Summary("Entirely deletes a reaction-role message with all of its contents.")]
        [Ratelimit(4)]
        public async Task Delete(
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            try 
            { 
                await _rrservice.RemoveReactionRoleMessage(Context.Guild, index);
                await ReplyAsync($"Successfully removed {ReactionRoleService.SpecifyReactionRoleMessage(index)}.");
            }
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        }

        [Command("changeemote")]
        [Alias("ce")]
        [Summary("Changes the emote of an emote-role pair.")]
        [Ratelimit(4)]
        public async Task ChangeEmote(
            [Summary("The existing role in emote-role pairs of a reaction role message. (this field remains the same after the change)")] IRole existingRole, 
            [Summary("The new emote to set.")] IEmote newEmote,
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            ) 
            => await ChangeEmote(newEmote, existingRole, index);

        [Command("changeemote")]
        [Alias("ce")]
        [Summary("Changes the emote of an emote - role pair.")]
        [Ratelimit(4)]
        public async Task ChangeEmote(
            [Summary("The new emote to set.")] IEmote newEmote,
            [Summary("The existing role in emote-role pairs of a reaction role message. (this field remains the same after the change)")] IRole existingRole,
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            try
            {
                await _rrservice.ChangeEmote(Context.Guild, index, existingRole, newEmote);
                await ReplyAsync($"Successfully changed the emote for role `{existingRole.Name}` to {newEmote} in {ReactionRoleService.SpecifyReactionRoleMessage(index)}.");
            } 
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        }

        [Command("changerole")]
        [Alias("cr")]
        [Summary("Changes the role of an emote-role pair.")]
        [Ratelimit(4)]
        public async Task ChangeRole(
            [Summary("The new role to set.")] IRole newRole, 
            [Summary("The existing emote in emote-role pairs of a reaction role message. (this field remains the same after the change)")] IEmote existingEmote, 
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            try
            {
                await _rrservice.ChangeRole(Context.Guild, index, newRole, existingEmote);
                await ReplyAsync($"Successfully changed the role for emote {existingEmote} to `{newRole.Name}` in {ReactionRoleService.SpecifyReactionRoleMessage(index)}.");
            }
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        }

        [Command("changerole")]
        [Alias("cr")]
        [Summary("Changes the role of an emote-role pair.")]
        [Ratelimit(4)]
        public async Task ChangeRole(
            [Summary("The existing emote in emote-role pairs of a reaction role message. (this field remains the same after the change)")] IEmote existingEmote,
            [Summary("The new role to set.")] IRole newRole,
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            ) 
            => await ChangeRole(newRole, existingEmote, index);

        [Command("addpair")]
        [Alias("ap")]
        [Summary("Adds an emote-role pair to a reaction-role message.")]
        [Ratelimit(4)]
        public async Task AddPair(
            [Summary("The emote of the reaction-role pair.")] IEmote emote, 
            [Summary("The role of the reaction-role pair.")] IRole role,
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
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
                await ReplyAsync($"Successfully added the pair to {ReactionRoleService.SpecifyReactionRoleMessage(index)}.");
            } 
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        }

        [Command("removepair")]
        [Alias("rp")]
        [Summary("Removes a pair with the given emote from a reaction-role message.")]
        [Ratelimit(4)]
        public async Task RemovePair(
            [Summary("The emote of the reaction-role pair.")] IEmote emote,
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            try
            {
                await _rrservice.RemovePairAsync(Context.Guild, index, emote);
                await ReplyAsync($"Successfully removed the pair with emote {emote} from {ReactionRoleService.SpecifyReactionRoleMessage(index)}.");
            }
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        }

        [Command("removepair")]
        [Alias("rp")]
        [Summary("Removes a pair with the given role from a reaction-role message.")]
        [Ratelimit(4)]
        public async Task RemovePair(
            [Summary("The role of the reaction-role pair.")] IRole role,
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            try
            {
                await _rrservice.RemovePairAsync(Context.Guild, index, role);
                await ReplyAsync($"Successfully removed the pair with role `{role.Name}` from {ReactionRoleService.SpecifyReactionRoleMessage(index)}.");
            }
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        }

        [Command("channel")]
        [Alias("ch")]
        [Summary("Changes the channel of a reaction-role message.")]
        [Ratelimit(4)]
        public async Task ChangeChannel(
            [Summary("The new channel to set. This is where the message will be displayed after using `{prefix}rr display`")] ITextChannel channel,
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            try 
            {
                await _rrservice.ChangeChannelAsync(Context.Guild, index, channel.Id);
                await ReplyAsync($"Successfully changed the channel to {channel.Mention} for {ReactionRoleService.SpecifyReactionRoleMessage(index)}.");
            }
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        }

        [Command("name")]
        [Alias("n")]
        [Summary("Change the name of the latest reaction-role message.")]
        [Ratelimit(4)]
        public async Task SetName(
            [Summary("The new name to set.")] [Remainder] string name
            )
        {
            try { 
                await _rrservice.ChangeNameAsync(Context.Guild, null, name);
                await ReplyAsync($"Successfully changed the name for the latest reaction-role message.\nNew name: {name}");
            }
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        }

        [Command("name")]
        [Alias("n")]
        [Summary("Change the name of the reaction-role message at a given index.")]
        [Ratelimit(4)]
        public async Task SetName(
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`.")] int index, 
            [Summary("The new name to set.")] [Remainder] string name
            )
        {
            try
            {
                await _rrservice.ChangeNameAsync(Context.Guild, index, name);
                await ReplyAsync($"Successfully changed the name for reaction-role message with index `{index}`.\nNew name: {name}");
            }
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        }

        [Command("color")]
        [Alias("colour", "col")]
        [Summary("Modify the color of a reaction-role message's embed.")]
        [Ratelimit(4)]
        public async Task SetColor(
            [Summary("The new color to set.")] Color color,
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            try 
            { 
                await _rrservice.ChangeColorAsync(Context.Guild, index, color == TeaEssentials.MainColor ? null : (Color?) color);
                await ReplyAsync($"Successfully changed the color to {color} for {ReactionRoleService.SpecifyReactionRoleMessage(index)}.");
            }
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        } 

        [Command("swap")]
        [Alias("reorder", "order", "changeorder")]
        [Summary("Swaps the positions of emote-role pairs in a reaction-role message.")]
        [Ratelimit(4)]
        public async Task ChangeOrder(
            [Summary("The emote of a reaction-role pair to swap.")] IEmote emote1, 
            [Summary("The emote of a reaction-role pair to swap.")] IEmote emote2,
            [Summary("The index of the reaction-role message. The indexes are available using `{prefix}rr list`. Leave empty for the latest reaction-role message.")] int? index = null
            )
        {
            try
            {
                await _rrservice.ChangeOrder(Context.Guild, index, emote1, emote2);
                await ReplyAsync($"Successfully swapped the order of {emote1} and {emote2} in {ReactionRoleService.SpecifyReactionRoleMessage(index)}.");
            } 
            catch (ReactionRoleServiceException rrse)
            {
                await ReplyAsync(rrse.Message.Replace("{prefix}", Context.Prefix));
            }
        }
    }
}
