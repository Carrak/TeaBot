using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace TeaBot.TypeReaders
{
    /// <summary>
    ///     TypeReader for emotes and emojis.
    /// </summary>
    class IEmoteTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            // Custom emote's name (e.g. :poppo:) 
            // Only works in guilds and is only useful when non-nitro users input animated emojis
            if (context.Guild?.Emotes.FirstOrDefault(x => $":{x.Name.ToLower()}:" == input.ToLower()) is Emote e)
                return Task.FromResult(TypeReaderResult.FromSuccess(e));

            // Same as the previous one, except for all emotes (not necessarily from the given guild)
            else if (Emote.TryParse(input, out var emote))
                return Task.FromResult(TypeReaderResult.FromSuccess(emote));

            // Unicode emoji
            else if (NeoSmart.Unicode.Emoji.IsEmoji(input))
                return Task.FromResult(TypeReaderResult.FromSuccess(new Emoji(input)));

            // None of the matches are matched, return a ParseFailed error
            else
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Could not parse emote/emoji."));
        }
    }
}
