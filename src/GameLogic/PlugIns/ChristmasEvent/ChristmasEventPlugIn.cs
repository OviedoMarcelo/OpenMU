// <copyright file="ChristmasEventPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChristmasEvent;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The Christmas event of MU Cyber: Santa Village (map 62) with Santa Claus, the Snowman and the Little Santas,
/// which is entered with the Invitation to Santa Village. This plugin holds the configuration, the other
/// plugins of the event ask it whether the event is running.
/// </summary>
[PlugIn]
[Display(Name = "Christmas Event", Description = "Evento de Navidad: Santa Village con Santa Claus (regalo diario), el Snowman y los Little Santa (buffs). Se entra con la Invitación a Santa Village.")]
[Guid("A7E3C1B2-5D4F-4E8A-9B6C-2F1D0E3A4B5C")]
public class ChristmasEventPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<ChristmasEventConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>
    /// The number of the map Santa Village.
    /// </summary>
    public const ushort SantaVillageMapNumber = 62;

    /// <summary>
    /// The number of the map Devias, where the Snowman sends the players back.
    /// </summary>
    public const ushort DeviasMapNumber = 2;

    /// <summary>
    /// The number of the NPC Santa Claus.
    /// </summary>
    public const short SantaClausNumber = 465;

    /// <summary>
    /// The number of the NPC Snowman, which sends the players back to Devias.
    /// </summary>
    public const short SnowmanNumber = 467;

    /// <summary>
    /// The number of the first Little Santa (yellow). The others follow up to <see cref="LastLittleSantaNumber"/>.
    /// </summary>
    public const short FirstLittleSantaNumber = 468;

    /// <summary>
    /// The number of the last Little Santa (pink).
    /// </summary>
    public const short LastLittleSantaNumber = 475;

    /// <summary>
    /// The distance to Santa Claus up to which a player can ask for the gift.
    /// </summary>
    public const int SantaClausRange = 6;

    /// <inheritdoc />
    public ChristmasEventConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the configuration of the event, if the plugin is active.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The configuration; <c>null</c>, if the plugin isn't active.</returns>
    public static ChristmasEventConfiguration? GetConfiguration(IGameContext gameContext)
    {
        if (gameContext.FeaturePlugIns.GetPlugIn<ChristmasEventPlugIn>() is not { } plugIn)
        {
            return null;
        }

        return plugIn.Configuration ??= new ChristmasEventConfiguration();
    }

    /// <summary>
    /// Gets the current time of the server, in its time zone.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The current time of the server.</returns>
    public static DateTime GetServerTime(IGameContext gameContext)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, gameContext.ServerTimeZone);

    /// <summary>
    /// Determines whether the event is running now.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns><c>true</c>, if the plugin is active and the event is running now.</returns>
    public static bool IsRunning(IGameContext gameContext)
        => GetConfiguration(gameContext)?.IsRunning(GetServerTime(gameContext)) ?? false;

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ChristmasEventConfiguration();
}
