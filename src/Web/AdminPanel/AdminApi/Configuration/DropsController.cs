// <copyright file="DropsController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// The overview of the item drops: which drop item groups apply to each monster on each map, and the
/// resulting chances - like the item drops page of the admin panel.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/drops")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class DropsController : ControllerBase
{
    private readonly IDataSource<GameConfiguration> _dataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="DropsController"/> class.
    /// </summary>
    /// <param name="dataSource">The data source of the game configuration.</param>
    public DropsController(IDataSource<GameConfiguration> dataSource)
    {
        this._dataSource = dataSource;
    }

    /// <summary>
    /// Gets the drops of the monsters per map.
    /// </summary>
    /// <returns>The maps with their monsters.</returns>
    [HttpGet]
    public async Task<IEnumerable<MapDrops>> GetOverviewAsync()
    {
        var configuration = await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        return configuration.Maps
            .OrderBy(map => map.Number)
            .Select(map => new MapDrops(
                map.GetId(),
                map.Name.ValueInNeutralLanguage,
                map.Number,
                map.MonsterSpawns
                    .Select(spawn => spawn.MonsterDefinition)
                    .OfType<MonsterDefinition>()
                    .Where(monster => monster.ObjectKind is NpcObjectKind.Monster or NpcObjectKind.Destructible)
                    .Distinct()
                    .Select(monster => CreateMonsterDrops(monster, map))
                    .OrderBy(m => m.Level)
                    .ThenBy(m => m.Number)
                    .ToList()))
            .Where(map => map.Monsters.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Determines the drop item groups of a monster on a map, in the same way as the drop generator
    /// of the game logic (except the groups which depend on quests or the character).
    /// </summary>
    private static MonsterDrops CreateMonsterDrops(MonsterDefinition monster, GameMapDefinition map)
    {
        // Not every monster has a level attribute, e.g. destructibles.
        var level = (int)(monster.Attributes.FirstOrDefault(a => a.AttributeDefinition == Stats.Level)?.Value ?? 0);
        var groups = monster.DropItemGroups.ToList();
        if (monster.ObjectKind != NpcObjectKind.Destructible)
        {
            groups.AddRange(map.DropItemGroups
                .Where(group => (group.MinimumMonsterLevel is not { } minimum || level >= minimum)
                                && (group.MaximumMonsterLevel is not { } maximum || level <= maximum)
                                && (group.Monster is null || group.Monster.Equals(monster)))
                .Except(groups));
        }

        var totalChance = groups.Where(g => g.Chance < 1.0).Sum(g => g.Chance);
        return new MonsterDrops(
            monster.GetId(),
            monster.Designation.ValueInNeutralLanguage,
            monster.Number,
            level,
            monster.NumberOfMaximumItemDrops,
            groups.OrderByDescending(g => g.Chance).Select(g => new GroupChance(g.GetId(), g.Description.ValueInNeutralLanguage, g.Chance)).ToList(),
            groups.Count(g => g.Chance >= 1.0),
            totalChance,
            Math.Max(0, 1.0 - totalChance));
    }

    /// <summary>
    /// The monsters of a map with their drops.
    /// </summary>
    /// <param name="Id">The id of the map.</param>
    /// <param name="Name">The name of the map.</param>
    /// <param name="Number">The number of the map.</param>
    /// <param name="Monsters">The monsters which spawn on the map.</param>
    public record MapDrops(Guid Id, string Name, short Number, IReadOnlyList<MonsterDrops> Monsters);

    /// <summary>
    /// The drops of a monster on a map.
    /// </summary>
    /// <param name="Id">The id of the monster definition.</param>
    /// <param name="Name">The name.</param>
    /// <param name="Number">The number.</param>
    /// <param name="Level">The level.</param>
    /// <param name="MaximumDrops">The maximum number of items which drop at once.</param>
    /// <param name="Groups">The drop item groups which apply, the most likely first.</param>
    /// <param name="GuaranteedDrops">The number of groups which drop always.</param>
    /// <param name="TotalChance">The sum of the chances of the other groups, per drop roll.</param>
    /// <param name="NoDropChance">The chance that nothing drops in a drop roll.</param>
    public record MonsterDrops(Guid Id, string Name, short Number, int Level, int MaximumDrops, IReadOnlyList<GroupChance> Groups, int GuaranteedDrops, double TotalChance, double NoDropChance);

    /// <summary>
    /// A drop item group with its chance.
    /// </summary>
    /// <param name="Id">The id of the group.</param>
    /// <param name="Description">The description.</param>
    /// <param name="Chance">The chance, between 0 and 1.</param>
    public record GroupChance(Guid Id, string Description, double Chance);
}
