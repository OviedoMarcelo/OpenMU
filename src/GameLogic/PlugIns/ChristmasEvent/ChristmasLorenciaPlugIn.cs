// <copyright file="ChristmasLorenciaPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChristmasEvent;

using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Shows the Christmas version of Lorencia while it's active: the server uses its terrain (where players can walk,
/// and the safe zone), and tells each client to load the Christmas files of Lorencia (Data\Navidad of the client).
/// It's disabled by default; it's turned on and off in the admin panel, without restarting the server.
/// </summary>
/// <remarks>
/// A client loads the files the next time it loads Lorencia, so players which are in Lorencia when it's turned
/// on or off see the change after they leave and come back (or log in again).
/// </remarks>
[PlugIn]
[Display(Name = "Christmas Lorencia", Description = "Lorencia de Navidad: mientras está activo, el server usa el terreno navideño de Lorencia y los clientes cargan sus archivos (Data\\Navidad). Se prende y se apaga sin reiniciar; los que ya están en Lorencia lo ven al volver a entrar al mapa.")]
[Guid("D4F2A8C6-3B1E-4F7A-9C5D-8E2B6A4F1C3D")]
public sealed class ChristmasLorenciaPlugIn : IPeriodicTaskPlugIn, IPlayerStateChangedPlugIn, IDisabledByDefault
{
    /// <summary>
    /// The number of the map of Lorencia.
    /// </summary>
    public const ushort LorenciaMapNumber = 0;

    private const string TerrainResourceName = "MUnique.OpenMU.GameLogic.PlugIns.ChristmasEvent.Terrain1Navidad.att";

    private static readonly Lazy<byte[]?> ChristmasTerrain = new(LoadChristmasTerrain);

    private static readonly ConditionalWeakTable<IGameContext, ContextState> States = new();

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var state = States.GetValue(gameContext, context => new ContextState(context));
        if (state.IsApplied)
        {
            return;
        }

        if (ChristmasTerrain.Value is not { } terrain)
        {
            return;
        }

        state.SubscribeToDeactivation();
        if (await gameContext.GetMapAsync(LorenciaMapNumber).ConfigureAwait(false) is not { } lorencia)
        {
            return;
        }

        lorencia.Terrain.ReplaceTerrain(terrain);
        state.IsApplied = true;
        gameContext.LoggerFactory.CreateLogger<ChristmasLorenciaPlugIn>().LogInformation("The Christmas version of Lorencia is active.");
        await SendToAllAsync(gameContext, true).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        // Nothing to start: the terrain is applied on the next periodic check.
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        // Before the character is selected, so that the client loads Lorencia with the right files.
        if (currentState != PlayerState.Authenticated || ChristmasTerrain.Value is null)
        {
            return;
        }

        await player.InvokeViewPlugInAsync<IShowChristmasDialogPlugIn>(p => p.SetChristmasLorenciaAsync(true)).ConfigureAwait(false);
    }

    private static async ValueTask SendToAllAsync(IGameContext gameContext, bool enabled)
    {
        await gameContext.ForEachPlayerAsync(player => player.InvokeViewPlugInAsync<IShowChristmasDialogPlugIn>(p => p.SetChristmasLorenciaAsync(enabled)).AsTask()).ConfigureAwait(false);
    }

    private static byte[]? LoadChristmasTerrain()
    {
        using var stream = typeof(ChristmasLorenciaPlugIn).Assembly.GetManifestResourceStream(TerrainResourceName);
        if (stream is null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>
    /// What the plugin did on a game context, so that it's undone when the plugin is deactivated.
    /// </summary>
    private sealed class ContextState
    {
        private readonly IGameContext _gameContext;
        private bool _isSubscribed;

        public ContextState(IGameContext gameContext)
        {
            this._gameContext = gameContext;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the Christmas terrain is applied to Lorencia.
        /// </summary>
        public bool IsApplied { get; set; }

        public void SubscribeToDeactivation()
        {
            if (this._isSubscribed)
            {
                return;
            }

            this._isSubscribed = true;
            this._gameContext.PlugInManager.PlugInDeactivated += this.OnPlugInDeactivated;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Event handler, catching all exceptions.")]
        private async void OnPlugInDeactivated(object? sender, PlugInEventArgs e)
        {
            if (e.PlugInType != typeof(ChristmasLorenciaPlugIn) || !this.IsApplied)
            {
                return;
            }

            var logger = this._gameContext.LoggerFactory.CreateLogger<ChristmasLorenciaPlugIn>();
            try
            {
                this.IsApplied = false;
                if (await this._gameContext.GetMapAsync(LorenciaMapNumber, false).ConfigureAwait(false) is { } lorencia)
                {
                    lorencia.Terrain.ReplaceTerrain(lorencia.Definition.TerrainData);
                }

                logger.LogInformation("The Christmas version of Lorencia is not active anymore.");
                await SendToAllAsync(this._gameContext, false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Couldn't restore the normal version of Lorencia.");
            }
        }
    }
}
