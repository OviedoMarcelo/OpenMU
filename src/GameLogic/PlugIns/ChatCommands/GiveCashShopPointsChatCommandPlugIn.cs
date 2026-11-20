// <copyright file="GiveCashShopPointsChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.CashShop;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The chat command "/darcc &lt;login&gt; &lt;amount&gt;" of game masters, which gives (or with a negative amount, takes)
/// points of the cash shop to an account.
/// </summary>
[Guid("5E9B3F06-7A2C-4D81-B4E5-0F6C8A1D2B97")]
[PlugIn]
[Display(Name = "/darcc", Description = "GM: da (o con una cantidad negativa, quita) Cyber Coins (la moneda de la tienda) a una cuenta. Uso: /darcc <usuario> <cantidad>.")]
[ChatCommandHelp(Command, typeof(CashShopPointsChatCommandArgs), CharacterStatus.GameMaster)]
public class GiveCashShopPointsChatCommandPlugIn : ChatCommandPlugInBase<CashShopPointsChatCommandArgs>
{
    private const string Command = "/darcc";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, CashShopPointsChatCommandArgs arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments.LoginName) || arguments.Amount == 0)
        {
            await gameMaster.ShowBlueMessageAsync("Usá /darcc <usuario> <cantidad>. Con una cantidad negativa, quita Cyber Coins.").ConfigureAwait(false);
            return;
        }

        if (CashShopPlugIn.GetActivePlugIn(gameMaster.GameContext) is not { } cashShop)
        {
            await gameMaster.ShowBlueMessageAsync("La tienda no está activa (plugin Cash Shop).").ConfigureAwait(false);
            return;
        }

        Guid accountId;
        string login;
        using (var context = gameMaster.GameContext.PersistenceContextProvider.CreateNewPlayerContext(gameMaster.GameContext.Configuration))
        {
            if (await context.GetAccountByLoginNameAsync(arguments.LoginName).ConfigureAwait(false) is not { } account)
            {
                await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.AccountNotFound)).ConfigureAwait(false);
                return;
            }

            accountId = account.GetId();
            login = account.LoginName;
        }

        var givenBy = gameMaster.SelectedCharacter?.Name ?? "gm";
        var points = await cashShop.AddPointsAsync(gameMaster.GameContext, accountId, arguments.Amount, "gm", givenBy).ConfigureAwait(false);
        if (points is null)
        {
            await gameMaster.ShowBlueMessageAsync($"La cuenta {login} no tiene tantas Cyber Coins para quitar.").ConfigureAwait(false);
            return;
        }

        gameMaster.Logger.LogInformation("{gm} gave {amount} cash shop points to account {account}, now {points}.", givenBy, arguments.Amount, login, points);
        await gameMaster.ShowBlueMessageAsync($"La cuenta {login} tiene ahora {points} Cyber Coins.").ConfigureAwait(false);
    }
}
