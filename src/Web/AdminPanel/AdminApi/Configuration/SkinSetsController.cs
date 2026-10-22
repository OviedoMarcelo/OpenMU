// <copyright file="SkinSetsController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Items;
using MUnique.OpenMU.Persistence.Progression;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Adds and edits the armor sets of skin packs (see <see cref="SkinArmorSetBuilder"/>), which are worn as cosmetic
/// pieces, while the game servers run.
/// </summary>
/// <remarks>
/// The pieces are built on the context of the data source, which has the whole configuration loaded, like the edit
/// pages of the admin panel. Saving publishes new pieces to the running game servers through the configuration change
/// listener, so no restart is needed; changes of existing pieces are applied to the game servers directly as well,
/// because they don't always reach them (see <see cref="ApplyToGameServers"/>). The five cosmetic pieces of a set are
/// kept in the configuration of the <see cref="CosmeticsPlugIn"/>, which reaches the game servers like the changes of
/// the plugins page. Afterwards, every game server of this process is checked for the set.
/// </remarks>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/skin-sets")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public partial class SkinSetsController : ControllerBase
{
    private const string AuditEntityType = "SkinArmorSet";

    private const string AuditEntityCaption = "Set de skins";

    private const int MaximumNameLength = 40;

    private const byte MaximumLevel = 15;

    /// <summary>
    /// The SQL state of a violated foreign key, e.g. of an item whose definition would be deleted.
    /// </summary>
    private const string ForeignKeyViolation = "23503";

    /// <summary>
    /// The cosmetic pieces of a set get the item number times this, plus 1 for the helm to 5 for the boots, e.g. 551 to 555 for the set 55.
    /// </summary>
    private const int CosmeticNumbersPerSet = 10;

    private static readonly string[] SlotNames = ["Casco", "Armadura", "Pantalón", "Guantes", "Botas"];

    private readonly IDataSource<GameConfiguration> _dataSource;
    private readonly IPersistenceContextProvider _contextProvider;
    private readonly IServerProvider _serverProvider;
    private readonly AdminAuditLog _auditLog;
    private readonly ILogger<SkinSetsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkinSetsController"/> class.
    /// </summary>
    /// <param name="dataSource">The data source of the game configuration.</param>
    /// <param name="contextProvider">The persistence context provider.</param>
    /// <param name="serverProvider">The server provider.</param>
    /// <param name="auditLog">The audit log.</param>
    /// <param name="logger">The logger.</param>
    public SkinSetsController(IDataSource<GameConfiguration> dataSource, IPersistenceContextProvider contextProvider, IServerProvider serverProvider, AdminAuditLog auditLog, ILogger<SkinSetsController> logger)
    {
        this._dataSource = dataSource;
        this._contextProvider = contextProvider;
        this._serverProvider = serverProvider;
        this._auditLog = auditLog;
        this._logger = logger;
    }

    /// <summary>
    /// Gets the skin sets, i.e. the item numbers whose five armor pieces are skins without stats, and the number
    /// which a new set gets.
    /// </summary>
    /// <returns>The overview.</returns>
    [HttpGet]
    public async Task<ActionResult<SkinSetOverview>> GetAsync()
    {
        var gameConfiguration = await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        var cosmetics = this.ReadCosmetics(gameConfiguration);
        var sets = gameConfiguration.Items
            .Where(i => SkinArmorSetBuilder.PieceGroups.Contains(i.Group))
            .GroupBy(i => i.Number)
            .Where(g => g.Count() == SkinArmorSetBuilder.PieceGroups.Count && g.All(SkinArmorSetBuilder.IsSkinOnly))
            .OrderBy(g => g.Key)
            .Select(g => new SkinSet(
                g.Key,
                GetSetName(g),
                SkinArmorSetBuilder.PieceGroups.Select(group => g.First(i => i.Group == group).Name.Value ?? string.Empty).ToList(),
                ToCosmeticPieces(cosmetics, g.Key)))
            .ToList();
        return new SkinSetOverview(sets, FindFreeNumber(gameConfiguration), SkinArmorSetBuilder.MaximumNumber, cosmetics is not null);
    }

    /// <summary>
    /// Checks whether the game servers of this process have the set with the number.
    /// </summary>
    /// <param name="number">The item number of the pieces.</param>
    /// <returns>The state of each game server.</returns>
    [HttpGet("{number:int}/live")]
    public ActionResult<IReadOnlyList<LiveState>> GetLive(short number) => this.Ok(this.CheckGameServers(number));

    /// <summary>
    /// Adds an armor set as skins. Without a number, it gets the lowest number which no armor piece uses.
    /// When the pieces with the number already exist and have the names of the set, their stats are taken off,
    /// if they have any, and the missing cosmetic pieces are added.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The result, with the state of each game server.</returns>
    [HttpPost]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<SkinSetResult>> CreateAsync([FromBody] CreateSkinSetRequest request)
    {
        if (ValidateName(request.Name, out var name) is { } invalidName)
        {
            return invalidName;
        }

        if (request.Number is < 0 or > SkinArmorSetBuilder.MaximumNumber)
        {
            return Invalid("number", $"El número tiene que estar entre 0 y {SkinArmorSetBuilder.MaximumNumber}.");
        }

        var cancellationToken = this.HttpContext.RequestAborted;
        short number;
        bool created;
        CosmeticsState cosmetics;
        await ConfigurationEditController.DataSourceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var gameConfiguration = await this._dataSource.GetOwnerAsync(default, cancellationToken).ConfigureAwait(false);
            var context = await this._dataSource.GetContextAsync(cancellationToken).ConfigureAwait(false);

            if ((request.Number ?? FindFreeNumber(gameConfiguration)) is not { } wanted)
            {
                return this.Conflict(new ConfigurationEditController.ErrorResponse("No queda ningún número libre en las armaduras."));
            }

            number = wanted;
            foreach (var group in SkinArmorSetBuilder.PieceGroups)
            {
                if (SkinArmorSetBuilder.FindItem(gameConfiguration, group, number) is { } existing
                    && existing.Name.Value != SkinArmorSetBuilder.GetPieceName(name, group))
                {
                    return this.Conflict(new ConfigurationEditController.ErrorResponse($"El número {number} ya lo usa \"{existing.Name.Value}\"."));
                }
            }

            created = SkinArmorSetBuilder.PieceGroups.Any(group => SkinArmorSetBuilder.FindItem(gameConfiguration, group, number) is null);
            try
            {
                SkinArmorSetBuilder.Build(context, gameConfiguration, number, name);
                foreach (var setGroup in SkinArmorSetBuilder.RemoveSetGroups(gameConfiguration, number))
                {
                    await context.DeleteAsync(setGroup).ConfigureAwait(false);
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                this.ApplyToGameServers(number, null);

                cosmetics = await this.ChangeCosmeticsAsync(gameConfiguration, number, c => AddMissingCosmeticPieces(c, number), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "The skin set {Number} \"{Name}\" couldn't be saved.", number, name);
                await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
                return this.Conflict(new ConfigurationEditController.ErrorResponse(
                    $"No se pudo guardar: {(ex.InnerException ?? ex).Message}. Lo que se guardó antes del error se completa al volver a agregar el set con el número {number}."));
            }
        }
        finally
        {
            ConfigurationEditController.DataSourceLock.Release();
        }

        await this.AuditAsync(created ? AuditAction.Created : AuditAction.Updated, number, name, [new AuditChange("Set", "Set", null, $"{name} ({number.ToString(CultureInfo.InvariantCulture)})")]).ConfigureAwait(false);
        return this.StatusCode(created ? StatusCodes.Status201Created : StatusCodes.Status200OK, new SkinSetResult(number, name, created, cosmetics, this.CheckGameServers(number)));
    }

    /// <summary>
    /// Changes a skin set: renames its pieces, and changes how its cosmetic pieces look.
    /// </summary>
    /// <param name="number">The item number of the pieces.</param>
    /// <param name="request">The request.</param>
    /// <returns>The result, with the state of each game server.</returns>
    [HttpPut("{number:int}")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<SkinSetResult>> UpdateAsync(short number, [FromBody] UpdateSkinSetRequest request)
    {
        if (ValidateName(request.Name, out var name) is { } invalidName)
        {
            return invalidName;
        }

        if (request.Level > MaximumLevel)
        {
            return Invalid("level", $"El nivel tiene que estar entre 0 y {MaximumLevel}.");
        }

        var cancellationToken = this.HttpContext.RequestAborted;
        string oldName;
        CosmeticsState cosmetics;
        await ConfigurationEditController.DataSourceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var gameConfiguration = await this._dataSource.GetOwnerAsync(default, cancellationToken).ConfigureAwait(false);
            var context = await this._dataSource.GetContextAsync(cancellationToken).ConfigureAwait(false);
            var pieces = SkinArmorSetBuilder.PieceGroups.Select(group => SkinArmorSetBuilder.FindItem(gameConfiguration, group, number)).ToList();
            if (pieces.Any(p => p is null) || !pieces.All(p => SkinArmorSetBuilder.IsSkinOnly(p!)))
            {
                return this.NotFound(new ConfigurationEditController.ErrorResponse($"No hay un set de skins con el número {number}."));
            }

            oldName = GetSetName(pieces!);
            try
            {
                if (oldName != name)
                {
                    foreach (var piece in pieces)
                    {
                        piece!.Name = SkinArmorSetBuilder.GetPieceName(name, piece.Group);
                    }

                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    this.ApplyToGameServers(number, name);
                }

                cosmetics = await this.ChangeCosmeticsAsync(
                    gameConfiguration,
                    number,
                    c =>
                    {
                        var changed = AddMissingCosmeticPieces(c, number);
                        foreach (var piece in c.Pieces.Where(p => p.ItemNumber == number && (p.Level != request.Level || p.IsExcellent != request.IsExcellent)))
                        {
                            piece.Level = request.Level;
                            piece.IsExcellent = request.IsExcellent;
                            changed = true;
                        }

                        return changed;
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "The skin set {Number} couldn't be changed.", number);
                await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
                return this.Conflict(new ConfigurationEditController.ErrorResponse($"No se pudo guardar: {(ex.InnerException ?? ex).Message}."));
            }
        }
        finally
        {
            ConfigurationEditController.DataSourceLock.Release();
        }

        var look = $"nivel {request.Level.ToString(CultureInfo.InvariantCulture)}{(request.IsExcellent ? ", brillo excelente" : string.Empty)}";
        await this.AuditAsync(AuditAction.Updated, number, name, [new AuditChange("Name", "Nombre", oldName, name), new AuditChange("Look", "Apariencia", null, look)]).ConfigureAwait(false);
        return new SkinSetResult(number, name, false, cosmetics, this.CheckGameServers(number));
    }

    /// <summary>
    /// Deletes a skin set: its pieces, its cosmetic pieces, and the cosmetic pieces which the characters unlocked.
    /// </summary>
    /// <remarks>
    /// It fails when a character has a piece as an item (e.g. created with /item), because the item would lose its
    /// definition. The cosmetic pieces are taken from all characters, so that a new set with the same number doesn't
    /// give them its pieces.
    /// </remarks>
    /// <param name="number">The item number of the pieces.</param>
    /// <returns>The result.</returns>
    [HttpDelete("{number:int}")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<DeleteSkinSetResult>> DeleteAsync(short number)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        string name;
        IReadOnlyList<short> removedPieces = [];
        await ConfigurationEditController.DataSourceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var gameConfiguration = await this._dataSource.GetOwnerAsync(default, cancellationToken).ConfigureAwait(false);
            var context = await this._dataSource.GetContextAsync(cancellationToken).ConfigureAwait(false);
            var pieces = SkinArmorSetBuilder.PieceGroups.Select(group => SkinArmorSetBuilder.FindItem(gameConfiguration, group, number)).ToList();
            if (pieces.Any(p => p is null) || !pieces.All(p => SkinArmorSetBuilder.IsSkinOnly(p!)))
            {
                return this.NotFound(new ConfigurationEditController.ErrorResponse($"No hay un set de skins con el número {number}."));
            }

            name = GetSetName(pieces);
            try
            {
                foreach (var piece in pieces)
                {
                    gameConfiguration.Items.Remove(piece!);
                    await context.DeleteAsync(piece!).ConfigureAwait(false);
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
                if ((ex as DbException ?? ex.InnerException as DbException)?.SqlState == ForeignKeyViolation)
                {
                    return this.Conflict(new ConfigurationEditController.ErrorResponse(
                        $"Algún personaje tiene una pieza de {name} como ítem (por ejemplo, creada con /item). Sacásela y volvé a intentar."));
                }

                this._logger.LogError(ex, "The skin set {Number} couldn't be deleted.", number);
                return this.Conflict(new ConfigurationEditController.ErrorResponse($"No se pudo borrar: {(ex.InnerException ?? ex).Message}."));
            }

            this.RemoveFromGameServers(number);
            await this.ChangeCosmeticsAsync(
                gameConfiguration,
                number,
                c =>
                {
                    removedPieces = c.Pieces.Where(p => p.ItemNumber == number).Select(p => p.Number).ToList();
                    return c.Pieces.Where(p => p.ItemNumber == number).ToList().Count(c.Pieces.Remove) > 0;
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ConfigurationEditController.DataSourceLock.Release();
        }

        var revoked = await this.RevokeCosmeticPiecesAsync(removedPieces, cancellationToken).ConfigureAwait(false);
        await this.AuditAsync(AuditAction.Deleted, number, name, [new AuditChange("Set", "Set", $"{name} ({number.ToString(CultureInfo.InvariantCulture)})", null)]).ConfigureAwait(false);
        return new DeleteSkinSetResult(number, name, revoked);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"^[\p{L}\p{N} ]+$")]
    private static partial Regex AllowedName();

    private static BadRequestObjectResult Invalid(string field, string message) =>
        new(new ConfigurationEditController.ErrorResponse("Hay valores inválidos.", [new FieldError(field, message)]));

    private static BadRequestObjectResult? ValidateName(string? requested, out string name)
    {
        name = WhiteSpace().Replace(requested ?? string.Empty, " ").Trim();
        return name.Length is < 2 or > MaximumNameLength || !AllowedName().IsMatch(name)
            ? Invalid("name", $"El nombre tiene que tener entre 2 y {MaximumNameLength} letras, números o espacios.")
            : null;
    }

    /// <summary>
    /// Gets the name of a set from the name of its armor, e.g. "Toranaga" from "Toranaga Armor".
    /// </summary>
    private static string GetSetName(IEnumerable<ItemDefinition?> pieces)
    {
        var armor = pieces.First(p => p?.Group == SkinArmorSetBuilder.PieceGroups[1])!.Name.Value ?? string.Empty;
        const string suffix = " Armor";
        return armor.EndsWith(suffix, StringComparison.Ordinal) ? armor[..^suffix.Length] : armor;
    }

    private static short? FindFreeNumber(GameConfiguration gameConfiguration)
    {
        var used = gameConfiguration.Items.Where(i => SkinArmorSetBuilder.PieceGroups.Contains(i.Group)).Select(i => i.Number).ToHashSet();
        return Enumerable.Range(0, SkinArmorSetBuilder.MaximumNumber + 1).Select(n => (short)n).Where(n => !used.Contains(n)).Select(n => (short?)n).FirstOrDefault();
    }

    private static IEnumerable<CosmeticPieceDefinition> FindCosmeticPieces(CosmeticsConfiguration configuration, short number) =>
        configuration.Pieces.Where(p => p.ItemNumber == number).OrderBy(p => p.Slot);

    private static IReadOnlyList<CosmeticPiece> ToCosmeticPieces(CosmeticsConfiguration? configuration, short number) =>
        configuration is null
            ? []
            : FindCosmeticPieces(configuration, number).Select(p => new CosmeticPiece(p.Number, SlotNames[(int)p.Slot], p.Level, p.IsExcellent)).ToList();

    /// <summary>
    /// Adds the cosmetic pieces of the set which are missing.
    /// </summary>
    /// <returns><c>true</c>, if a piece was added.</returns>
    private static bool AddMissingCosmeticPieces(CosmeticsConfiguration configuration, short number)
    {
        var changed = false;
        foreach (var slot in CosmeticSlotExtensions.All)
        {
            if (configuration.Pieces.Any(p => p.Slot == slot && p.ItemNumber == number))
            {
                continue;
            }

            var pieceNumber = (short)((number * CosmeticNumbersPerSet) + (int)slot + 1);
            if (configuration.FindPiece(pieceNumber) is not null)
            {
                pieceNumber = (short)(configuration.Pieces.Max(p => p.Number) + 1);
            }

            configuration.Pieces.Add(new CosmeticPieceDefinition { Number = pieceNumber, Slot = slot, ItemNumber = number });
            changed = true;
        }

        return changed;
    }

    private Task AuditAsync(AuditAction action, short number, string name, IReadOnlyList<AuditChange> changes) =>
        this._auditLog.AddAsync(new AuditEntry(DateTimeOffset.UtcNow, this.User.Identity?.Name, action, AuditEntityType, AuditEntityCaption, Guid.Empty, $"{name} ({number})", changes));

    /// <summary>
    /// Reads the configuration of the cosmetics plugin; <c>null</c>, if it isn't installed.
    /// </summary>
    private CosmeticsConfiguration? ReadCosmetics(GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.PlugInConfigurations.FirstOrDefault(p => p.TypeId == typeof(CosmeticsPlugIn).GUID) is not { } plugIn)
        {
            return null;
        }

        try
        {
            return plugIn.GetConfiguration<CosmeticsConfiguration>(new ByDataSourceReferenceHandler(this._dataSource)) ?? CosmeticsConfiguration.Default;
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "The configuration of the cosmetics plugin couldn't be read.");
            return null;
        }
    }

    /// <summary>
    /// Applies the changes of a set to the configuration which the game servers of this process use: takes the stats
    /// off its pieces, removes its set groups and, if a name is given, renames its pieces.
    /// </summary>
    /// <remarks>
    /// New pieces reach the game servers through the configuration change listener, but the changes of pieces
    /// which already existed don't always: the removed links to set groups and options are rows of join tables,
    /// whose changes aren't published, and the cache doesn't update the instances which were added after it was
    /// loaded. So the changes are applied directly as well; where they already arrived, it changes nothing.
    /// </remarks>
    private void ApplyToGameServers(short number, string? name)
    {
        var configurations = this._serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context.Configuration).Distinct();
        foreach (var configuration in configurations)
        {
            foreach (var group in SkinArmorSetBuilder.PieceGroups)
            {
                if (SkinArmorSetBuilder.FindItem(configuration, group, number) is { } piece)
                {
                    SkinArmorSetBuilder.MakeSkinOnly(piece);
                    if (name is not null)
                    {
                        piece.Name = SkinArmorSetBuilder.GetPieceName(name, group);
                    }
                }
            }

            SkinArmorSetBuilder.RemoveSetGroups(configuration, number);
        }
    }

    /// <summary>
    /// Removes the pieces and set groups of a set from the configuration which the game servers of this process use,
    /// because deleted configuration doesn't always reach them (see <see cref="ApplyToGameServers"/>).
    /// </summary>
    private void RemoveFromGameServers(short number)
    {
        var configurations = this._serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context.Configuration).Distinct();
        foreach (var configuration in configurations)
        {
            foreach (var group in SkinArmorSetBuilder.PieceGroups)
            {
                if (SkinArmorSetBuilder.FindItem(configuration, group, number) is { } piece)
                {
                    configuration.Items.Remove(piece);
                }
            }

            SkinArmorSetBuilder.RemoveSetGroups(configuration, number);
        }
    }

    /// <summary>
    /// Takes cosmetic pieces from all characters: from the ones in the game through the cosmetics plugin, so that
    /// they see it at once, and from the others in the storage.
    /// </summary>
    /// <returns>The number of unlocked pieces which have been removed from the characters which weren't in the game.</returns>
    private async Task<int> RevokeCosmeticPiecesAsync(IReadOnlyList<short> pieceNumbers, CancellationToken cancellationToken)
    {
        if (pieceNumbers.Count == 0)
        {
            return 0;
        }

        foreach (var context in this._serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context))
        {
            if (CosmeticsPlugIn.GetActivePlugIn(context) is not { } plugIn)
            {
                continue;
            }

            foreach (var player in await context.GetPlayersAsync().ConfigureAwait(false))
            {
                foreach (var pieceNumber in pieceNumbers)
                {
                    try
                    {
                        await plugIn.RevokeAsync(player, pieceNumber).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        this._logger.LogWarning(ex, "Couldn't take the cosmetic piece {Piece} from {Player}.", pieceNumber, player.Name);
                    }
                }
            }
        }

        try
        {
            return ProgressionRepositoryRegistry.Current is { } repository
                ? await repository.RemoveCosmeticPiecesOfAllCharactersAsync(pieceNumbers, cancellationToken).ConfigureAwait(false)
                : 0;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't take the cosmetic pieces {Pieces} from the characters.", pieceNumbers);
            return 0;
        }
    }

    /// <summary>
    /// Changes the configuration of the cosmetics plugin, like the plugins page changes it.
    /// </summary>
    private async Task<CosmeticsState> ChangeCosmeticsAsync(GameConfiguration gameConfiguration, short number, Func<CosmeticsConfiguration, bool> change, CancellationToken cancellationToken)
    {
        var plugInId = gameConfiguration.PlugInConfigurations.FirstOrDefault(p => p.TypeId == typeof(CosmeticsPlugIn).GUID)?.GetId();
        if (plugInId is null)
        {
            return new CosmeticsState(false, false, []);
        }

        var referenceHandler = new ByDataSourceReferenceHandler(this._dataSource);
        using var context = this._contextProvider.CreateNewTypedContext(typeof(PlugInConfiguration), true, gameConfiguration);
        if (await context.GetByIdAsync<PlugInConfiguration>(plugInId.Value, cancellationToken).ConfigureAwait(false) is not { } plugIn)
        {
            return new CosmeticsState(false, false, []);
        }

        var configuration = plugIn.GetConfiguration<CosmeticsConfiguration>(referenceHandler) ?? CosmeticsConfiguration.Default;
        if (change(configuration))
        {
            plugIn.SetConfiguration(configuration, referenceHandler);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // The data source reads the configuration of the plugin again, e.g. for the list of the sets.
            await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
        }

        return new CosmeticsState(true, plugIn.IsActive, ToCosmeticPieces(configuration, number));
    }

    /// <summary>
    /// Checks the set in the configuration which each game server uses: that every piece exists once, without
    /// stats, and that the cosmetics plugin is active and has the pieces.
    /// </summary>
    private IReadOnlyList<LiveState> CheckGameServers(short number)
    {
        var result = new List<LiveState>();
        foreach (var server in this._serverProvider.Servers.OfType<IGameServerContextProvider>())
        {
            var manageable = (IManageableServer)server;
            var configuration = server.Context.Configuration;
            var problems = new List<string>();
            foreach (var group in SkinArmorSetBuilder.PieceGroups)
            {
                var matches = configuration.Items.Where(i => i.Group == group && i.Number == number).ToList();
                if (matches.Count != 1)
                {
                    problems.Add(matches.Count == 0 ? $"Falta la pieza del grupo {group}." : $"La pieza del grupo {group} está {matches.Count} veces.");
                    continue;
                }

                var piece = matches[0];
                if (piece.DropsFromMonsters)
                {
                    problems.Add($"\"{piece.Name.Value}\" cae de los monstruos.");
                }

                if (piece.BasePowerUpAttributes.Count > 0 || piece.PossibleItemOptions.Count > 0 || piece.PossibleItemSetGroups.Count > 0)
                {
                    problems.Add($"\"{piece.Name.Value}\" tiene stats, opciones o bonos de set.");
                }
            }

            if (CosmeticsPlugIn.GetActivePlugIn(server.Context) is not { } plugIn)
            {
                problems.Add("El plugin de cosméticos no está activo.");
            }
            else if (plugIn.Configuration is not { } cosmetics || FindCosmeticPieces(cosmetics, number).Select(p => p.Slot).Distinct().Count() != CosmeticSlotExtensions.All.Count)
            {
                problems.Add("El plugin de cosméticos todavía no tiene las 5 piezas.");
            }

            result.Add(new LiveState(manageable.Id, manageable.Description, problems.Count == 0, problems));
        }

        return result;
    }

    /// <summary>
    /// The skin sets.
    /// </summary>
    /// <param name="Sets">The skin sets, ordered by number.</param>
    /// <param name="NextFreeNumber">The number which a new set gets: the lowest which no armor piece uses; <c>null</c>, if there is none.</param>
    /// <param name="MaximumNumber">The highest item number of an item group.</param>
    /// <param name="CosmeticsInstalled"><c>true</c>, if the cosmetics plugin is installed.</param>
    public record SkinSetOverview(IReadOnlyList<SkinSet> Sets, short? NextFreeNumber, short MaximumNumber, bool CosmeticsInstalled);

    /// <summary>
    /// A skin set.
    /// </summary>
    /// <param name="Number">The item number of the pieces.</param>
    /// <param name="Name">The name of the set, e.g. "Toranaga".</param>
    /// <param name="Pieces">The names of the helm, armor, pants, gloves and boots.</param>
    /// <param name="Cosmetics">The cosmetic pieces of the set.</param>
    public record SkinSet(short Number, string Name, IReadOnlyList<string> Pieces, IReadOnlyList<CosmeticPiece> Cosmetics);

    /// <summary>
    /// The request to add an armor set.
    /// </summary>
    /// <param name="Number">The item number of the pieces; <c>null</c> for the lowest free one.</param>
    /// <param name="Name">The name of the set, e.g. "Toranaga"; the pieces are named after it.</param>
    public record CreateSkinSetRequest(short? Number, string? Name);

    /// <summary>
    /// The request to change a skin set.
    /// </summary>
    /// <param name="Name">The name of the set; the pieces are renamed after it.</param>
    /// <param name="Level">The level which the cosmetic pieces show (0 to 15); from 7 on they glow.</param>
    /// <param name="IsExcellent">If set to <c>true</c>, the cosmetic pieces glow like excellent items.</param>
    public record UpdateSkinSetRequest(string? Name, byte Level, bool IsExcellent);

    /// <summary>
    /// The result of adding or changing an armor set.
    /// </summary>
    /// <param name="Number">The item number of the pieces.</param>
    /// <param name="Name">The name of the set.</param>
    /// <param name="Created"><c>true</c>, if pieces were created.</param>
    /// <param name="Cosmetics">The cosmetic pieces of the set.</param>
    /// <param name="Servers">The state of each game server.</param>
    public record SkinSetResult(short Number, string Name, bool Created, CosmeticsState Cosmetics, IReadOnlyList<LiveState> Servers);

    /// <summary>
    /// The result of deleting a skin set.
    /// </summary>
    /// <param name="Number">The item number of the pieces.</param>
    /// <param name="Name">The name of the set.</param>
    /// <param name="RevokedPieces">The number of unlocked cosmetic pieces which were taken from characters which weren't in the game.</param>
    public record DeleteSkinSetResult(short Number, string Name, int RevokedPieces);

    /// <summary>
    /// The cosmetic pieces of a set in the configuration of the cosmetics plugin.
    /// </summary>
    /// <param name="IsInstalled"><c>true</c>, if the cosmetics plugin is installed; otherwise the pieces couldn't be added.</param>
    /// <param name="IsActive"><c>true</c>, if the cosmetics plugin is active.</param>
    /// <param name="Pieces">The pieces, from the helm to the boots.</param>
    public record CosmeticsState(bool IsInstalled, bool IsActive, IReadOnlyList<CosmeticPiece> Pieces);

    /// <summary>
    /// A cosmetic piece of a set.
    /// </summary>
    /// <param name="Number">The number of the piece, which /darcosmetico takes.</param>
    /// <param name="Slot">The name of the slot, in Spanish.</param>
    /// <param name="Level">The level which the piece shows.</param>
    /// <param name="IsExcellent"><c>true</c>, if the piece glows like an excellent item.</param>
    public record CosmeticPiece(short Number, string Slot, byte Level, bool IsExcellent);

    /// <summary>
    /// Whether a game server has the set.
    /// </summary>
    /// <param name="ServerId">The id of the game server.</param>
    /// <param name="ServerName">The name of the game server.</param>
    /// <param name="IsReady"><c>true</c>, if the game server has the whole set.</param>
    /// <param name="Problems">What's missing or wrong, in Spanish.</param>
    public record LiveState(int ServerId, string ServerName, bool IsReady, IReadOnlyList<string> Problems);
}
