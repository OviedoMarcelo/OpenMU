// <copyright file="PlayerAppearanceData.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;

/// <summary>
/// The appearance data of a <see cref="Player"/>, which is based on the
/// currently selected character and its equipped items.
/// </summary>
internal sealed class PlayerAppearanceData : IAppearanceData
{
    private readonly Player _player;
    private bool? _fullAncientSetEquipped;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerAppearanceData"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public PlayerAppearanceData(Player player)
    {
        this._player = player;
    }

    /// <inheritdoc />
    public event EventHandler? AppearanceChanged;

    /// <inheritdoc />
    public CharacterClass? CharacterClass => this._player.SelectedCharacter?.CharacterClass;

    /// <inheritdoc />
    public CharacterStatus CharacterStatus => this._player.SelectedCharacter?.CharacterStatus ?? default;

    /// <inheritdoc />
    public CharacterPose Pose => this._player.SelectedCharacter?.Pose ?? default;

    /// <inheritdoc />
    public bool FullAncientSetEquipped => !PlayerCosmetics.IsWearingAny(this._player)
                                          && ((this._fullAncientSetEquipped ??= this._player.SelectedCharacter?.HasFullAncientSetEquipped()) ?? false);

    /// <inheritdoc />
    public IEnumerable<ItemAppearance> EquippedItems
    {
        get
        {
            if (this._player.Inventory is null)
            {
                return Enumerable.Empty<ItemAppearance>();
            }

            var items = this._player.Inventory.EquippedItems.Select(item => item.GetAppearance());
            if (!PlayerCosmetics.IsWearingAny(this._player))
            {
                return items;
            }

            // The cosmetic pieces are shown instead of the real items, and the ones without a required item also in empty slots.
            var result = items.Where(item => PlayerCosmetics.GetPiece(this._player, item.ItemSlot) is null).ToList();
            foreach (var slot in CosmeticSlotExtensions.All.Select(s => s.GetItemSlot()))
            {
                if (PlayerCosmetics.GetPiece(this._player, slot) is { } piece)
                {
                    result.Add(piece);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Raises the <see cref="AppearanceChanged"/> event.
    /// </summary>
    public void RaiseAppearanceChanged()
    {
        this._fullAncientSetEquipped = null;
        this.AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }
}
