﻿using System;
using System.Threading.Tasks;
using Discord.Commands;
using TeaBot.Commands;
using TeaBot.Preconditions;

namespace TeaBot.Modules
{
    [RequireNsfw(ErrorMessage = "The channel is not flagged as NSFW!")]
    [Summary("Commands that can only be executed in NSFW channels")]
    public class NSFW : TeaInteractiveBase
    {
        [Command("nhentai")]
        [Alias("nh")]
        [Summary("Sends the specified doujin")]
        [Ratelimit(5)]
        public async Task Nhentai(int doujinID)
        {
            if (doujinID <= 0)
            {
                await ReplyAsync("The ID must be above 0!");
                return;
            }

            await ReplyAsync("https://nhentai.net/g/" + doujinID);
        }

        [Command("nhentai")]
        [Alias("nh")]
        [Summary("Sends a random doujin")]
        [Ratelimit(5)]
        public async Task Nhentai()
        {
            int randomDoujin = new Random().Next(1, 305878);
            await ReplyAsync("https://nhentai.net/g/" + randomDoujin);
            return;
        }

    }
}
