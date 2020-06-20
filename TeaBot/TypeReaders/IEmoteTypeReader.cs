using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace TeaBot.TypeReaders
{
    class IEmoteTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if (context.Guild?.Emotes.FirstOrDefault(x => $":{x.Name.ToLower()}:" == input.ToLower()) is Emote e)
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
