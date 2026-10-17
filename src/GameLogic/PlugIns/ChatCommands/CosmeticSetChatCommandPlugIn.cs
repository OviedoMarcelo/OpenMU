// <copyright file="CosmeticSetChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Globalization;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.PlugIns.CosmeticSets;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which changes the cosmetic set which the character wears over its armor.
/// The cosmetic sets window of the client uses it, too.
/// </summary>
[Guid("2D6F4A8E-9C13-4B57-8E20-6A1F3C5D7B92")]
[PlugIn]
[Display(Name = nameof(PlugInResources.CosmeticSetChatCommandPlugIn_Name), Description = nameof(PlugInResources.CosmeticSetChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(CosmeticSetChatCommandArgs), CharacterStatus.Normal)]
public class CosmeticSetChatCommandPlugIn : ChatCommandPlugInBase<CosmeticSetChatCommandArgs>
{
    private const string Command = "/cosmetico";

    private const string Off = "off";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, CosmeticSetChatCommandArgs arguments)
    {
        if (CosmeticSetsPlugIn.GetActivePlugIn(player) is not { } plugIn)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticSetsNotAvailable)).ConfigureAwait(false);
            return;
        }

        short? setNumber = null;
        if (!string.Equals(arguments.SetNumber, Off, StringComparison.OrdinalIgnoreCase))
        {
            if (!short.TryParse(arguments.SetNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticSetUsage)).ConfigureAwait(false);
                return;
            }

            setNumber = number;
        }

        var setName = plugIn.Configuration?.FindSet(setNumber)?.Name ?? arguments.SetNumber;
        var messageKey = await plugIn.SetActiveSetAsync(player, setNumber).ConfigureAwait(false) switch
        {
            CosmeticSetChangeResult.Changed => nameof(PlayerMessage.CosmeticSetChanged),
            CosmeticSetChangeResult.Removed => nameof(PlayerMessage.CosmeticSetRemoved),
            CosmeticSetChangeResult.NotUnlocked => nameof(PlayerMessage.CosmeticSetNotUnlocked),
            CosmeticSetChangeResult.Unknown => nameof(PlayerMessage.CosmeticSetUnknown),
            CosmeticSetChangeResult.NotQualified => nameof(PlayerMessage.CosmeticSetNotQualified),
            _ => nameof(PlayerMessage.CosmeticSetChangeFailed),
        };
        await player.ShowLocalizedBlueMessageAsync(messageKey, setName).ConfigureAwait(false);
    }
}
