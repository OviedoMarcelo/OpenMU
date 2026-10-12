// <copyright file="Utf8Text.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.World;

/// <summary>
/// Helps with texts of a fixed length in UTF-8 in messages.
/// </summary>
internal static class Utf8Text
{
    /// <summary>
    /// Shortens the text so that it fits into the maximum number of bytes in UTF-8, without cutting a character.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maximumBytes">The maximum number of bytes.</param>
    /// <returns>The text, shortened if needed.</returns>
    public static string Truncate(string? text, int maximumBytes)
    {
        var result = text ?? string.Empty;
        while (Encoding.UTF8.GetByteCount(result) > maximumBytes)
        {
            result = result[..^1];
        }

        return result;
    }
}
