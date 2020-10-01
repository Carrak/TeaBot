using System;
using Npgsql;

namespace TeaBot.Services.ReactionRole
{
    /// <summary>
    ///     Error messages and a few utility methods for ReactionRoleService.
    /// </summary>
    static class ReactionRoleServiceMessages
    {
        private static SupportService _support;

        public static void Init(SupportService support)
        {
            _support = support;
        }

        public static string GetErrorMessageFromPostgresException(PostgresException pe, string prefix, int? index = null)
        {
            switch (pe.SqlState)
            {
                // Class 1: Reaction-role messages
                case "RR010":
                    return "No reaction-role messages exist in this guild.\n" +
                        $"Use {ReactionRoleMessageCommandString(prefix, "create")} to create an empty message or use {ReactionRoleMessageCommandString(prefix, "createcustom")} to create a custom message.";
                case "RR011":
                    return $"No reaction-role message exists at such index. Use {ReactionRoleMessageCommandString(prefix, "list")} to see available reaction-role messages.";
                case "RR012":
                    return $"You've reached the limit of reaction-role messages for this guild.\nUse {ReactionRoleMessageCommandString(prefix, "remove")} to remove existing messages.";

                // Class 2: Emote-role pairs
                case "RR020":
                    return $"No emote-role pairs exist in {SpecifyReactionRoleMessage(index)}\n" +
                        $"Use {ReactionRoleMessageCommandString(prefix, "addpair")} to add emote-role pairs.";
                case "RR021":
                    return $"No emote-role pair with emote {pe.Detail} exists in {SpecifyReactionRoleMessage(index)}.\n" +
                        $"Use {ReactionRoleMessageCommandString(prefix, "info")} to view emote-role pairs of this RR message.";
                case "RR022":
                    return $"No emote-role pair with role {{role}} exists in {SpecifyReactionRoleMessage(index)}.\n" +
                        $"Use {ReactionRoleMessageCommandString(prefix, "info")} to view emote-role pairs of this RR message.";
                case "RR023":
                    return $"This message has reached the maximum of emote-role pairs (20).\n" +
                    $"Create a new reaction-role message using {ReactionRoleMessageCommandString(prefix, "create")} " +
                    $"or remove existing pairs using {ReactionRoleMessageCommandString("removepair", prefix)}.";

                // Class 3: Custom messages
                case "RR030":
                    return $"{SpecifyReactionRoleMessage(index, true)} is custom.\nConsider creating a non-custom message using {ReactionRoleMessageCommandString(prefix, "create")}";

                // Class 4: Role restrictions (global and non-global)
                case "RR040":
                    return $"Emote-role pair with emote {{emote}} in {SpecifyReactionRoleMessage(index)} has reached its limit of role restrictions.\n" +
                        $"Remove existing restrictions using {ReactionRoleMessageCommandString("rolerestrictions", prefix)}";
                case "RR041":
                    return $"{SpecifyReactionRoleMessage(index, true)} has reached its limit of role restrictions.\n" +
                        $"Remove existing restrictions using {ReactionRoleMessageCommandString("globalrolerestrictions", prefix)}";
                case "RR042":
                    return $"{{role}} is already restricted globally for {SpecifyReactionRoleMessage(index)}.";

                // Class 5: Reaction limits
                case "RR050":
                    return $"No limit exists for {SpecifyReactionRoleMessage(index)}.";

                // UNIQUE constraints
                // Unique emote-role pairs
                case "23505" when pe.ConstraintName == "unique_emote_per_rrid":
                    return $"A pair with emote {{emote}} already exists. You can remove existing pairs using {ReactionRoleMessageCommandString(prefix, "removepair")}";
                case "23505" when pe.ConstraintName == "unique_roleid_per_rrid":
                    return $"A pair with role {{role}} already exists. You can remove existing pairs using {ReactionRoleMessageCommandString(prefix, "removepair")}";

                // Unique role limitations
                case "23505" when pe.ConstraintName == "role_restrictions_pairid_roleid_key":
                    return $"A restriction with role {{role}} already exists for emote-role pair with emote {{emote}} in {SpecifyReactionRoleMessage(index)}";
                case "23505" when pe.ConstraintName == "global_role_restrictions_rrid_roleid_key":
                    return $"A restriction with role {{role}} already exists for {SpecifyReactionRoleMessage(index)}";

                // Unique messageid
                case "23505" when pe.ConstraintName == "unique_messageid":
                    return "A reaction-role message is already attached to that message. Specify a different message.";

                default:
                    throw pe;
            }
        }

        public static string SpecifyReactionRoleMessage(int? index, bool capitalize = false) => index.HasValue ?
            $"{(capitalize ? "R" : "r")}eaction-role message at index `{index.Value}`" :
            $"{(capitalize ? "T" : "t")}he latest reaction-role message";

        public static string ReactionRoleMessageCommandString(string prefix, string command, bool escape = true)
        {
            string header = _support.GetCommandHeader($"{Modules.ReactionRoles.Group} {command}");

            if (header is null)
                throw new NotImplementedException();

            string fullCommand = $"{prefix}{header}";
            return escape ? $"`{fullCommand}`" : fullCommand;
        }
    }
}
