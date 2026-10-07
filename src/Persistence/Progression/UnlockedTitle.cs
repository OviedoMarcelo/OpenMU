// <copyright file="UnlockedTitle.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// A title which a character or an account has unlocked.
/// </summary>
public class UnlockedTitle
{
    /// <summary>
    /// Gets or sets the identifier of the owner: the character, or the account for titles of the whole account.
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the title, as configured in the plugin configuration.
    /// </summary>
    public string TitleId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp (UTC) when the title has been unlocked.
    /// </summary>
    public DateTime UnlockedAt { get; set; }

    /// <summary>
    /// Gets or sets where the title came from, e.g. the identifier of an achievement or "gm".
    /// </summary>
    public string? Source { get; set; }
}
