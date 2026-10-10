// <copyright file="ConfigurationTypeGroups.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// The configuration types grouped by topic, with Spanish captions - the same grouping as the
/// configuration menu of the admin panel (ConfigNavMenu.razor).
/// </summary>
public static class ConfigurationTypeGroups
{
    /// <summary>
    /// Gets the caption of the group which contains all other collections of the game configuration.
    /// </summary>
    public static string OtherGroupCaption => "Otros";

    /// <summary>
    /// Gets the groups.
    /// </summary>
    public static IReadOnlyList<Group> All { get; } =
    [
        new("Ajustes generales", [
            new(typeof(GameConfiguration), "General", "Rates de experiencia, niveles máximos, límites de Zen, party y otros ajustes globales.", []),
            new(typeof(GameClientDefinition), "Clientes del juego", "Versiones del cliente del juego que pueden conectarse.", ["Season", "Episode", "Language"]),
        ]),
        new("Mundo", [
            new(typeof(GameMapDefinition), "Mapas", "Mapas: multiplicador de experiencia, requisitos de entrada, zonas de aparición y drops propios.", ["Number", "ExpMultiplier", "SafezoneMap"]),
            new(typeof(MonsterDefinition), "Monstruos", "Monstruos y NPC: vida, daño, defensa, velocidad, respawn y qué sueltan.", ["Number", "ObjectKind", "NpcWindow", "MoveRange", "AttackRange", "RespawnDelay", "NumberOfMaximumItemDrops"]),
            new(typeof(WarpInfo), "Lista de warps", "Lista de teletransportes del comando /warp: costo, nivel mínimo y destino.", ["Index", "Costs", "LevelRequirement", "Gate"]),
        ]),
        new("Ítems y economía", [
            new(typeof(ItemDefinition), "Ítems", "Definiciones de ítems: poder, requisitos, precio, tamaño de pila (Durability) y drops.", ["Group", "Number", "Width", "Height", "DropLevel", "Durability", "Value"]),
            new(typeof(DropItemGroup), "Grupos de drop", "Grupos de drop: qué cae, con qué probabilidad y en qué monstruos o mapas.", ["Chance", "MinimumMonsterLevel", "MaximumMonsterLevel", "Monster", "ItemType"]),
            new(typeof(JewelMix), "Mezclas de joyas", "Cómo se combinan y se separan las joyas en packs de 10, 20 o 30.", ["Number", "SingleJewel", "MixedJewel"]),
        ]),
        new("Personajes y eventos", [
            new(typeof(CharacterClass), "Clases de personaje", "Clases de personaje: atributos iniciales, puntos por nivel y evoluciones.", ["Number", "CanGetCreated", "LevelRequirementByCreation", "IsMasterClass", "NextGenerationClass"]),
            new(typeof(Skill), "Habilidades", "Habilidades: daño, costo de maná y AG, alcance y requisitos.", ["Number", "DamageType", "SkillType", "Range", "AttackDamage"]),
            new(typeof(MiniGameDefinition), "Eventos", "Eventos como Blood Castle, Devil Square y Chaos Castle: niveles, entradas, duración y premios.", ["Type", "GameLevel", "MinimumCharacterLevel", "MaximumCharacterLevel", "EntranceFee", "MaximumPlayerCount"]),
        ]),
    ];

    /// <summary>
    /// Finds the entry of the type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The entry, or <c>null</c> if the type isn't part of a group.</returns>
    public static Entry? FindEntry(Type type) => All.SelectMany(g => g.Entries).FirstOrDefault(e => e.Type == type);

    /// <summary>
    /// A group of configuration types.
    /// </summary>
    /// <param name="Caption">The caption.</param>
    /// <param name="Entries">The entries.</param>
    public record Group(string Caption, IReadOnlyList<Entry> Entries);

    /// <summary>
    /// A configuration type of a group.
    /// </summary>
    /// <param name="Type">The type.</param>
    /// <param name="Caption">The caption.</param>
    /// <param name="Description">The description.</param>
    /// <param name="ListColumns">The properties which are shown as columns in its list.</param>
    public record Entry(Type Type, string Caption, string Description, IReadOnlyList<string> ListColumns);
}
