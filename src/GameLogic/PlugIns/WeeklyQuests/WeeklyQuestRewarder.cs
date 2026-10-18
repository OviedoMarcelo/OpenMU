// <copyright file="WeeklyQuestRewarder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.WeeklyQuests;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.GameLogic.Views.Inventory;

/// <summary>
/// Hands out the rewards of a <see cref="WeeklyQuestDefinition"/>.
/// </summary>
/// <remarks>
/// Unlike the classic quests, items are never dropped on the ground when the inventory is full.
/// The rewards are handed out completely or not at all, so they can be retried later.
/// </remarks>
public static class WeeklyQuestRewarder
{
    /// <summary>
    /// Tries to give all rewards of the quest to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="quest">The quest.</param>
    /// <returns><c>true</c>, if all rewards have been given; <c>false</c>, if nothing has been given.</returns>
    public static ValueTask<bool> TryGiveRewardsAsync(Player player, WeeklyQuestDefinition quest)
    {
        return TryGiveRewardsAsync(player, quest.Rewards, quest.Id);
    }

    /// <summary>
    /// Tries to give all of the specified rewards to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="rewards">The rewards.</param>
    /// <param name="sourceId">The identifier of what is rewarded, e.g. a quest or an achievement, for the log.</param>
    /// <returns><c>true</c>, if all rewards have been given; <c>false</c>, if nothing has been given.</returns>
    public static async ValueTask<bool> TryGiveRewardsAsync(Player player, ICollection<WeeklyQuestReward> rewards, string sourceId)
    {
        if (player.Inventory is null || player.SelectedCharacter is null)
        {
            return false;
        }

        var money = rewards
            .Where(r => r.RewardType == WeeklyQuestRewardType.Money)
            .Sum(r => (long)r.Amount);
        if (money > 0 && player.Money + money > player.GameContext.Configuration.MaximumInventoryMoney)
        {
            return false;
        }

        var addedItems = new List<Item>();
        foreach (var reward in rewards.Where(r => r.RewardType == WeeklyQuestRewardType.Item))
        {
            if (ResolveItemDefinition(player, reward) is not { } itemDefinition)
            {
                player.Logger.LogWarning("{source} has an item reward without a valid item.", sourceId);
                continue;
            }

            for (var i = 0; i < reward.Amount; i++)
            {
                var item = CreateItem(itemDefinition, reward).MakePersistent(player.PersistenceContext);
                if (!await player.Inventory.AddItemAsync(item).ConfigureAwait(false))
                {
                    await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
                    await RollbackAsync(player, addedItems).ConfigureAwait(false);
                    return false;
                }

                addedItems.Add(item);
            }
        }

        foreach (var item in addedItems)
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        }

        if (money > 0)
        {
            player.TryAddMoney((int)money);
        }

        foreach (var reward in rewards)
        {
            switch (reward.RewardType)
            {
                case WeeklyQuestRewardType.Experience:
                    await player.AddExperienceAsync(reward.Amount, null).ConfigureAwait(false);
                    break;
                case WeeklyQuestRewardType.MasterExperience:
                    await player.AddMasterExperienceAsync(reward.Amount, null).ConfigureAwait(false);
                    break;
                case WeeklyQuestRewardType.CosmeticPiece:
                    await GiveCosmeticPieceAsync(player, reward, sourceId).ConfigureAwait(false);
                    break;
                default:
                    // Items and money are handled above. The points of the season pass are given by the
                    // season pass when the quest is completed (see WeeklyQuestDefinition.GetSeasonPoints).
                    break;
            }
        }

        return true;
    }

    private static async ValueTask GiveCosmeticPieceAsync(Player player, WeeklyQuestReward reward, string sourceId)
    {
        if (CosmeticsPlugIn.GetActivePlugIn(player) is not { } plugIn)
        {
            player.Logger.LogWarning("{source} has a cosmetic piece as reward, but the cosmetics plugin isn't active.", sourceId);
            return;
        }

        if (await plugIn.GrantAsync(player, reward.CosmeticPieceNumber, sourceId).ConfigureAwait(false) is null)
        {
            player.Logger.LogWarning("{source} has the cosmetic piece {piece} as reward, which couldn't be unlocked.", sourceId, reward.CosmeticPieceNumber);
        }
    }

    private static async ValueTask RollbackAsync(Player player, List<Item> addedItems)
    {
        foreach (var item in addedItems)
        {
            await player.Inventory!.RemoveItemAsync(item).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(item).ConfigureAwait(false);
        }
    }

    private static ItemDefinition? ResolveItemDefinition(Player player, WeeklyQuestReward reward)
    {
        return ConfiguredItemFactory.Resolve(player.GameContext, reward.Item);
    }

    private static TemporaryItem CreateItem(ItemDefinition itemDefinition, WeeklyQuestReward reward)
    {
        return ConfiguredItemFactory.Create(itemDefinition, reward.ItemLevel, reward.Skill, reward.Luck, reward.OptionLevel, reward.ExcellentOptions);
    }
}
