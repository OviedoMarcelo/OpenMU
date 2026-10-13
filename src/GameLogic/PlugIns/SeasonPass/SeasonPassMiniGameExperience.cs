// <copyright file="SeasonPassMiniGameExperience.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;

/// <summary>
/// The experience of the season pass which is given at the end of a mini game, e.g. Blood Castle.
/// </summary>
public class SeasonPassMiniGameExperience
{
    /// <summary>
    /// Gets or sets the type of the mini game.
    /// </summary>
    [Display(Name = "Evento")]
    public MiniGameType MiniGameType { get; set; }

    /// <summary>
    /// Gets or sets the experience for staying in the game until its end.
    /// </summary>
    [Display(Name = "XP por participar", Description = "XP del pase para quien sigue en el evento cuando termina.")]
    [Range(0, int.MaxValue)]
    public int ParticipationExperience { get; set; }

    /// <summary>
    /// Gets or sets the additional experience for completing the game, see <see cref="RequiredSuccess"/>.
    /// </summary>
    [Display(Name = "XP por completar", Description = "XP del pase adicional para quien además cumple \"Éxito requerido\".")]
    [Range(0, int.MaxValue)]
    public int CompletionExperience { get; set; }

    /// <summary>
    /// Gets or sets what a player has to achieve to complete the game, with the same rules as the rewards of the mini game.
    /// </summary>
    [Display(Name = "Éxito requerido", Description = "Qué cuenta como completar el evento, igual que en los premios del evento. P. ej. Blood Castle: WinnerOrInWinningParty, Devil Square: Alive, Chaos Castle: Winner.")]
    public MiniGameSuccessFlags RequiredSuccess { get; set; }

    /// <inheritdoc />
    public override string ToString() => this.MiniGameType.ToString();
}
