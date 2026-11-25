// <copyright file="SantaInvasionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChristmasEvent;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The invasion of Santa of the Christmas event: the Cursed Santa with his Evil Goblins. It only starts while the
/// Christmas event is running (see <see cref="ChristmasEventConfiguration"/>). Santa Village can't be invaded,
/// because the whole map is a safe zone, so it invades the towns.
/// </summary>
[PlugIn]
[Display(Name = "Santa Invasion", Description = "Invasión de Santa del evento de Navidad: el Cursed Santa y sus Evil Goblins. Solo arranca mientras dura el evento de Navidad.")]
[Guid("C5A1E7B3-2D9F-4C6A-B8E4-6F1D3A5C7E9B")]
public sealed class SantaInvasionPlugIn : SimpleInvasionPlugIn
{
    /// <summary>
    /// The number of the Evil Goblin.
    /// </summary>
    public const ushort EvilGoblin = 466;

    /// <summary>
    /// The number of the Cursed Santa.
    /// </summary>
    public const ushort CursedSanta = 476;

    /// <summary>
    /// Initializes a new instance of the <see cref="SantaInvasionPlugIn"/> class.
    /// </summary>
    public SantaInvasionPlugIn()
        : base(CreateDefaultConfiguration)
    {
    }

    /// <inheritdoc />
    protected override ushort? AnnouncedMonsterId => CursedSanta;

    /// <summary>
    /// Creates the default configuration: every three hours for 30 minutes, in Lorencia, Noria or Devias.
    /// </summary>
    /// <returns>The default configuration.</returns>
    internal static PeriodicInvasionConfiguration CreateDefaultConfiguration() => new()
    {
        TaskDuration = TimeSpan.FromMinutes(30),
        PreStartMessageDelay = TimeSpan.FromSeconds(3),
        StartMessage = "[{mapName}] ¡Invasión de Santa! Llegaron el Cursed Santa y sus Evil Goblins.",
        EndMessage = "[{mapName}] Terminó la invasión de Santa.",
        ForceSingleMap = true,
        Timetable = PeriodicTaskConfiguration.GenerateTimeSequence(TimeSpan.FromHours(3), new TimeOnly(1, 0)).ToList(),
        Mobs =
        [
            new(CursedSanta, 3, [InvasionMaps.Lorencia, InvasionMaps.Noria, InvasionMaps.Devias], SpawnMapStrategy.RandomMap, announceDeath: true),
            new(EvilGoblin, 40, [], SpawnMapStrategy.RandomMap),
        ],
    };

    /// <inheritdoc />
    protected override bool IsItTimeToStart(IGameContext gameContext)
    {
        return ChristmasEventPlugIn.IsRunning(gameContext) && base.IsItTimeToStart(gameContext);
    }
}
