// <copyright file="CustomMonstersDataTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the data of the custom monsters, which are meant to be the bosses of encounters.
/// </summary>
[TestFixture]
internal class CustomMonstersDataTest
{
    private const short IceColossusNumber = 700;
    private const short WatcherNumber = 701;
    private const short VaultGuardianNumber = 708;

    private static readonly short[] EncounterObjectNumbers = [702, 703, 704, 705, 706, 707];

    /// <summary>
    /// Tests that a new season 6 database contains the custom monsters, without spawns and drops of their own.
    /// </summary>
    [Test]
    public async Task NewDatabaseContainsCustomMonstersAsync()
    {
        var (_, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);

        AssertMonster(gameConfiguration, IceColossusNumber, "Coloso de Hielo", 150, 150_000);
        AssertMonster(gameConfiguration, WatcherNumber, "El Vigía", 130, 120_000);
        Assert.That(gameConfiguration.Maps.SelectMany(m => m.MonsterSpawns).Any(s => s.MonsterDefinition?.Number is IceColossusNumber or WatcherNumber), Is.False);
        AssertEncounterObjects(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update adds the objects of the boss encounters to a database which was created before they existed, only once.
    /// </summary>
    [Test]
    public async Task UpdateAddsEncounterObjectsOnceAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            foreach (var monster in gameConfiguration.Monsters.Where(m => EncounterObjectNumbers.Contains(m.Number)).ToList())
            {
                gameConfiguration.Monsters.Remove(monster);
            }

            await new AddEncounterObjectsPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
            await new AddEncounterObjectsPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

            AssertEncounterObjects(gameConfiguration);
        }
    }

    /// <summary>
    /// Tests that the updates add the custom monsters to a database which was created before they existed, only once.
    /// </summary>
    [Test]
    public async Task UpdatesAddCustomMonstersOnceAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            gameConfiguration.Monsters.Remove(gameConfiguration.Monsters.Single(m => m.Number == IceColossusNumber));
            gameConfiguration.Monsters.Remove(gameConfiguration.Monsters.Single(m => m.Number == WatcherNumber));

            await new AddIceColossusPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
            await new AddIceColossusPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
            await new AddWatcherPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
            await new AddWatcherPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

            AssertMonster(gameConfiguration, IceColossusNumber, "Coloso de Hielo", 150, 150_000);
            AssertMonster(gameConfiguration, WatcherNumber, "El Vigía", 130, 120_000);
        }
    }

    /// <summary>
    /// Tests that a new season 6 database contains the Guardián del Baúl, standing on a walkable tile of the safezone of Lorencia,
    /// next to the vault keeper.
    /// </summary>
    [Test]
    public async Task NewDatabaseContainsVaultGuardianAsync()
    {
        var (_, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);

        AssertVaultGuardian(gameConfiguration);
    }

    /// <summary>
    /// Tests that the update adds the Guardián del Baúl and its spawn to a database which was created before it existed, only once.
    /// </summary>
    [Test]
    public async Task UpdateAddsVaultGuardianOnceAsync()
    {
        var (context, gameConfiguration) = await CreateSeason6ConfigurationAsync().ConfigureAwait(false);
        using (context)
        {
            var lorencia = gameConfiguration.Maps.Single(m => m.Number == 0);
            lorencia.MonsterSpawns.Remove(lorencia.MonsterSpawns.Single(s => s.MonsterDefinition?.Number == VaultGuardianNumber));
            gameConfiguration.Monsters.Remove(gameConfiguration.Monsters.Single(m => m.Number == VaultGuardianNumber));

            await new AddVaultGuardianPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
            await new AddVaultGuardianPlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

            AssertVaultGuardian(gameConfiguration);
        }
    }

    private static void AssertVaultGuardian(GameConfiguration gameConfiguration)
    {
        var npc = gameConfiguration.Monsters.Single(m => m.Number == VaultGuardianNumber);
        Assert.That(npc.Designation.Value, Is.EqualTo("Guardián del Baúl"));
        Assert.That(npc.ObjectKind, Is.EqualTo(NpcObjectKind.PassiveNpc));
        Assert.That(npc.NpcWindow, Is.EqualTo(NpcWindow.Undefined));

        var lorencia = gameConfiguration.Maps.Single(m => m.Number == 0);
        var spawn = lorencia.MonsterSpawns.Single(s => s.MonsterDefinition?.Number == VaultGuardianNumber);
        Assert.That((spawn.X1, spawn.Y1, spawn.X2, spawn.Y2), Is.EqualTo((149, 144, 149, 144)));
        Assert.That(gameConfiguration.Maps.SelectMany(m => m.MonsterSpawns).Count(s => s.MonsterDefinition?.Number == VaultGuardianNumber), Is.EqualTo(1));

        var terrain = new GameLogic.GameMapTerrain(lorencia);
        Assert.That(terrain.WalkMap[spawn.X1, spawn.Y1], Is.True, "walkable");
        Assert.That(terrain.SafezoneMap[spawn.X1, spawn.Y1], Is.True, "in the safezone");
        Assert.That(
            lorencia.MonsterSpawns.Any(s => s.MonsterDefinition?.Number == 240 && Math.Abs(s.X1 - spawn.X1) <= 3 && Math.Abs(s.Y1 - spawn.Y1) <= 3),
            Is.True,
            "next to the vault keeper");
    }

    private static void AssertEncounterObjects(GameConfiguration gameConfiguration)
    {
        var objects = gameConfiguration.Monsters.Where(m => EncounterObjectNumbers.Contains(m.Number)).OrderBy(m => m.Number).ToList();
        Assert.That(objects.Select(m => m.Number), Is.EqualTo(EncounterObjectNumbers));
        Assert.That(objects.Select(m => m.Designation.Value), Is.EqualTo(new[] { "Cristal de Hielo", "Cristal de Fuego", "Pilar Rúnico de Hielo", "Pilar Rúnico de Fuego", "Obelisco de Hielo", "Obelisco de Fuego" }));
        Assert.That(objects.All(m => m.MoveRange == 0 && m.AttackRange == 0 && m.NumberOfMaximumItemDrops == 0 && m.DropItemGroups.Count == 0), Is.True);
    }

    private static void AssertMonster(GameConfiguration gameConfiguration, short number, string name, float level, float health)
    {
        var monster = gameConfiguration.Monsters.Single(m => m.Number == number);
        Assert.That(monster.Designation.Value, Is.EqualTo(name));
        Assert.That(Attribute(monster, Stats.Level), Is.EqualTo(level), name);
        Assert.That(Attribute(monster, Stats.MaximumHealth), Is.EqualTo(health), name);
        Assert.That(monster.DropItemGroups, Is.Empty, name);
    }

    private static float Attribute(MonsterDefinition monster, AttributeSystem.AttributeDefinition attribute) =>
        monster.Attributes.Single(a => a.AttributeDefinition?.Id == attribute.Id).Value;

    private static async Task<(IContext Context, GameConfiguration GameConfiguration)> CreateSeason6ConfigurationAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);

        var context = contextProvider.CreateNewContext();
        return (context, (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First());
    }
}
