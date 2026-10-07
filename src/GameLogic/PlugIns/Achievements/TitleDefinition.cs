// <copyright file="TitleDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Achievements;

using System.Globalization;

/// <summary>
/// A title which a character can show below its name, e.g. "Blood Castle Slayer".
/// </summary>
public class TitleDefinition
{
    /// <summary>
    /// The maximum length of the text, which the client can show.
    /// </summary>
    public const int MaximumTextLength = 32;

    /// <summary>
    /// The color which is used when the configured one is invalid: a light gold.
    /// </summary>
    private const uint DefaultColor = 0xFFFFD700;

    /// <summary>
    /// Gets or sets the identifier of the title. The unlocked and active titles are stored by it.
    /// </summary>
    [Display(Name = "Id", Description = "Identificador único y estable, p. ej. \"bc-slayer\". Los títulos desbloqueados se guardan con este valor, y es lo que el jugador escribe en /titulo.")]
    [Required]
    [StringLength(64)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text which is shown below the name of the character.
    /// </summary>
    [Display(Name = "Texto", Description = "Lo que se ve debajo del nombre, hasta 32 caracteres.")]
    [Required]
    [StringLength(MaximumTextLength)]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the color of the text, as "#RRGGBB".
    /// </summary>
    [Display(Name = "Color", Description = "Color del texto en formato #RRGGBB, p. ej. #FFD700 (dorado).")]
    [StringLength(7)]
    public string Color { get; set; } = "#FFD700";

    /// <inheritdoc />
    public override string ToString() => this.Text;

    /// <summary>
    /// Gets the color as 32 bit ARGB value.
    /// </summary>
    /// <returns>The color; a light gold, if the configured color is invalid.</returns>
    public uint GetArgb()
    {
        var hex = this.Color?.Trim().TrimStart('#');
        if (hex is { Length: 6 } && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return 0xFF000000 | rgb;
        }

        return DefaultColor;
    }
}
