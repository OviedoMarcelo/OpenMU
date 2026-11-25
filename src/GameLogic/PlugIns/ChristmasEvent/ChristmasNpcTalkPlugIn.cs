// <copyright file="ChristmasNpcTalkPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChristmasEvent;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Properties;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handles talking to the NPCs of Santa Village: Santa Claus opens the dialog of the gift, the Snowman the dialog to
/// go back to Devias and the Little Santas give their buffs, or fill up the health or the mana.
/// </summary>
[PlugIn]
[Display(Name = "Christmas NPCs", Description = "Santa Claus (diálogo del regalo), el Snowman (volver a Devias) y los Little Santa (buffs) de Santa Village.")]
[Guid("3C8E2A41-7B5D-4F9E-A1C3-5D7E9F1B2C4A")]
public class ChristmasNpcTalkPlugIn : IPlayerTalkToNpcPlugIn
{
    /// <inheritdoc />
    public ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        var number = npc.Definition.Number;
        if (number != ChristmasEventPlugIn.SantaClausNumber
            && number != ChristmasEventPlugIn.SnowmanNumber
            && number is < ChristmasEventPlugIn.FirstLittleSantaNumber or > ChristmasEventPlugIn.LastLittleSantaNumber)
        {
            return ValueTask.CompletedTask;
        }

        // It has to happen synchronously, because the caller doesn't await this method.
        eventArgs.HasBeenHandled = true;
        _ = this.TalkAsync(player, number);
        return ValueTask.CompletedTask;
    }

    private static async ValueTask GiveLittleSantaBlessingAsync(Player player, short littleSantaNumber, ChristmasEventConfiguration configuration)
    {
        if (player.Attributes is not { } attributes)
        {
            return;
        }

        if (LittleSantaBlessings.EffectOf(littleSantaNumber) is not { } effectNumber)
        {
            // The red and the blue ones fill up the health and the mana.
            if (littleSantaNumber == LittleSantaBlessings.RedLittleSanta)
            {
                attributes[Stats.CurrentHealth] = attributes[Stats.MaximumHealth];
            }
            else
            {
                attributes[Stats.CurrentMana] = attributes[Stats.MaximumMana];
            }

            return;
        }

        if (player.GameContext.Configuration.MagicEffects.FirstOrDefault(e => e.Number == effectNumber) is not { } effectDefinition)
        {
            player.Logger.LogWarning("The effect {effect} of the Little Santa {npc} is missing; is the update of the Christmas event installed?", effectNumber, littleSantaNumber);
            return;
        }

        var boosts = effectDefinition.PowerUpDefinitions
            .Where(def => def.Boost is not null && def.TargetAttribute is not null)
            .Select(def => new MagicEffect.ElementWithTarget(attributes.CreateElement(def), def.TargetAttribute!))
            .ToArray();
        if (boosts.Length == 0)
        {
            return;
        }

        var effect = new MagicEffect(configuration.BuffDuration, effectDefinition, boosts);
        await player.MagicEffectList.AddEffectAsync(effect).ConfigureAwait(false);
    }

    private async Task TalkAsync(Player player, short number)
    {
        try
        {
            if (number == ChristmasEventPlugIn.SnowmanNumber)
            {
                // Going back to Devias is always possible, also after the event.
                await player.InvokeViewPlugInAsync<IShowChristmasDialogPlugIn>(p => p.ShowSnowmanDialogAsync()).ConfigureAwait(false);
                return;
            }

            if (ChristmasEventPlugIn.GetConfiguration(player.GameContext) is not { } configuration
                || !configuration.IsRunning(ChristmasEventPlugIn.GetServerTime(player.GameContext)))
            {
                await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.ChristmasEventNotRunning)).ConfigureAwait(false);
                return;
            }

            if (number == ChristmasEventPlugIn.SantaClausNumber)
            {
                await player.InvokeViewPlugInAsync<IShowChristmasDialogPlugIn>(p => p.ShowSantaClausDialogAsync()).ConfigureAwait(false);
                return;
            }

            await GiveLittleSantaBlessingAsync(player, number, configuration).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error when talking to the Christmas NPC {number}.", number);
        }
    }
}
