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
        public new readonly Dictionary<IEmote, FullEmoteRolePair> EmoteRolePairs;

        public FullReactionRoleMessage(ReactionRoleMessage rrmsg, IEnumerable<FullEmoteRolePair> fullEmoteRolePairs, ReactionRoleMessageData data) : base(rrmsg.RRService, rrmsg.RRID, rrmsg.Limit, rrmsg.Guild, rrmsg.Channel, rrmsg.Message, rrmsg.EmoteRolePairs, rrmsg.GlobalAllowedRoles, rrmsg.GlobalProhibitedRoles)
        {
            Data = data ?? new ReactionRoleMessageData(null, null, null);
            EmoteRolePairs = fullEmoteRolePairs.ToDictionary(x => x.Emote);
        }

        /// <summary>
        ///     The embed to use for displaying the reaction-role message.     
        /// </summary>
        /// <returns>Predesigned embed.</returns>
        public Embed ConstructEmbed()
        {
            var embed = new EmbedBuilder();

            List<string> emoteRolePairs = new List<string>();

            foreach(var pair in EmoteRolePairs.Values)
            {
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
                $"{(Limit.HasValue ? $"You can select **{Limit.Value}** role{(Limit == 1 ? "" : "s")} from the list.\n\n" : "\n")}" +
                $"{string.Join("\n\n", emoteRolePairs)}")
                .WithFooter("React to give yourself a role from the list.");

            return embed.Build();
        }

        /// <summary>
        ///     Sends the complete reaction-role message to <see cref="ReactionRoleMessage.Channel"/> 
        ///     or 
        ///     modifies the existing message.
        ///     Both ways use <see cref="ConstructEmbed"/> as the message's embed.
        /// </summary>
        public async Task DisplayAsync()
        {
            var embed = ConstructEmbed();

            if (Message is null)
                Message = await Channel.SendMessageAsync(embed: embed);
            else
                await Message.ModifyAsync(x => x.Embed = embed);

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

        public FullEmoteRolePair(EmoteRolePair erp, EmoteRolePairData data) : base(erp.PairId, erp.Emote, erp.Role, erp.AllowedRoles, erp.ProhibitedRoles)
        {
            Data = data ?? new EmoteRolePairData(null);
        }
    }
}
