using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace TeaBot.TypeReaders
{
    /// <summary>
    ///     TypeReader for Discord colours.
    /// </summary>
    class ColorTypeReader : TypeReader
    {
        /// <inheritdoc/>
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            // Bytes split by a comma
            // Example: 255,255,255 (yields #FFFFFF)
            if (input.Split(',') is string[] arr && arr.Length == 3 &&
                byte.TryParse(arr[0], out var r) && byte.TryParse(arr[1], out var g) && byte.TryParse(arr[2], out var b))
                return Task.FromResult(TypeReaderResult.FromSuccess(new Color(r, g, b)));

            // Hexadecimal RGB representation with the prefixes # or 0x
            // Example: #FFFFFF or 0xFFFFFF
            var match = Regex.Match(input, @"(?<=^(#|0x))[0-9a-f]{6}$", RegexOptions.IgnoreCase);
            if (match.Success && uint.TryParse(match.ToString(), NumberStyles.HexNumber, null, out var x))
                return Task.FromResult(TypeReaderResult.FromSuccess(new Color(x)));

            // Raw value input
            // Example: 16777215 (yields #FFFFFF)
            if (uint.TryParse(input, out var rawValue) && rawValue > 0 && rawValue < 16777216)
                return Task.FromResult(TypeReaderResult.FromSuccess(new Color(rawValue)));

            // If none if these cases is matched, return a ParseFailed error.
            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Couldn't parse the colour."));
        }
    }
}
