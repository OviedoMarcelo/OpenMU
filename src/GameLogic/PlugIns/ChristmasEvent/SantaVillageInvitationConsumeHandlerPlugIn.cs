// <copyright file="SantaVillageInvitationConsumeHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChristmasEvent;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.GameLogic.Properties;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Consume handler for the Invitation to Santa Village (13, 66): it takes the player to Santa Village while the
/// Christmas event is running. The client asks for confirmation before it uses the item.
/// </summary>
[PlugIn]
[Display(Name = "Invitation to Santa Village", Description = "Al usar la Invitación a Santa Village (13, 66), lleva al jugador a Santa Village mientras dura el evento de Navidad.")]
[Guid("9D4B7E2A-1C6F-4A3B-8E5D-7F2A9C1B3D6E")]
public class SantaVillageInvitationConsumeHandlerPlugIn : BaseConsumeHandlerPlugIn
{
    /// <summary>
    /// The item of the invitation.
    /// </summary>
    public static readonly ItemIdentifier Invitation = new(66, 13);

    private readonly SantaVillageWarpAction _warpAction = new();

    /// <inheritdoc />
    public override ItemIdentifier Key => Invitation;

    /// <inheritdoc />
    public override async ValueTask<bool> ConsumeItemAsync(Player player, Item item, Item? targetItem, FruitUsage fruitUsage)
    {
        if (!this.CheckPreconditions(player, item))
        {
            return false;
        }

        if (!ChristmasEventPlugIn.IsRunning(player.GameContext))
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.ChristmasEventNotRunning)).ConfigureAwait(false);
            return false;
        }

        if (player.CurrentMap?.MapId == ChristmasEventPlugIn.SantaVillageMapNumber)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.AlreadyInSantaVillage)).ConfigureAwait(false);
            return false;
        }

        if (player.CurrentMiniGame is not null || player.DuelRoom is not null)
        {
            return false;
        }

        if (await this._warpAction.GetSantaVillageGateAsync(player).ConfigureAwait(false) is not { } gate
            || !await base.ConsumeItemAsync(player, item, targetItem, fruitUsage).ConfigureAwait(false))
        {
            return false;
        }

        await player.WarpToAsync(gate).ConfigureAwait(false);
        return true;
    }
}
