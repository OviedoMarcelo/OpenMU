// <copyright file="CashShopPointsChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Globalization;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.CashShop;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The chat command "/cc", which shows the Cyber Coins (the currency of the cash shop) of the account.
/// </summary>
[Guid("C4A1E7D2-3B95-4F08-8E6C-2D7F0B9A5E13")]
[PlugIn]
[Display(Name = "/cc", Description = "Muestra las Cyber Coins (la moneda de la tienda) de la cuenta.")]
[ChatCommandHelp(Command, typeof(EmptyChatCommandArgs), CharacterStatus.Normal)]
public class CashShopPointsChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>
{
    private const string Command = "/cc";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, EmptyChatCommandArgs arguments)
    {
        if (CashShopPlugIn.GetActivePlugIn(player.GameContext) is not { } cashShop || player.Account is not { } account)
        {
            await player.ShowBlueMessageAsync("La tienda no está disponible.").ConfigureAwait(false);
            return;
        }

        try
        {
            var points = await cashShop.Repository.GetPointsAsync(account.GetId()).ConfigureAwait(false);
            var currency = cashShop.Configuration?.CurrencyName ?? "Cyber Coins";
            await player.ShowBlueMessageAsync($"Tenés {points.ToString("N0", CultureInfo.GetCultureInfo("es-AR"))} {currency}. Apretá X para abrir la tienda.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Couldn't load the cash shop points of account {account}.", account.LoginName);
            await player.ShowBlueMessageAsync("No pudimos consultar tu saldo. Probá de nuevo en un rato.").ConfigureAwait(false);
        }
    }
}
