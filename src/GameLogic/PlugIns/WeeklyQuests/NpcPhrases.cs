// <copyright file="NpcPhrases.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;

using System.Text;

/// <summary>
/// Splits the text of an NPC (<see cref="WeeklyQuestObjective.NpcText"/>) into the phrases which are shown one after another above the NPC.
/// </summary>
public static class NpcPhrases
{
    /// <summary>
    /// The description of the text field in the admin panel.
    /// </summary>
    public const string FieldDescription = "Solo para \"Hablar con NPC\". Lo que dice el NPC (globo sobre su cabeza) cuando la charla cuenta para el paso. Una frase por línea: se dicen una tras otra, con la pausa de la configuración. Una línea larga se parte en varios globos. Vacío = no dice nada.";

    /// <summary>
    /// The maximum size of a phrase in bytes (UTF-8). The client shows up to 90 bytes, including the terminating zero.
    /// </summary>
    internal const int MaximumPhraseBytes = 89;

    /// <summary>
    /// Splits the text into phrases: each non-empty line is a phrase, and a line which doesn't fit
    /// into one message is split at its spaces.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The phrases, in the order in which they are said.</returns>
    public static IReadOnlyList<string> Split(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var phrases = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                AddWrapped(trimmed, phrases);
            }
        }

        return phrases;
    }

    private static void AddWrapped(string line, List<string> phrases)
    {
        var current = new StringBuilder();
        foreach (var word in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (Encoding.UTF8.GetByteCount(candidate) <= MaximumPhraseBytes)
            {
                current.Clear().Append(candidate);
                continue;
            }

            if (current.Length > 0)
            {
                phrases.Add(current.ToString());
                current.Clear();
            }

            // A single word which doesn't fit is cut.
            var rest = word;
            while (Encoding.UTF8.GetByteCount(rest) > MaximumPhraseBytes)
            {
                var length = rest.Length;
                while (Encoding.UTF8.GetByteCount(rest.AsSpan(0, length)) > MaximumPhraseBytes)
                {
                    length--;
                }

                phrases.Add(rest[..length]);
                rest = rest[length..];
            }

            current.Append(rest);
        }

        if (current.Length > 0)
        {
            phrases.Add(current.ToString());
        }
    }
}
