using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using TeaBot.Main;

namespace TeaBot.ReactionCallbackCommands.ReactionRole
{
    /// <summary>
    ///     Represents a complete, non-custom reaction-role message that utilizes the predefined design for the visual part of reaction-role messages.
    /// </summary>
    public class FullReactionRoleMessage : ReactionRoleMessage
    {
        public readonly ReactionRoleMessageData Data;

        public new Dictionary<IEmote, FullEmoteRolePair> EmoteRolePairs
        {
            get
            {
                return base.EmoteRolePairs.Values.Cast<FullEmoteRolePair>().ToDictionary(x => x.Emote, x => x);
            }
        }

        public FullReactionRoleMessage(ReactionRoleMessage rrmsg, ReactionRoleMessageData data) : base(rrmsg.RRService, rrmsg.RRID, rrmsg.LimitId, rrmsg.Guild, rrmsg.Message, rrmsg.EmoteRolePairs, rrmsg.GlobalAllowedRoles, rrmsg.GlobalProhibitedRoles)
        {
            Data = data ?? new ReactionRoleMessageData(null, null, null);
        }

        /// <summary>
        ///     The embed to use for displaying the reaction-role message.     
        /// </summary>
        /// <returns>Predesigned embed.</returns>
        public Embed ConstructEmbed()
        {
            var embed = new EmbedBuilder();

            List<string> emoteRolePairs = new List<string>();

            foreach (var pair in EmoteRolePairs.Values)
            {
                if (pair.Blocked)
                    continue;

                string toAdd = "";

                int allowedRolesCount = pair.AllowedRoles.Count();
                int prohibitedRolesCount = pair.ProhibitedRoles.Count();

                if (allowedRolesCount > 0 && prohibitedRolesCount > 0)
                    toAdd += $"If you have {string.Join(", ", pair.AllowedRoles.Select(x => x.Mention))}, " +
                        $"but don't have {string.Join(", ", pair.ProhibitedRoles.Select(x => x.Mention))}, you can select:\n";
                else if (allowedRolesCount > 0)
                    toAdd += $"If you have {string.Join(", ", pair.AllowedRoles.Select(x => x.Mention))}, you can select:\n";
                else if (prohibitedRolesCount > 0)
                    toAdd += $"If you don't have {string.Join(", ", pair.ProhibitedRoles.Select(x => x.Mention))}, you can select:\n";

                toAdd += $"{pair.Emote} - {pair.Role.Mention}";

                if (pair.Data != null)
                {
                    toAdd += $"\n{pair.Data.Description}";
                }

                emoteRolePairs.Add(toAdd);
            }

            string globalRoleLimitations = null;
            if (GlobalAllowedRoles.Any() && GlobalProhibitedRoles.Any())
                globalRoleLimitations = $"This list is exclusively for people who have {string.Join(", ", GlobalAllowedRoles.Select(x => x.Mention))}, but don't have {string.Join(", ", GlobalProhibitedRoles.Select(x => x.Mention))}";
            else if (GlobalAllowedRoles.Any())
                globalRoleLimitations = $"This list is exclusively for people who have {string.Join(", ", GlobalAllowedRoles.Select(x => x.Mention))}";
            else if (GlobalProhibitedRoles.Any())
                globalRoleLimitations = $"This list is exclusively for people who don't have {string.Join(", ", GlobalProhibitedRoles.Select(x => x.Mention))}";

            embed.WithTitle(Data.Name ?? "Select the roles you want to give to yourself.")
                .WithColor(Data.Color ?? TeaEssentials.MainColor)
                .WithDescription($"{(string.IsNullOrEmpty(Data.Description) ? "" : $"{Data.Description}")}" +
                $"{(string.IsNullOrEmpty(globalRoleLimitations) ? "" : $"\n{globalRoleLimitations}")}\n" +
                $"{(LimitId.HasValue ? $"You can select **{LimitId.Value}** role{(LimitId == 1 ? "" : "s")} from the list.\n\n" : "\n")}" +
                $"{string.Join("\n\n", emoteRolePairs)}")
                .WithFooter("React to give yourself a role from the list.");

            return embed.Build();
        }

        /// <summary>
        ///     Sends the complete reaction-role message to <paramref name="channel"/> if no message is currently present of it's in a different channel
        ///     or 
        ///     modifies the existing message.
        ///     Both ways use <see cref="ConstructEmbed"/> as the message's embed.
        /// </summary>
        public async Task DisplayAsync(ITextChannel channel = null)
        {
            var embed = ConstructEmbed();

            switch ((Message is null, channel is null))
            {
                case (true, true):
                    return;

                case (false, false) when Message.Channel.Id != channel.Id:
                case (true, false):
                    Message = await channel.SendMessageAsync(embed: embed);
                    break;

                case (false, true):
                    await Message.ModifyAsync(x => x.Embed = embed);
                    break;
            }

            await AddReactionCallbackAsync();
        }

        /// <summary>
        ///     Attempt to delete the message of the reaction-role message
        /// </summary>
        /// <returns></returns>
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
    }

    /// <summary>
    ///     Represents a complete representation of <see cref="EmoteRolePair"/> for non-custom, full messages.
    /// </summary>
    public sealed class FullEmoteRolePair : EmoteRolePair
    {
        public readonly EmoteRolePairData Data;

        public FullEmoteRolePair(EmoteRolePair erp, EmoteRolePairData data) : base(erp.PairId, erp.Emote, erp.Role, erp.AllowedRoles, erp.ProhibitedRoles, erp.Blocked)
        {
            Data = data ?? new EmoteRolePairData(null);
        }
    }
}
