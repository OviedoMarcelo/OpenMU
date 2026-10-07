// <copyright file="SeasonPremiumChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which activates the premium season pass of the running season for an account.
/// </summary>
[Guid("B792E089-3030-4447-A0B1-D07E7C70DB40")]
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonPremiumChatCommandPlugIn_Name), Description = nameof(PlugInResources.SeasonPremiumChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(SeasonPremiumChatCommandArgs), CharacterStatus.GameMaster)]
public class SeasonPremiumChatCommandPlugIn : ChatCommandPlugInBase<SeasonPremiumChatCommandArgs>
{
    private const string Command = "/pasepremium";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, SeasonPremiumChatCommandArgs arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments.LoginName))
        {
            await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.LoginNameRequired)).ConfigureAwait(false);
            return;
        }

        if (SeasonPassChatCommandPlugIn.GetActivePlugIn(gameMaster) is not { } plugIn)
        {
            await gameMaster.ShowBlueMessageAsync("El pase de temporada no está disponible.").ConfigureAwait(false);
            return;
        }

        Guid accountId;
        using (var context = gameMaster.GameContext.PersistenceContextProvider.CreateNewPlayerContext(gameMaster.GameContext.Configuration))
        {
            if (await context.GetAccountByLoginNameAsync(arguments.LoginName).ConfigureAwait(false) is not { } account)
            {
                await gameMaster.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.AccountNotFound)).ConfigureAwait(false);
                return;
            }

            accountId = account.GetId();
        }

        var grantedBy = gameMaster.SelectedCharacter?.Name ?? "gm";
        var message = await plugIn.GrantPremiumAsync(gameMaster.GameContext, accountId, grantedBy).ConfigureAwait(false) switch
        {
            null => "No hay una temporada activa, o no se pudo guardar. Probá de nuevo en un rato.",
            { WasActive: true } result => $"La cuenta {arguments.LoginName} ya tenía el pase premium de {result.Season.Name}.",
            { } result => $"Pase premium de {result.Season.Name} activado para la cuenta {arguments.LoginName}.",
        };
        await gameMaster.ShowBlueMessageAsync(message).ConfigureAwait(false);
    }
}
