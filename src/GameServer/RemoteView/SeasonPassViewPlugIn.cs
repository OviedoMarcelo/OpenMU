// <copyright file="SeasonPassViewPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.SeasonPass;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ISeasonPassViewPlugIn"/> which sends a
/// <see cref="SeasonPassState"/> message to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SeasonPassViewPlugIn_Name), Description = nameof(PlugInResources.SeasonPassViewPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("1CB2B63F-0754-4951-8B57-B2492023B515")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class SeasonPassViewPlugIn : ISeasonPassViewPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonPassViewPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public SeasonPassViewPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public ValueTask ShowSeasonPassAsync(SeasonPassOverview overview)
    {
        if (this._player.Connection is not { Connected: true } connection)
        {
            return ValueTask.CompletedTask;
        }

        var culture = this._player.Culture;
        var levels = overview.Season is null ? [] : overview.Levels.Take(SeasonDefinition.MaximumLevel).ToList();
        var experiencePerLevel = Math.Max(1, overview.Season?.ExperiencePerLevel ?? 1);
        var maximumLevel = overview.Season?.GetMaximumLevel() ?? 0;
        var experienceInLevel = overview.Level >= maximumLevel ? 0 : overview.Experience % experiencePerLevel;
        var secondsUntilEnd = overview.Season is null ? 0 : (uint)Math.Clamp((overview.EndUtc - DateTime.UtcNow).TotalSeconds, 0, uint.MaxValue);

        int Write()
        {
            var size = SeasonPassStateRef.GetRequiredSize(levels.Count);
            var span = connection.Output.GetSpan(size)[..size];
            var packet = new SeasonPassStateRef(span)
            {
                IsPremium = overview.IsPremium,
                Level = (ushort)overview.Level,
                MaximumLevel = (ushort)maximumLevel,
                LevelCount = (ushort)levels.Count,
                ExperienceInLevel = (uint)experienceInLevel,
                ExperiencePerLevel = (uint)experiencePerLevel,
                SecondsUntilEnd = secondsUntilEnd,
                SeasonName = overview.Season?.Name ?? string.Empty,
            };

            for (var i = 0; i < levels.Count; i++)
            {
                var entry = levels[i];
                var target = packet[i];
                target.Level = (ushort)entry.Level.Level;
                target.IsFreeClaimed = entry.IsFreeClaimed;
                target.IsPremiumClaimed = entry.IsPremiumClaimed;
                target.FreeRewards = entry.Level.GetRewardsText(false, culture);
                target.PremiumRewards = entry.Level.GetRewardsText(true, culture);
            }

            return size;
        }

        return connection.SendAsync(Write);
    }
}
