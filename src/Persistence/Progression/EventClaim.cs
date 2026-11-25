// <copyright file="EventClaim.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// Something of an event which has been handed out once for a key, e.g. the daily gift of Santa Claus
/// for an account or an IP address on a day.
/// </summary>
public class EventClaim
{
    /// <summary>
    /// Gets or sets the key which can claim it only once, e.g. <c>santa-gift:2026-12-24:ip:203.0.113.5</c>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp (UTC) when it has been claimed.
    /// </summary>
    public DateTime ClaimedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the account which claimed it.
    /// </summary>
    public Guid? AccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the character which claimed it.
    /// </summary>
    public Guid? CharacterId { get; set; }
}
