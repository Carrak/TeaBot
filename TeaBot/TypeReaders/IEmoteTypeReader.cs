using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord;
using System.Linq;

namespace TeaBot.TypeReaders
{
    class IEmoteTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if (context.Guild.Emotes.FirstOrDefault(x => $":{x.Name}:" == input) is Emote e)
                return Task.FromResult(TypeReaderResult.FromSuccess(e));
            else if (Emote.TryParse(input, out var emote))
                return Task.FromResult(TypeReaderResult.FromSuccess(emote));
            else if (NeoSmart.Unicode.Emoji.IsEmoji(input))
                return Task.FromResult(TypeReaderResult.FromSuccess(new Emoji(input)));
            else
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Could not parse emote/emoji"));
        }
    }
}
