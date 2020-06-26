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
        [Ratelimit(3)]
        public async Task Gacha()
        {
            Random random = new Random();
            int roll = random.Next(1, 100001);
            Emoji die = new Emoji("🎲");

            string pull = $"{Context.User.Mention} rolled ";

            if (roll <= 65000)
                pull += "**COMMON**";
            else if (roll <= 85000)
                pull += "**UNCOMMON**";
            else if (roll <= 95000)
                pull += "**RARE**";
            else if (roll <= 99000)
                pull += "**EPIC**";
            else if (roll <= 99900)
                pull += "**LEGENDARY**";
            else if (roll <= 99990)
                pull += "**IMPOSSIBLE**";
            else if (roll <= 99999)
                pull += "**OMG THIS IS ILLEGAL**";
            else if (roll == 100000)
                pull += "**AMIYA (JAME I HOPE UR HAPPY)**";

            await ReplyAsync(die + " " + pull);
        }

        [Command("uwufy")]
        [Summary("twansfowms text uwu")]
        [Ratelimit(3)]
        public async Task Uwufy(
            [Summary("The text to uwufy.")][Remainder] string text
            )
        {
            text = text.DeafenMentionsFromMessage(Context.Message);

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
        [Alias("ratewaifu", "ratewf", "waifurate", "rw")]
        [Summary("Get to know how much of a waifu something or someone is.")]
        [Ratelimit(3)]
        public async Task WaifuMeter(
            [Summary("The subject to rate.")][Remainder] string subject
            )
        {
            subject = subject.DeafenMentionsFromMessage(Context.Message);

            int percentage = 0;
            foreach (char character in subject.ToLower())
            {
                percentage += character;
            }
            percentage = new Random(percentage).Next(0, 101);

            string addon;
            if (percentage == 100)
                addon = "Absolute waifu material!";
            else if (percentage >= 90)
                addon = "Very good!";
            else if (percentage >= 70)
                addon = "Decent waifu!";
            else if (percentage == 69)
                addon = "NICE!";
            else if (percentage >= 50)
                addon = "Not bad, but could be better.";
            else if (percentage >= 30)
                addon = "Kinda wack.";
            else if (percentage >= 10)
                addon = "Hope for the best.";
            else if (percentage > 0)
                addon = "Ew, what is that?";
            else
                addon = "Absolutely terrible, is this even a waifu?";

            await ReplyAsync($"**{subject}** is **{percentage}%** waifu. {addon}");
        }
    }
}
