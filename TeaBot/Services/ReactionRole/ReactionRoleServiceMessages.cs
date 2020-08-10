using Npgsql;

namespace TeaBot.Services.ReactionRole
{
    /// <summary>
    ///     Error messages and a few utility methods for ReactionRoleService.
    /// </summary>
    static class ReactionRoleServiceMessages
    {
        public static string GetErrorMessageFromPostgresException(PostgresException pe, string prefix, int? index = null)
        {
            switch (pe.SqlState)
            {
                // 1. Custom exceptions
                // 1.1 Main
                case "RR008":
                    return "No reaction-role messages exist in this guild.\n" +
                    $"Use {ReactionRoleMessageCommandString(prefix, "create")} to create an empty message or use {ReactionRoleMessageCommandString(prefix, "createcustom")} to create a custom message.";
                case "RR009":
                    return $"No reaction-role message exists at such index. Use {ReactionRoleMessageCommandString(prefix, "list")} to see available reaction-role messages.";
                case "RR010":
                    return $"No emote-role pair with emote {pe.Detail} exists in {SpecifyReactionRoleMessage(index)}.\n" +
                    $"Use {ReactionRoleMessageCommandString(prefix, "info", index)} to view emote-role pairs of this RR message.";
                case "RR011":
                    return $"No emote-role pair with role {{role}} exists in {SpecifyReactionRoleMessage(index)}.\n" +
                    $"Use {ReactionRoleMessageCommandString(prefix, "info", index)} to view emote-role pairs of this RR message.";
                case "RR012":
                    return $"No emote-role pairs exist in {SpecifyReactionRoleMessage(index)}\n" +
                        $"Use {ReactionRoleMessageCommandString(prefix, "addpair [emote] [role]")} to add emote-role pairs.";

                // 1.2 Custom messages
                case "RR013":
                    return $"{SpecifyReactionRoleMessage(index, true)} is custom.\nConsider creating a non-custom message using {ReactionRoleMessageCommandString(prefix, "create")}";
                case "RR014":
                    return $"{SpecifyReactionRoleMessage(index, true)} is custom.\nTo display a custom message, use {ReactionRoleMessageCommandString(prefix, "displaycustom [message link]", index)} instead. That will also set the channel.";
                case "RR015":
                    return $"{SpecifyReactionRoleMessage(index, true)} is not custom.\nConsider using {ReactionRoleMessageCommandString(prefix, "display [channel]", index)} instead.";

                // 1.3 Count
                // 1.3.1 Reaction-role messages and emote-role pairs
                case "RR001":
                    return $"You've reached the limit of reaction-role messages for this guild.\nUse {ReactionRoleMessageCommandString(prefix, "remove <index>")} to remove existing messages.";
                case "RR002":
                    return $"This message has reached the maximum of emote-role pairs (20).\n" +
                    $"Create a new reaction-role message using {ReactionRoleMessageCommandString(prefix, "create")} " +
                    $"or remove existing pairs using {ReactionRoleMessageCommandString("removepair [emote]", prefix, index)}.";

                // 1.3.2 Role limitations
                case "RR016":
                    return $"Limit of allowed roles for this emote-role pair has been reached.\nYou can remove roles from this list using {ReactionRoleMessageCommandString(prefix, "removeallowedrole [emote] [role]", index)}";
                case "RR017":
                    return $"Limit of prohibited roles for this emote-role pair has been reached.\nYou can remove roles from this list using {ReactionRoleMessageCommandString(prefix, "removeprohibitedrole [emote] [role]", index)}";
                case "RR018":
                    return $"Limit of global allowed roles for {SpecifyReactionRoleMessage(index)} has been reached.\nYou can remove roles from this list using {ReactionRoleMessageCommandString(prefix, "removeglobalallowedrole [emote] [role]", index)}";
                case "RR019":
                    return $"Limit of global prohibted roles for {SpecifyReactionRoleMessage(index)} has been reached.\nYou can remove roles from this list using {ReactionRoleMessageCommandString(prefix, "removeglobalprohibitedrole [emote] [role]", index)}";

                // 1.4 Role limitations
                case "RR003":
                    return $"`{{role}}` cannot be added because it is in the list of allowed roles of emote-role pair with emote {{emote}} in {SpecifyReactionRoleMessage(index)}";
                case "RR004":
                    return $"`{{role}}` cannot be added because it is in the list of prohibited roles of emote-role pair with emote {{emote}} in {SpecifyReactionRoleMessage(index)}";
                case "RR005":
                    return $"`{{role}}` cannot be added because it is in the list of global allowed roles of {SpecifyReactionRoleMessage(index)}";
                case "RR006":
                    return $"`{{role}}` cannot be added because it is in the list of global prohibited roles of {SpecifyReactionRoleMessage(index)}";
                case "RR007":
                    return $"`{{role}}` cannot be added because it is in the list of emote-role pairs of {SpecifyReactionRoleMessage(index)}";

                // 1.5 Reaction limit
                case "RR020":
                    return $"The specififed limit is higher than the amount of emote-role pairs in {SpecifyReactionRoleMessage(index)} [{{limit}}]";

                // 2. UNIQUE constraints
                // 2.1 Unique emote-role pairs
                case "23505" when pe.ConstraintName == "unique_emote_per_rrid":
                    return $"A pair with this emote already exists. You can remove existing pairs using {ReactionRoleMessageCommandString(prefix, "removepair [emote]", index)}";
                case "23505" when pe.ConstraintName == "unique_roleid_per_rrid":
                    return $"A pair with this role already exists. You can remove existing pairs using {ReactionRoleMessageCommandString(prefix, "removepair [emote]", index)}";

                // 2.2 Unique role limitations
                case "23505" when pe.ConstraintName == "ar_unique_pairid_roleid":
                    return $"`{{role}}` is already in the list of allowed roles of emote-role pair with emote {{emote}} in {SpecifyReactionRoleMessage(index)}";
                case "23505" when pe.ConstraintName == "pr_unique_pairid_roleid":
                    return $"`{{role}}` is already in the list of prohibited roles of emote-role pair with emote {{emote}} in {SpecifyReactionRoleMessage(index)}";
                case "23505" when pe.ConstraintName == "gar_unique_rrid_roleid":
                    return $"`{{role}}` is already in the list of global allowed roles of {SpecifyReactionRoleMessage(index)}";
                case "23505" when pe.ConstraintName == "gpr_unique_rrid_roleid":
                    return $"`{{role}}` is already in the list of global prohibited roles of {SpecifyReactionRoleMessage(index)}";

                // 2.3 Unique messageid
                case "23505" when pe.ConstraintName == "unique_messageid":
                    return "A reaction-role message is already attached to that message. Consider picking a different message.";

                // 3. CHECK constraints
                case "23514" when pe.ConstraintName == "reaction_roles_reaction_limit_check":
                    return $"Limit cannot be negative. Set limit to 0 to remove it.";
            }

            System.Console.WriteLine($"Query: {pe.InternalQuery}");
            System.Console.WriteLine($"Location: {pe.Where}");
            throw pe;
        }

        public static string SpecifyReactionRoleMessage(int? index, bool capitalize = false) => index.HasValue ?
            $"{(capitalize ? "R" : "r")}eaction-role message at index `{index.Value}`" :
            $"{(capitalize ? "T" : "t")}he latest reaction-role message";

        public static string ReactionRoleMessageCommandString(string prefix, string command, int? index, bool escape = true)
        {
            string fullCommand = $"{ReactionRoleMessageCommandString(prefix, command, false)}{(index.HasValue ? $" {index.Value}" : "")}";
            return escape ? $"`{fullCommand}`" : fullCommand;
        }

        public static string ReactionRoleMessageCommandString(string prefix, string command, bool escape = true)
        {
            string fullCommand = $"{prefix}rr {command}";
            return escape ? $"`{fullCommand}`" : fullCommand;
        }
    }
}
