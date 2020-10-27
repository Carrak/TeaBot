using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        readonly Dictionary<int, ReactionLimits> reactionLimits = new Dictionary<int, ReactionLimits>();

        public async Task UpdateLimitAsync(int limitid)
        {
            string query = @"
            SELECT reaction_role_messages.get_reaction_limit(@lid);
            ";

            await using var cmd = _database.GetCommand(query);
            cmd.Parameters.AddWithValue("lid", limitid);
            await using var reader = await cmd.ExecuteReaderAsync();

            await reader.ReadAsync();

            ReactionLimits rl;

            if (reader.HasRows)
            {
                rl = JsonConvert.DeserializeObject<ReactionLimits>(reader.GetString(0));

                if (!rl.ReactionRoleMesageRRIDs.Any())
                    reactionLimits[limitid] = rl;
            }
        }

        public bool CheckLimitReached(int limitid, IEnumerable<ulong> currentRoleIds)
        {
            var sharedLimit = reactionLimits.GetValueOrDefault(limitid);

            if (sharedLimit is null)
                return false;

            List<IRole> roles = new List<IRole>();
            foreach (var rrid in sharedLimit.ReactionRoleMesageRRIDs)
            {
                var rrmsg = displayedRrmsgs.Values.FirstOrDefault(x => x.RRID == rrid);
                if (rrmsg != null)
                    roles.AddRange(rrmsg.EmoteRolePairs.Values.Select(x => x.Role));
            }

            int count = currentRoleIds.Count(x => roles.Any(y => y.Id == x));

            if (count >= sharedLimit.Limit)
                return true;

            return false;
        }
    }

    sealed class ReactionLimits
    {
        [JsonProperty("limitid")]
        public int LimitId;

        [JsonProperty("reaction_limit")]
        public int Limit;

        [JsonProperty("rrmsgs")]
        public IEnumerable<int> ReactionRoleMesageRRIDs;

        public ReactionLimits(int limitid, int limit, IEnumerable<int> reactionRoleMesages)
        {
            LimitId = limitid;
            Limit = limit;
            ReactionRoleMesageRRIDs = reactionRoleMesages ?? new List<int>();
        }
    }
}
