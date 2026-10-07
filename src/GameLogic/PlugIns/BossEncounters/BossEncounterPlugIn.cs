// <copyright file="BossEncounterPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Boss encounters with phases: a configurable boss appears on a configurable map, at configurable
/// times, and fights with mechanics like pillars, shields, telegraphed area attacks, summons and an enrage.
/// </summary>
/// <remarks>
/// It's disabled by default, because it's custom gameplay which the original game didn't have.
/// The default configuration contains "Kundun Reborn" in Kalima 7 as example.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.BossEncounterPlugIn_Name), Description = nameof(PlugInResources.BossEncounterPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C30A7641-90A4-4357-849E-7CB7858ED2F5")]
public sealed class BossEncounterPlugIn : IFeaturePlugIn, IPeriodicTaskPlugIn, ISupportCustomConfiguration<BossEncounterConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    private readonly ConcurrentDictionary<IGameContext, ConcurrentDictionary<string, BossEncounterContext>> _contexts = new();
    private readonly ConcurrentDictionary<IGameContext, int> _runningTicks = new();

    /// <inheritdoc />
    public BossEncounterConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the encounters of the game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The encounters; empty, if the plugin isn't active.</returns>
    public static IReadOnlyList<BossEncounterContext> GetEncounters(IGameContext gameContext)
    {
        var plugIn = gameContext.FeaturePlugIns.GetPlugIn<BossEncounterPlugIn>();
        return plugIn is not null && plugIn._contexts.TryGetValue(gameContext, out var contexts)
            ? contexts.Values.OrderBy(context => context.Name).ToList()
            : [];
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (this._runningTicks.GetOrAdd(gameContext, 0) != 0
            || !this._runningTicks.TryUpdate(gameContext, 1, 0))
        {
            return;
        }

        try
        {
            var configuration = this.Configuration ??= BossEncounterConfiguration.Default;
            var contexts = this._contexts.GetOrAdd(gameContext, _ => new ConcurrentDictionary<string, BossEncounterContext>(StringComparer.OrdinalIgnoreCase));
            var utcNow = DateTime.UtcNow;
            var definitions = configuration.Encounters
                .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
                .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            foreach (var definition in definitions)
            {
                var context = contexts.GetOrAdd(definition.Name, _ => new BossEncounterContext(gameContext, definition));
                context.UpdateDefinition(definition);
                await this.TickAsync(gameContext, context, utcNow).ConfigureAwait(false);
            }

            // Encounters which were removed from the configuration end.
            foreach (var removed in contexts.Values.Where(context => !definitions.Any(d => string.Equals(d.Name, context.Name, StringComparison.OrdinalIgnoreCase))).ToList())
            {
                if (removed.IsRunning)
                {
                    removed.RequestStop();
                    await this.TickAsync(gameContext, removed, utcNow).ConfigureAwait(false);
                }

                contexts.TryRemove(removed.Name, out _);
            }
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<BossEncounterPlugIn>().LogError(ex, "Unexpected error in the boss encounters.");
        }
        finally
        {
            this._runningTicks[gameContext] = 0;
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        foreach (var context in this._contexts.Values.SelectMany(contexts => contexts.Values))
        {
            context.RequestStart();
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return BossEncounterConfiguration.Default;
    }

    private async ValueTask TickAsync(IGameContext gameContext, BossEncounterContext context, DateTime utcNow)
    {
        try
        {
            await context.TickAsync(utcNow).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<BossEncounterPlugIn>().LogError(ex, "Unexpected error in the boss encounter {Name}.", context.Name);
        }
    }
}
