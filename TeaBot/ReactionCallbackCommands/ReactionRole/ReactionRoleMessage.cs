using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using TeaBot.Main;
using TeaBot.Services.ReactionRole;

namespace TeaBot.ReactionCallbackCommands
{
    public sealed class ReactionRoleMessage
    {
        private readonly ReactionRoleService _rrservice;

        // Message properties
        public IGuild Guild { get; }
        public ITextChannel Channel { get; }
        public IUserMessage Message { get; private set; }
        public Color? Color { get; set; }

        // Reaction-role properties
        public Dictionary<IEmote, EmoteRolePair> EmoteRolePairs;
        public readonly int RRID;
        public string Name { get; set; }

        public ReactionRoleMessage(
            int rrid,
            Dictionary<IEmote, EmoteRolePair> emoterolepairs,
            ReactionRoleService rrservice,
            IGuild guild,
            ITextChannel channel,
            Color? color,
            string name = null,
            IUserMessage message = null)
        {
            _rrservice = rrservice;

            Guild = guild;
            Channel = channel;
            Message = message;
            Color = color;

            EmoteRolePairs = emoterolepairs;
            RRID = rrid;
            Name = name;
        }

        public Embed ConstructEmbed()
        {
            var embed = new EmbedBuilder();

            embed.WithTitle(Name ?? "Select the roles you want to give to yourself.")
                .WithColor(Color ?? TeaEssentials.MainColor)
                .WithDescription(string.Join("\n\n", EmoteRolePairs.Select(x => $"{x.Key} - {x.Value.Role.Mention}")))
                .WithFooter("React to give yourself a role from the list.");

            return embed.Build();
        }

        public async Task AddReactionCallback()
        {
            _ = Task.Run(async () => await Message.AddReactionsAsync(EmoteRolePairs.Keys.ToArray()));
            await _rrservice.AddReactionCallback(Message, this);
        }

        public async Task DisplayAsync()
        {
            var embed = ConstructEmbed();

            // Disable adding reactions if it is possible
            if ((await Guild.GetCurrentUserAsync()).GetPermissions(Channel).ManageRoles)
            {
                var permissions = Channel.GetPermissionOverwrite(Guild.EveryoneRole).Value;
                permissions = permissions.Modify(addReactions: PermValue.Deny);
                await Channel.AddPermissionOverwriteAsync(Guild.EveryoneRole, permissions);
            }

            if (Message is null) 
                Message = await Channel.SendMessageAsync(embed: embed);
            else
                await Message.ModifyAsync(x => x.Embed = embed);

            await AddReactionCallback();
        }

        public async Task TryDeleteMessageAsync()
        {
            try
            {
                await Message.DeleteAsync();
            } 
            catch (HttpException)
            {

            }
        }

        public async Task HandleReactionAdded(SocketReaction reaction)
        {
            if (EmoteRolePairs.TryGetValue(reaction.Emote, out var erp))
            {
                var user = await Guild.GetUserAsync(reaction.User.Value.Id);
                try
                {
                    await user.AddRoleAsync(erp.Role);
                }
                // Discard missing permissions
                catch (HttpException)
                {

                }
            }
        }

        public async Task HandleReactionRemoved(SocketReaction reaction)
        {
            if (EmoteRolePairs.TryGetValue(reaction.Emote, out var erp))
            {
                var user = await Guild.GetUserAsync(reaction.User.Value.Id);
                try
                {
                    await user.RemoveRoleAsync(erp.Role);
                }
                catch (HttpException)
                {

                }
            }
        }
    }

    public sealed class EmoteRolePair
    {
        public IEmote Emote { get; }
        public IRole Role { get; }

        public string Description { get; }
        public IEnumerable<IRole> AllowedRoles;
        public IEnumerable<IRole> ProhibitedRoles;

        public EmoteRolePair(IEmote emote, 
            IRole role 
            //string description = null, 
            //IEnumerable<IRole> allowedRoles = null, 
            //IEnumerable<IRole> prohibitedRoles = null
            )
        {
            Emote = emote;
            Role = role;

            //Description = description;
            //AllowedRoles = allowedRoles ?? new List<IRole>();
            //ProhibitedRoles = prohibitedRoles ?? new List<IRole>();
        }

    }

}
