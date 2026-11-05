// <copyright file="PlayerStorages.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.Persistence;

/// <summary>
/// The item storages of a <see cref="Player"/>.
/// </summary>
internal sealed class PlayerStorages
{
    private readonly Player _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerStorages"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public PlayerStorages(Player player)
    {
        this._player = player;
    }

    /// <summary>
    /// Gets the inventory of the selected character.
    /// </summary>
    public IInventoryStorage? Inventory { get; private set; }

    /// <summary>
    /// Gets or sets the temporary storage, which is used while an NPC or trade dialog is opened.
    /// </summary>
    public IStorage? TemporaryStorage { get; set; }

    /// <summary>
    /// Gets the shop storage of the selected character.
    /// </summary>
    public IShopStorage? ShopStorage { get; private set; }

    /// <summary>
    /// Gets or sets the vault. It's set when the vault NPC is opened.
    /// </summary>
    public IStorage? Vault { get; set; }

    /// <summary>
    /// Gets or sets the backup of the inventory, which is created before a trade.
    /// </summary>
    public BackupItemStorage? BackupInventory { get; set; }

    /// <summary>
    /// Creates the storages for the selected character, when it entered the world.
    /// </summary>
    /// <param name="character">The selected character.</param>
    public void CreateForCharacter(Character character)
    {
        this.Inventory = new InventoryStorage(this._player, this._player.GameContext);
        this.ShopStorage = new ShopStorage(character);
        this.TemporaryStorage = new Storage(InventoryConstants.TemporaryStorageSize, new TemporaryItemStorage());
        this.Vault = null; // vault storage is getting set when vault npc is opened.
    }

    /// <summary>
    /// Returns the items which are still placed in an NPC or trade dialog (the temporary storage)
    /// to the inventory, e.g. when the player leaves the game.
    /// </summary>
    /// <remarks>
    /// The inventory itself is never reset to the <see cref="BackupInventory"/> here. The backup is
    /// just a snapshot of the moment the dialog was opened; the inventory may have changed since then
    /// (picked up, consumed, dropped, sold or equipped items). Resetting it to the snapshot would remove
    /// items which were added afterwards (so they are deleted in the database) and bring back items which
    /// were already deleted. The backup is only used to put the returned items back to their original slots.
    /// </remarks>
    public async ValueTask RestoreTemporaryStorageItemsAsync()
    {
        var backupInventory = this.BackupInventory;
        this.BackupInventory = null;
        try
        {
            if (this.Inventory is not { } inventory)
            {
                return;
            }

            if (this._player.TradingMoney > 0)
            {
                // The trading money was already taken from the inventory when it was put into the trade.
                this._player.TryAddMoney(this._player.TradingMoney);
                this._player.TradingMoney = 0;
            }

            if (this.TemporaryStorage is not { ItemStorage.Items.Count: > 0 } temporaryStorage)
            {
                // Nothing to restore.
                return;
            }

            var items = temporaryStorage.Items.ToList();
            this._player.Logger.LogInformation("Returning {count} items from temporary storage to inventory for player {player}", items.Count, this._player.Name);
            foreach (var item in items)
            {
                try
                {
                    var originalSlot = backupInventory?.GetInitialSlot(item);
                    await temporaryStorage.RemoveItemAsync(item).ConfigureAwait(false);
                    if (originalSlot is { } slot && inventory.GetItem(slot) is null && await inventory.AddItemAsync(slot, item).ConfigureAwait(false))
                    {
                        continue;
                    }

                    if (!await inventory.AddItemAsync(item).ConfigureAwait(false))
                    {
                        // We should never get so far, since the space is checked before doing anything with the temporary storage.
                        this._player.Logger.LogError("CRITICAL: Failed to return item {item} from temporary storage to inventory of player {player}. Item is lost. id: {itemid}", item, this._player.Name, item.GetId());
                    }
                }
                catch (Exception ex)
                {
                    this._player.Logger.LogError(ex, "Error returning item {item} from temporary storage to inventory of player {player}.", item, this._player.Name);
                }
            }

            this.TemporaryStorage = null;
        }
        catch (Exception ex)
        {
            this._player.Logger.LogError(ex, "Error returning items from temporary storage to inventory");
        }
    }
}
