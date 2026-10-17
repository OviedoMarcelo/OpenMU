// <copyright file="EventScheduleSyncPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sends the events again to the players in the game when a timetable changes or an event is (de)activated,
/// e.g. in the admin panel. The players receive them anyway when they enter the game.
/// Its configuration tells in which time zone the players see the times.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.EventScheduleSyncPlugIn_Name), Description = nameof(PlugInResources.EventScheduleSyncPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("7C2E5A1D-3B94-4F0E-A6D8-91E4B2C7F530")]
public sealed class EventScheduleSyncPlugIn : IPeriodicTaskPlugIn, ISupportCustomConfiguration<EventScheduleConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private readonly ConditionalWeakTable<GameContext, State> _states = new();

    /// <inheritdoc />
    public EventScheduleConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new EventScheduleConfiguration();

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var state = this._states.GetOrCreateValue(gameContext);
        var now = DateTime.UtcNow;
        if (now < state.NextCheckUtc)
        {
            return;
        }

        state.NextCheckUtc = now.Add(CheckInterval);
        var signature = $"{this.Configuration?.DisplayTimeZoneId}|{this.Configuration?.DisplayTimeZoneName}|{EventSchedule.GetSignature(EventSchedule.GetEvents(gameContext))}";
        var previous = state.Signature;
        state.Signature = signature;
        if (previous is null || previous == signature)
        {
            // At the first check, the players in the game already received the events when they entered.
            return;
        }

        await gameContext.ForEachPlayerAsync(player => player.SelectedCharacter is null ? Task.CompletedTask : EventSchedule.SendAsync(player).AsTask()).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        // Nothing to start.
    }

    private sealed class State
    {
        public DateTime NextCheckUtc { get; set; }

        public string? Signature { get; set; }
    }
}
