using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using TeaBot.Commands;
using TeaBot.Preconditions;
using TeaBot.Utilities;

namespace TeaBot.Modules
{
    [Summary("Commands targeted at entertainment")]
    public class Fun : TeaInteractiveBase
    {
        [Command("gacha", true)]
        [Summary("This one could grow into something bigger, but this is just a pointless command for now.")]
        [Ratelimit(2, Measure.Seconds)]
        public async Task Gacha()
        {
            Random random = new Random();
            int roll = random.Next(1, 100001);
            Emoji die = new Emoji("🎲");

            string pull = $"{Context.User.Mention} rolled ";

            switch (roll)
            {
                case int _ when roll <= 65000:
                    pull += "**COMMON**";
                    break;
                case int _ when roll <= 85000:
                    pull += "**UNCOMMON**";
                    break;
                case int _ when roll <= 95000:
                    pull += "**RARE**";
                    break;
                case int _ when roll <= 99000:
                    pull += "**EPIC**";
                    break;
                case int _ when roll <= 99900:
                    pull += "**LEGENDARY**";
                    break;
                case int _ when roll <= 99990:
                    pull += "**IMPOSSIBLE**";
                    break;
                case int _ when roll <= 99999:
                    pull += "**OMG THIS IS ILLEGAL**";
                    break;
                case int _ when roll == 100000:
                    pull += "**STOP SPAMMING THE GACHA COMMAND THIS IS THE HIGHEST RARITY**";
                    break;
            }
            await ReplyAsync(die + " " + pull);
        }

        [Command("uwufy")]
        [Summary("Don't.")]
        public async Task Uwufy([Remainder] string text)
        {
            var split = text.ToLower().Split(" ").Where(x => x != "").ToArray();
            Random random = new Random();

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < split.Length; j++)
                {
                    if (char.IsLetter(split[j][0]) && random.Next(0, 101) > 85)
                    {
                        split[j] = $"{split[j][0]}-{split[j]}";
                    }
                }
            }

            string result = string.Join(" ", split)
                .Replace('r', 'w')
                .Replace('l', 'w')
                .Replace("na", "nya")
                .Replace("ma", "mya")
                .Replace(".", "!!");

            result = Regex.Replace(result, "sh*", "sh");

            await ReplyAsync(result);
        }

        [Command("waifumeter")]
        [Alias("ratewaifu", "ratewf", "waifurate")]
        [Summary("Get to know how much of a waifu something or someone is")]
        public async Task WaifuMeter([Remainder] string subject)
        {
            subject = subject.DeafenMentions(Context.Message);
            int percentage = 0;
            foreach (char character in subject.ToLower())
            {
                percentage += character;
            }
            percentage = new Random(percentage).Next(0, 101);
            await ReplyAsync($"**{subject}** is **{percentage}%** waifu.");
        }
    }
}
