// <copyright file="WeeklyQuestDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;

using System.ComponentModel;
using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// The definition of a weekly quest.
/// </summary>
public class WeeklyQuestDefinition
{
    private IReadOnlyList<WeeklyQuestObjective>? _objectives;
    private int _objectivesSourceCount;

    /// <summary>
    /// Gets or sets the identifier of the quest. The progress of the characters is stored by it.
    /// </summary>
    [Display(Name = "Id", Description = "Identificador único y estable, p. ej. \"bc-3\". El progreso se guarda con este valor.")]
    [Required]
    [StringLength(64)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name which is shown to the player.
    /// </summary>
    [Display(Name = "Nombre")]
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description which is shown to the player.
    /// </summary>
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this quest is active.
    /// </summary>
    [Display(Name = "Activa")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the category, which decides under which tab the quest is shown.
    /// </summary>
    [Display(Name = "Categoría", Description = "La pestaña en la que se muestra: Historia, Diaria, Semanal, Clase o Zona.")]
    public QuestCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the period after which the progress starts over.
    /// </summary>
    [Display(Name = "Reinicio", Description = "Semanal, Diaria o Una sola vez (p. ej. los capítulos de la historia).")]
    public QuestPeriod Period { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the quest which has to be completed before this quest is available.
    /// It's required in addition to one of the <see cref="PrerequisiteOptions"/>.
    /// </summary>
    [Display(Name = "Requiere quest", Description = "Opcional. La quest que hay que completar antes. Hasta entonces, esta quest no se muestra. Si además hay \"Requisitos (caminos)\", hacen falta esta quest y uno de los caminos.")]
    [KeyOf(nameof(WeeklyQuestsConfiguration.Quests))]
    [StringLength(64)]
    public string? PrerequisiteQuestId { get; set; }

    /// <summary>
    /// Gets or sets the options of the prerequisites. The quest is available when all quests of one of the options are completed.
    /// If empty, only the <see cref="PrerequisiteQuestId"/> is required.
    /// </summary>
    [Display(Name = "Requisitos (caminos)", Description = "Opcional. Cada fila es un camino para desbloquear esta quest, y en cada fila se eligen una o más quests: hay que completarlas todas. Alcanza con completar una de las filas. P. ej. fila 1 = A y B, fila 2 = C: se desbloquea al completar A y B, o al completar solo C.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<WeeklyQuestPrerequisite> PrerequisiteOptions { get; set; } = new List<WeeklyQuestPrerequisite>();

    /// <summary>
    /// Gets or sets the identifier of the <see cref="WeeklyQuestChapter"/> of the quest. The quest is available when the chapter is unlocked.
    /// </summary>
    [Display(Name = "Capítulo", Description = "Opcional. El capítulo de la quest. La quest se muestra recién cuando el capítulo está desbloqueado.")]
    [KeyOf(nameof(WeeklyQuestsConfiguration.Chapters))]
    [StringLength(64)]
    public string? ChapterId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this quest is part of every week, when the quests rotate
    /// (<see cref="WeeklyQuestsConfiguration.QuestsPerWeek"/>). Otherwise, it's part of the drawing.
    /// </summary>
    [Display(Name = "Siempre incluida", Description = "Solo con rotación: la quest aparece todas las semanas y no entra en el sorteo.")]
    public bool AlwaysIncluded { get; set; }

    /// <summary>
    /// Gets or sets the minimum character level to make progress in this quest.
    /// </summary>
    [Display(Name = "Nivel mínimo")]
    [Range(0, int.MaxValue)]
    public int MinimumLevel { get; set; }

    /// <summary>
    /// Gets or sets the maximum character level for this quest. 0 means no limit.
    /// Characters above it don't see the quest, unless they already made progress in it.
    /// </summary>
    [Display(Name = "Nivel máximo", Description = "0 = sin límite. Arriba de este nivel la quest no se muestra, salvo que ya tenga progreso.")]
    [Range(0, int.MaxValue)]
    public int MaximumLevel { get; set; }

    /// <summary>
    /// Gets or sets the minimum reset count to make progress in this quest.
    /// </summary>
    [Display(Name = "Resets mínimos")]
    [Range(0, int.MaxValue)]
    public int MinimumResets { get; set; }

    /// <summary>
    /// Gets or sets the maximum reset count for this quest. 0 means no limit.
    /// Characters above it don't see the quest, unless they already made progress in it.
    /// </summary>
    [Display(Name = "Resets máximos", Description = "0 = sin límite. Arriba de estos resets la quest no se muestra, salvo que ya tenga progreso.")]
    [Range(0, int.MaxValue)]
    public int MaximumResets { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rewards of this quest can be received only by one character per account and week.
    /// </summary>
    [Display(Name = "Una vez por cuenta", Description = "Si un personaje de la cuenta ya cobró el premio esta semana, los demás personajes no ven la quest.")]
    public bool OncePerAccount { get; set; }

    /// <summary>
    /// Gets or sets the character classes which can make progress in this quest.
    /// Each class includes its following <see cref="CharacterClass.NextGenerationClass"/>es,
    /// e.g. Dark Wizard includes Soul Master and Grand Master. If empty, every class qualifies.
    /// </summary>
    [Display(Name = "Clases permitidas", Description = "Opcional. Vacío = todas las clases. Cada clase incluye sus evoluciones: Dark Wizard = DW, SM y GrM; Soul Master = SM y GrM.")]
    public ICollection<CharacterClass> QualifiedCharacters { get; set; } = new List<CharacterClass>();

    /// <summary>
    /// Gets or sets the objectives (steps) of the quest. If empty, the single objective of the fields
    /// <see cref="ObjectiveType"/>, <see cref="RequiredCount"/> etc. is used.
    /// </summary>
    [Display(Name = "Pasos", Description = "Lo que hay que hacer para completar la quest: uno o más pasos, p. ej. hablar con un NPC, matar 50 Skeletons y derrotar al jefe.")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<WeeklyQuestObjective> Objectives { get; set; } = new List<WeeklyQuestObjective>();

    /// <summary>
    /// Gets or sets a value indicating whether the <see cref="Objectives"/> have to be done in their order.
    /// Otherwise, they make progress at the same time.
    /// </summary>
    [Display(Name = "Pasos en orden", Description = "Cada paso cuenta recién cuando se completó el anterior.")]
    public bool SequentialObjectives { get; set; }

    // The fields of the single objective below are only read from configurations which were saved before
    // the objectives were always a list of steps. The update plugin MigrateWeeklyQuestLegacyFieldsPlugIn
    // moves them into the steps; they are hidden from the admin panel, so new quests always use steps.

    /// <summary>
    /// Gets or sets the type of the objective.
    /// </summary>
    [Browsable(false)]
    public WeeklyQuestObjectiveType ObjectiveType { get; set; }

    /// <summary>
    /// Gets or sets the required count to complete the objective.
    /// </summary>
    [Range(1, int.MaxValue)]
    [Browsable(false)]
    public int RequiredCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the monster which has to be killed, for <see cref="WeeklyQuestObjectiveType.KillMonster"/>,
    /// or the NPC to talk to, for <see cref="WeeklyQuestObjectiveType.TalkToNpc"/>.
    /// </summary>
    [Browsable(false)]
    public virtual MonsterDefinition? Monster { get; set; }

    /// <summary>
    /// Gets or sets the text which the NPC says when the talk counts, for <see cref="WeeklyQuestObjectiveType.TalkToNpc"/>.
    /// Each line is said as a phrase of its own, see <see cref="NpcPhrases.Split"/>.
    /// </summary>
    [DataType(DataType.MultilineText)]
    [Browsable(false)]
    public string? NpcText { get; set; }

    /// <summary>
    /// Gets or sets the map on which the kills have to happen. If empty, kills on every map count.
    /// </summary>
    [Browsable(false)]
    public virtual GameMapDefinition? Map { get; set; }

    /// <summary>
    /// Gets or sets the item which has to be picked up, for <see cref="WeeklyQuestObjectiveType.CollectItem"/>.
    /// </summary>
    [Browsable(false)]
    public virtual ItemDefinition? Item { get; set; }

    /// <summary>
    /// Gets or sets the minimum level of the picked up item, for <see cref="WeeklyQuestObjectiveType.CollectItem"/>.
    /// </summary>
    [Range(0, 15)]
    [Browsable(false)]
    public int MinimumItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the type of the mini game, for <see cref="WeeklyQuestObjectiveType.CompleteMiniGame"/>.
    /// <see cref="DataModel.Configuration.MiniGameType.Undefined"/> means any mini game.
    /// </summary>
    [Browsable(false)]
    public MiniGameType MiniGameType { get; set; }

    /// <summary>
    /// Gets or sets the name of the boss encounter, for <see cref="WeeklyQuestObjectiveType.DefeatBossEncounter"/>.
    /// Empty means any encounter.
    /// </summary>
    [Browsable(false)]
    public string? BossEncounterName { get; set; }

    /// <summary>
    /// Gets or sets the minimum level of a killed player, for <see cref="WeeklyQuestObjectiveType.KillPlayer"/>.
    /// </summary>
    [Range(0, int.MaxValue)]
    [Browsable(false)]
    public int MinimumVictimLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether kills of players with the same IP address don't count, for <see cref="WeeklyQuestObjectiveType.KillPlayer"/>.
    /// </summary>
    [Browsable(false)]
    public bool IgnoreSameIp { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether kills of players of the same guild don't count, for <see cref="WeeklyQuestObjectiveType.KillPlayer"/>.
    /// </summary>
    [Browsable(false)]
    public bool IgnoreSameGuild { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether kills of players of the same party don't count, for <see cref="WeeklyQuestObjectiveType.KillPlayer"/>.
    /// </summary>
    [Browsable(false)]
    public bool IgnoreSameParty { get; set; } = true;

    /// <summary>
    /// Gets or sets the minutes until the same victim counts again, for <see cref="WeeklyQuestObjectiveType.KillPlayer"/>. 0 means no cooldown.
    /// </summary>
    [Range(0, int.MaxValue)]
    [Browsable(false)]
    public int VictimCooldownMinutes { get; set; }

    /// <summary>
    /// Gets or sets the experience of the season pass which the quest gives when it's completed.
    /// 0 means the default of the season pass for the period of the quest.
    /// </summary>
    /// <remarks>
    /// Only read from configurations which were saved before the points were a reward of the type
    /// <see cref="WeeklyQuestRewardType.SeasonPoints"/>; the update plugin MigrateWeeklyQuestLegacyFieldsPlugIn moves it into the rewards.
    /// </remarks>
    [Range(0, int.MaxValue)]
    [Browsable(false)]
    public int SeasonXp { get; set; }

    /// <summary>
    /// Gets or sets the rewards which are given when the quest has been completed.
    /// </summary>
    [Display(Name = "Premios")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<WeeklyQuestReward> Rewards { get; set; } = new List<WeeklyQuestReward>();

    /// <summary>
    /// Determines whether a character of the specified class can make progress in this quest.
    /// </summary>
    /// <param name="characterClass">The class of the character.</param>
    /// <returns><c>true</c>, if the class is one of the <see cref="QualifiedCharacters"/> or one of their evolutions.</returns>
    public bool IsQualified(CharacterClass? characterClass)
    {
        if (this.QualifiedCharacters.Count == 0)
        {
            return true;
        }

        if (characterClass is null)
        {
            return false;
        }

        foreach (var qualified in this.QualifiedCharacters)
        {
            // The step limit guards against a misconfigured, circular chain of classes.
            var current = qualified;
            for (var steps = 0; current is not null && steps < 10; steps++)
            {
                if (current.Number == characterClass.Number)
                {
                    return true;
                }

                current = current.NextGenerationClass;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the objectives of the quest: the configured <see cref="Objectives"/>, or the single objective
    /// of the fields <see cref="ObjectiveType"/>, <see cref="RequiredCount"/> etc. when there are none.
    /// </summary>
    /// <returns>The objectives; at least one.</returns>
    /// <remarks>
    /// The result is cached, because it's needed for every event which may make progress.
    /// A changed plugin configuration is deserialized into new objects, so the cache doesn't get outdated.
    /// </remarks>
    public IReadOnlyList<WeeklyQuestObjective> GetObjectives()
    {
        var count = this.Objectives.Count;
        if (this._objectives is { } cached && this._objectivesSourceCount == count)
        {
            return cached;
        }

        this._objectivesSourceCount = count;
        this._objectives = count == 0 ? [this.CreateSingleObjective()] : this.Objectives.ToArray();
        return this._objectives;
    }

    /// <summary>
    /// Gets the text which describes all rewards to a player.
    /// </summary>
    /// <param name="culture">The culture of the player.</param>
    /// <returns>The rewards, separated by commas.</returns>
    public string GetRewardsText(System.Globalization.CultureInfo culture)
    {
        return string.Join(", ", this.Rewards.Select(r => r.GetDisplayText(culture)));
    }

    /// <summary>
    /// Gets the points of the season pass which the quest gives instead of the default for its period:
    /// the sum of its rewards of the type <see cref="WeeklyQuestRewardType.SeasonPoints"/>, or else <see cref="SeasonXp"/>.
    /// </summary>
    /// <returns>The points; 0 means the default of the season pass for the period of the quest.</returns>
    public int GetSeasonPoints()
    {
        var points = this.Rewards.Where(r => r.RewardType == WeeklyQuestRewardType.SeasonPoints).Sum(r => (long)r.Amount);
        return points > 0 ? (int)Math.Min(points, int.MaxValue) : this.SeasonXp;
    }

    /// <inheritdoc />
    public override string ToString() => $"{this.Id}: {this.Name}";

    private WeeklyQuestObjective CreateSingleObjective() => new()
    {
        ObjectiveType = this.ObjectiveType,
        RequiredCount = this.RequiredCount,
        Monster = this.Monster,
        NpcText = this.NpcText,
        Map = this.Map,
        Item = this.Item,
        MinimumItemLevel = this.MinimumItemLevel,
        MiniGameType = this.MiniGameType,
        BossEncounterName = this.BossEncounterName,
        MinimumVictimLevel = this.MinimumVictimLevel,
        IgnoreSameIp = this.IgnoreSameIp,
        IgnoreSameGuild = this.IgnoreSameGuild,
        IgnoreSameParty = this.IgnoreSameParty,
        VictimCooldownMinutes = this.VictimCooldownMinutes,
    };
}
