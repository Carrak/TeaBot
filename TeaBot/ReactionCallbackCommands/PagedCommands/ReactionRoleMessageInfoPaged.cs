using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Addons.Interactive;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.ReactionCallbackCommands.PagedCommands.Base;
using TeaBot.ReactionCallbackCommands.ReactionRole;
using TeaBot.Services.ReactionRole;

namespace TeaBot.ReactionCallbackCommands.PagedCommands
{
    class ReactionRoleMessageInfoPaged : FragmentedPagedMessage<EmbedFieldBuilder>
    {
        private readonly ReactionRoleMessage _rrmsg;
        private readonly int? _index;

        private const string emptyPlaceholder = "**-**";

        public ReactionRoleMessageInfoPaged(InteractiveService interactive,
            TeaCommandContext context,
            ReactionRoleMessage rrmsg,
            int? index)
            : base(interactive, context, GetFields(rrmsg), 4)
        {
            _rrmsg = rrmsg;
            _index = index;
        }

        protected override Embed ConstructEmbed(IEnumerable<EmbedFieldBuilder> currentPage)
        {
            EmbedBuilder embed;

            if (_rrmsg is FullReactionRoleMessage fullrrmsg)
            {
                embed = GetFullReactionRoleMessageEmbed(fullrrmsg);
            }
            else
            {
                embed = GetCustomReactionRoleMessageEmbed(_rrmsg);
            }

            embed.WithFields(currentPage);
            embed = SetDefaultFooter(embed);

            return embed.Build();
        }

        private EmbedBuilder GetFullReactionRoleMessageEmbed(FullReactionRoleMessage fullrrmsg)
        {
            var embed = new EmbedBuilder();

            embed.WithColor(fullrrmsg.Data.Color ?? TeaEssentials.MainColor)
                    .WithTitle(_index.HasValue ? $"RR Message at index {_index.Value}" : "Latest RR Message")
                    .WithFooter(fullrrmsg.EmoteRolePairs.Any() ?
                        $"Use {Context.Prefix}rr preview{(_index.HasValue ? $" {_index.Value}" : "")} to see how this message would look like when displayed" :
                        $"This reaction-role message does not have any emote-role pairs.\n" +
                        $"Use {ReactionRoleServiceMessages.ReactionRoleMessageCommandString(Context.Prefix, "addpair [emote] [role]", _index, false)} to add emote-role pairs.")
                    .AddField("Color", fullrrmsg.Data.Color.HasValue ? fullrrmsg.Data.Color.ToString() : $"Default color ({TeaEssentials.MainColor})", true)
                    .AddField("Name", fullrrmsg.Data.Name ?? emptyPlaceholder, true)
                    .AddField("Channel", fullrrmsg.Channel?.Mention ?? emptyPlaceholder, true)
                    .AddField("Message", fullrrmsg.Message is null ? emptyPlaceholder : $"[Click here to jump to the message!]({fullrrmsg.Message.GetJumpUrl()})")
                    .AddField("Limit (how many roles can be assigned)", fullrrmsg.Limit?.ToString() ?? emptyPlaceholder)
                    .AddField("Role restrictions/limitations",
                    $"Global allowed roles:  {(fullrrmsg.GlobalAllowedRoles.Any() ? string.Join(", ", fullrrmsg.GlobalAllowedRoles.Select(x => x.Mention)) : emptyPlaceholder)}\n" +
                    $"Global prohibited roles: {(fullrrmsg.GlobalProhibitedRoles.Any() ? string.Join(", ", fullrrmsg.GlobalProhibitedRoles.Select(x => x.Mention)) : emptyPlaceholder)}")
                    .AddField("Pairs", fullrrmsg.EmoteRolePairs.Count);

            return embed;
        }

        private EmbedBuilder GetCustomReactionRoleMessageEmbed(ReactionRoleMessage rrmsg)
        {
            var embed = new EmbedBuilder();

            embed.WithColor(TeaEssentials.MainColor)
                    .WithTitle(_index.HasValue ? $"Custom RR Message at index {_index.Value}" : "Latest RR Message (Custom)")
                    .AddField("Channel", rrmsg.Channel?.Mention ?? emptyPlaceholder, true)
                    .AddField("Message", rrmsg.Message is null ? emptyPlaceholder : $"[Click here to jump to the message!]({rrmsg.Message.GetJumpUrl()})", true)
                    .AddField("Limit (how many roles can be assigned)", rrmsg.Limit?.ToString() ?? emptyPlaceholder)
                    .AddField("Role restrictions/limitations",
                    $"Global allowed roles:  {(rrmsg.GlobalAllowedRoles.Any() ? string.Join(", ", rrmsg.GlobalAllowedRoles.Select(x => x.Mention)) : emptyPlaceholder)}\n" +
                    $"Global prohibited roles: {(rrmsg.GlobalProhibitedRoles.Any() ? string.Join(", ", rrmsg.GlobalProhibitedRoles.Select(x => x.Mention)) : emptyPlaceholder)}")
                    .AddField("Pairs", rrmsg.EmoteRolePairs.Count);

            if (!rrmsg.EmoteRolePairs.Any())
                embed.WithFooter($"This reaction-role message does not have any emote-role pairs.\n" +
                    $"Use {ReactionRoleServiceMessages.ReactionRoleMessageCommandString(Context.Prefix, "addpair [emote] [role]", _index, false)} to add emote-role pairs.");

            return embed;
        }

        private static IEnumerable<EmbedFieldBuilder> GetFields(ReactionRoleMessage rrmsg)
        {
            if (rrmsg is FullReactionRoleMessage frrmsg)
            {
                var fullEmoteRolePairsFields = frrmsg.EmoteRolePairs.Values.Select((x, index) => new EmbedFieldBuilder
                {
                    Name = $"Emote-role pair {index + 1}",
                    Value = $"{x.Emote} - {x.Role.Mention}\n" +
                            $"Description: {x.Data?.Description ?? emptyPlaceholder}\n" +
                            $"Allowed roles: {(x.AllowedRoles.Count() > 0 ? string.Join(", ", x.AllowedRoles.Select(x => x.Mention)) : emptyPlaceholder)}\n" +
                            $"Prohibited roles: {(x.ProhibitedRoles.Count() > 0 ? string.Join(", ", x.ProhibitedRoles.Select(x => x.Mention)) : emptyPlaceholder)}"
                });
                return fullEmoteRolePairsFields;
            }
            else
            {
                var emoteRolePairsFields = rrmsg.EmoteRolePairs.Values.Select((x, index) => new EmbedFieldBuilder
                {
                    Name = $"Emote-role pair {index + 1}",
                    Value = $"{x.Emote} - {x.Role.Mention}\n" +
                            $"Allowed roles: {(x.AllowedRoles.Count() > 0 ? string.Join(", ", x.AllowedRoles.Select(x => x.Mention)) : emptyPlaceholder)}\n" +
                            $"Prohibited roles: {(x.ProhibitedRoles.Count() > 0 ? string.Join(", ", x.ProhibitedRoles.Select(x => x.Mention)) : emptyPlaceholder)}"
                });
                return emoteRolePairsFields;
            }

        }
    }
}
