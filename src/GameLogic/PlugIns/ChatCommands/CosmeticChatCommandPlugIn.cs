// <copyright file="CosmeticChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Globalization;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which puts on a cosmetic piece over the armor of the character, or takes it off.
/// The cosmetics window of the client uses it, too.
/// </summary>
[Guid("2D6F4A8E-9C13-4B57-8E20-6A1F3C5D7B92")]
[PlugIn]
[Display(Name = nameof(PlugInResources.CosmeticChatCommandPlugIn_Name), Description = nameof(PlugInResources.CosmeticChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(CosmeticChatCommandArgs), CharacterStatus.Normal)]
public class CosmeticChatCommandPlugIn : ChatCommandPlugInBase<CosmeticChatCommandArgs>
{
    private const string Command = "/cosmetico";

    private const string Off = "off";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, CosmeticChatCommandArgs arguments)
    {
        if (CosmeticsPlugIn.GetActivePlugIn(player) is not { } plugIn)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticsNotAvailable)).ConfigureAwait(false);
            return;
        }

        if (string.Equals(arguments.Piece, Off, StringComparison.OrdinalIgnoreCase))
        {
            var takeOff = arguments.TakeOffPieceNumber > 0 ? arguments.TakeOffPieceNumber : (short?)null;
            await ShowResultAsync(player, await plugIn.TakeOffAsync(player, takeOff).ConfigureAwait(false), null).ConfigureAwait(false);
            return;
        }

        if (!short.TryParse(arguments.Piece, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pieceNumber))
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.CosmeticUsage)).ConfigureAwait(false);
            return;
        }

        var piece = plugIn.Configuration?.FindPiece(pieceNumber);
        var name = piece is null ? arguments.Piece : CosmeticsPlugIn.GetName(player.GameContext.Configuration, piece, player.Culture);
        await ShowResultAsync(player, await plugIn.WearAsync(player, pieceNumber).ConfigureAwait(false), name).ConfigureAwait(false);
    }

    private static ValueTask ShowResultAsync(Player player, CosmeticChangeResult result, string? name)
    {
        var messageKey = result switch
        {
            CosmeticChangeResult.Worn => nameof(PlayerMessage.CosmeticWorn),
            CosmeticChangeResult.TakenOff => nameof(PlayerMessage.CosmeticTakenOff),
            CosmeticChangeResult.NotWorn => nameof(PlayerMessage.CosmeticNotWorn),
            CosmeticChangeResult.NotUnlocked => nameof(PlayerMessage.CosmeticNotUnlocked),
            CosmeticChangeResult.Unknown => nameof(PlayerMessage.CosmeticUnknown),
            CosmeticChangeResult.NotQualified => nameof(PlayerMessage.CosmeticNotQualified),
            _ => nameof(PlayerMessage.CosmeticChangeFailed),
        };
        return player.ShowLocalizedBlueMessageAsync(messageKey, name);
    }
}
