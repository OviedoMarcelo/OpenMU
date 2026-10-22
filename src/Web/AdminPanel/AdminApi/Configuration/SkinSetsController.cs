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
/// Adds and edits the skins of skin packs, which are worn as cosmetic pieces, while the game servers run: armor sets
/// (see <see cref="SkinArmorSetBuilder"/>), wings and pets (see <see cref="SkinItemBuilder"/>).
/// </summary>
/// <remarks>
/// The items are built on the context of the data source, which has the whole configuration loaded, like the edit
/// pages of the admin panel. Saving publishes new items to the running game servers through the configuration change
/// listener, so no restart is needed; changes of existing items are applied to the game servers directly as well,
/// because they don't always reach them (see <see cref="ApplyToGameServers"/>). The cosmetic pieces of a skin are
/// kept in the configuration of the <see cref="CosmeticsPlugIn"/>, which reaches the game servers like the changes of
/// the plugins page. Afterwards, every game server of this process is checked for the skin.
/// </remarks>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/skin-sets")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public partial class SkinSetsController : ControllerBase
{
    private const string AuditEntityType = "Skin";

    private const string AuditEntityCaption = "Skin";

    private const int MaximumNameLength = 40;

    private const byte MaximumLevel = 15;

    /// <summary>
    /// The SQL state of a violated foreign key, e.g. of an item whose definition would be deleted.
    /// </summary>
    private const string ForeignKeyViolation = "23503";

    private static readonly IReadOnlyDictionary<CosmeticSlot, string> SlotNames = new Dictionary<CosmeticSlot, string>
    {
        [CosmeticSlot.Helm] = "Casco",
        [CosmeticSlot.Armor] = "Armadura",
        [CosmeticSlot.Pants] = "Pantalón",
        [CosmeticSlot.Gloves] = "Guantes",
        [CosmeticSlot.Boots] = "Botas",
        [CosmeticSlot.Wings] = "Alas",
        [CosmeticSlot.Pet] = "Mascota",
    };

    /// <summary>
    /// The pets and mounts which can have skins: the ones which the client draws as a pet or mount of its own.
    /// </summary>
    private static readonly short[] PetBaseNumbers = [0, 1, 2, 3, 4, 37];

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
    /// Gets the skins of all kinds, the number which a new skin of each kind gets, and the pets which can have skins.
    /// </summary>
    /// <returns>The overview.</returns>
    [HttpGet]
    public async Task<ActionResult<SkinSetOverview>> GetAsync()
    {
        var gameConfiguration = await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        var cosmetics = this.ReadCosmetics(gameConfiguration);
        var sets = SkinKind.All.SelectMany(kind => FindSkins(gameConfiguration, kind, cosmetics)).ToList();
        var freeNumbers = SkinKind.All.ToDictionary(k => k.Key, k => FindFreeNumber(gameConfiguration, k));
        var petBases = PetBaseNumbers
            .Select(n => SkinArmorSetBuilder.FindItem(gameConfiguration, SkinItemBuilder.PetGroup, n))
            .OfType<ItemDefinition>()
            .Select(i => new PetBase(i.Number, i.Name.Value ?? string.Empty))
            .ToList();
        return new SkinSetOverview(sets, freeNumbers, SkinArmorSetBuilder.MaximumNumber, cosmetics is not null, petBases);
    }

    /// <summary>
    /// Checks whether the game servers of this process have the skin.
    /// </summary>
    /// <param name="kind">The kind of the skin.</param>
    /// <param name="number">The item number.</param>
    /// <returns>The state of each game server.</returns>
    [HttpGet("{kind}/{number:int}/live")]
    public ActionResult<IReadOnlyList<LiveState>> GetLive(string kind, short number) =>
        SkinKind.Find(kind) is { } skinKind ? this.Ok(this.CheckGameServers(skinKind, number)) : this.NotFound();

    /// <summary>
    /// Adds a skin. Without a number, it gets the lowest free one of its kind. When the items with the number already
    /// exist and have the names of the skin, their stats are taken off, if they have any, and the missing cosmetic
    /// pieces are added.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The result, with the state of each game server.</returns>
    [HttpPost]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<SkinSetResult>> CreateAsync([FromBody] CreateSkinSetRequest request)
    {
        if (SkinKind.Find(request.Kind ?? SkinKind.Armor.Key) is not { } kind)
        {
            return Invalid("kind", "El tipo tiene que ser armadura, alas o mascota.");
        }

        if (ValidateName(request.Name, out var name) is { } invalidName)
        {
            return invalidName;
        }

        if (request.Number is { } requested && (requested < kind.FirstNumber || requested > SkinArmorSetBuilder.MaximumNumber))
        {
            return Invalid("number", $"El número tiene que estar entre {kind.FirstNumber} y {SkinArmorSetBuilder.MaximumNumber}.");
        }

        if (kind == SkinKind.Pet && (request.OnlyOver is not { } petBase || !PetBaseNumbers.Contains(petBase)))
        {
            return Invalid("onlyOver", "Elegí la mascota o montura de la que es skin.");
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

            if ((request.Number ?? FindFreeNumber(gameConfiguration, kind)) is not { } wanted)
            {
                return this.Conflict(new ConfigurationEditController.ErrorResponse("No queda ningún número libre."));
            }

            number = wanted;
            foreach (var group in kind.Groups)
            {
                if (SkinArmorSetBuilder.FindItem(gameConfiguration, group, number) is { } existing
                    && existing.Name.Value != kind.GetPieceName(name, group))
                {
                    return this.Conflict(new ConfigurationEditController.ErrorResponse($"El número {number} ya lo usa \"{existing.Name.Value}\"."));
                }
            }

            var template = GetTemplate(gameConfiguration, kind, request.OnlyOver);
            if (kind.IsSingleItem && template is null)
            {
                return this.Conflict(new ConfigurationEditController.ErrorResponse("No encontré el ítem en el que se basa la skin."));
            }

            created = kind.Groups.Any(group => SkinArmorSetBuilder.FindItem(gameConfiguration, group, number) is null);
            try
            {
                if (kind.IsSingleItem)
                {
                    SkinItemBuilder.Build(context, gameConfiguration, kind.Groups[0], number, name, template!);
                }
                else
                {
                    SkinArmorSetBuilder.Build(context, gameConfiguration, number, name);
                    foreach (var setGroup in SkinArmorSetBuilder.RemoveSetGroups(gameConfiguration, number))
                    {
                        await context.DeleteAsync(setGroup).ConfigureAwait(false);
                    }
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                this.ApplyToGameServers(kind, number, null);

                var onlyOver = kind == SkinKind.Pet ? request.OnlyOver : null;
                cosmetics = await this.ChangeCosmeticsAsync(gameConfiguration, kind, number, c => AddMissingCosmeticPieces(c, kind, number, onlyOver), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "The {Kind} skin {Number} \"{Name}\" couldn't be saved.", kind.Key, number, name);
                await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
                return this.Conflict(new ConfigurationEditController.ErrorResponse(
                    $"No se pudo guardar: {(ex.InnerException ?? ex).Message}. Lo que se guardó antes del error se completa al volver a agregarla con el número {number}."));
            }
        }
        finally
        {
            ConfigurationEditController.DataSourceLock.Release();
        }

        await this.AuditAsync(created ? AuditAction.Created : AuditAction.Updated, kind, number, name, [new AuditChange("Skin", "Skin", null, $"{name} ({number.ToString(CultureInfo.InvariantCulture)})")]).ConfigureAwait(false);
        return this.StatusCode(created ? StatusCodes.Status201Created : StatusCodes.Status200OK, new SkinSetResult(kind.Key, number, name, created, cosmetics, this.CheckGameServers(kind, number)));
    }

    /// <summary>
    /// Changes a skin: renames its items, changes how its cosmetic pieces look and, for a pet, over which pet it's shown.
    /// </summary>
    /// <param name="kind">The kind of the skin.</param>
    /// <param name="number">The item number.</param>
    /// <param name="request">The request.</param>
    /// <returns>The result, with the state of each game server.</returns>
    [HttpPut("{kind}/{number:int}")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<SkinSetResult>> UpdateAsync(string kind, short number, [FromBody] UpdateSkinSetRequest request)
    {
        if (SkinKind.Find(kind) is not { } skinKind)
        {
            return this.NotFound();
        }

        if (ValidateName(request.Name, out var name) is { } invalidName)
        {
            return invalidName;
        }

        if (request.Level > MaximumLevel)
        {
            return Invalid("level", $"El nivel tiene que estar entre 0 y {MaximumLevel}.");
        }

        if (skinKind == SkinKind.Pet && (request.OnlyOver is not { } petBase || !PetBaseNumbers.Contains(petBase)))
        {
            return Invalid("onlyOver", "Elegí la mascota o montura de la que es skin.");
        }

        var cancellationToken = this.HttpContext.RequestAborted;
        string oldName;
        CosmeticsState cosmetics;
        await ConfigurationEditController.DataSourceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var gameConfiguration = await this._dataSource.GetOwnerAsync(default, cancellationToken).ConfigureAwait(false);
            var context = await this._dataSource.GetContextAsync(cancellationToken).ConfigureAwait(false);
            if (FindSkinItems(gameConfiguration, skinKind, number) is not { } items)
            {
                return this.NotFound(new ConfigurationEditController.ErrorResponse($"No hay una skin con el número {number}."));
            }

            oldName = skinKind.GetName(items);
            try
            {
                if (oldName != name)
                {
                    foreach (var item in items)
                    {
                        item.Name = skinKind.GetPieceName(name, item.Group);
                    }

                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    this.ApplyToGameServers(skinKind, number, name);
                }

                var onlyOver = skinKind == SkinKind.Pet ? request.OnlyOver : null;
                cosmetics = await this.ChangeCosmeticsAsync(
                    gameConfiguration,
                    skinKind,
                    number,
                    c => AddMissingCosmeticPieces(c, skinKind, number, onlyOver) | ChangeLook(c, skinKind, number, request.Level, request.IsExcellent, onlyOver),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "The {Kind} skin {Number} couldn't be changed.", skinKind.Key, number);
                await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
                return this.Conflict(new ConfigurationEditController.ErrorResponse($"No se pudo guardar: {(ex.InnerException ?? ex).Message}."));
            }
        }
        finally
        {
            ConfigurationEditController.DataSourceLock.Release();
        }

        var look = $"nivel {request.Level.ToString(CultureInfo.InvariantCulture)}{(request.IsExcellent ? ", brillo excelente" : string.Empty)}";
        await this.AuditAsync(AuditAction.Updated, skinKind, number, name, [new AuditChange("Name", "Nombre", oldName, name), new AuditChange("Look", "Apariencia", null, look)]).ConfigureAwait(false);
        return new SkinSetResult(skinKind.Key, number, name, false, cosmetics, this.CheckGameServers(skinKind, number));
    }

    /// <summary>
    /// Deletes a skin: its items, its cosmetic pieces, and the cosmetic pieces which the characters unlocked.
    /// </summary>
    /// <remarks>
    /// It fails when a character has one of the items (e.g. created with /item), because the item would lose its
    /// definition. The cosmetic pieces are taken from all characters, so that a new skin with the same number doesn't
    /// give them its pieces.
    /// </remarks>
    /// <param name="kind">The kind of the skin.</param>
    /// <param name="number">The item number.</param>
    /// <returns>The result.</returns>
    [HttpDelete("{kind}/{number:int}")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<DeleteSkinSetResult>> DeleteAsync(string kind, short number)
    {
        if (SkinKind.Find(kind) is not { } skinKind)
        {
            return this.NotFound();
        }

        var cancellationToken = this.HttpContext.RequestAborted;
        string name;
        IReadOnlyList<short> removedPieces = [];
        await ConfigurationEditController.DataSourceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var gameConfiguration = await this._dataSource.GetOwnerAsync(default, cancellationToken).ConfigureAwait(false);
            var context = await this._dataSource.GetContextAsync(cancellationToken).ConfigureAwait(false);
            if (FindSkinItems(gameConfiguration, skinKind, number) is not { } items)
            {
                return this.NotFound(new ConfigurationEditController.ErrorResponse($"No hay una skin con el número {number}."));
            }

            name = skinKind.GetName(items);
            try
            {
                foreach (var item in items)
                {
                    gameConfiguration.Items.Remove(item);
                    await context.DeleteAsync(item).ConfigureAwait(false);
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
                if ((ex as DbException ?? ex.InnerException as DbException)?.SqlState == ForeignKeyViolation)
                {
                    return this.Conflict(new ConfigurationEditController.ErrorResponse(
                        $"Algún personaje tiene {name} como ítem (por ejemplo, creado con /item). Sacáselo y volvé a intentar."));
                }

                this._logger.LogError(ex, "The {Kind} skin {Number} couldn't be deleted.", skinKind.Key, number);
                return this.Conflict(new ConfigurationEditController.ErrorResponse($"No se pudo borrar: {(ex.InnerException ?? ex).Message}."));
            }

            this.RemoveFromGameServers(skinKind, number);
            await this.ChangeCosmeticsAsync(
                gameConfiguration,
                skinKind,
                number,
                c =>
                {
                    var pieces = FindCosmeticPieces(c, skinKind, number).ToList();
                    removedPieces = pieces.Select(p => p.Number).ToList();
                    return pieces.Count(c.Pieces.Remove) > 0;
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ConfigurationEditController.DataSourceLock.Release();
        }

        var revoked = await this.RevokeCosmeticPiecesAsync(removedPieces, cancellationToken).ConfigureAwait(false);
        await this.AuditAsync(AuditAction.Deleted, skinKind, number, name, [new AuditChange("Skin", "Skin", $"{name} ({number.ToString(CultureInfo.InvariantCulture)})", null)]).ConfigureAwait(false);
        return new DeleteSkinSetResult(skinKind.Key, number, name, revoked);
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
    /// Gets the item whose size and slot a skin of wings or a pet takes: the Wings of Elf, or the pet it's a skin of.
    /// </summary>
    private static ItemDefinition? GetTemplate(GameConfiguration gameConfiguration, SkinKind kind, short? onlyOver)
    {
        if (kind == SkinKind.Wings)
        {
            return SkinArmorSetBuilder.FindItem(gameConfiguration, SkinItemBuilder.WingsGroup, SkinItemBuilder.WingsTemplateNumber);
        }

        return kind == SkinKind.Pet && onlyOver is { } petBase
            ? SkinArmorSetBuilder.FindItem(gameConfiguration, SkinItemBuilder.PetGroup, petBase)
            : null;
    }

    /// <summary>
    /// Finds the items of a skin; <c>null</c>, if there isn't a whole skin without stats with the number.
    /// </summary>
    private static IReadOnlyList<ItemDefinition>? FindSkinItems(GameConfiguration gameConfiguration, SkinKind kind, short number)
    {
        if (number < kind.FirstNumber)
        {
            return null;
        }

        var items = kind.Groups.Select(group => SkinArmorSetBuilder.FindItem(gameConfiguration, group, number)).ToList();
        return items.All(i => i is not null && SkinArmorSetBuilder.IsSkinOnly(i)) ? items.OfType<ItemDefinition>().ToList() : null;
    }

    private static IEnumerable<SkinSet> FindSkins(GameConfiguration gameConfiguration, SkinKind kind, CosmeticsConfiguration? cosmetics) =>
        gameConfiguration.Items
            .Where(i => kind.Groups.Contains(i.Group) && i.Number >= kind.FirstNumber)
            .Select(i => i.Number)
            .Distinct()
            .Order()
            .Select(number => (Number: number, Items: FindSkinItems(gameConfiguration, kind, number)))
            .Where(s => s.Items is not null)
            .Select(s => new SkinSet(
                kind.Key,
                s.Number,
                kind.GetName(s.Items!),
                s.Items!.Select(i => i.Name.Value ?? string.Empty).ToList(),
                ToCosmeticPieces(cosmetics, kind, s.Number)));

    private static short? FindFreeNumber(GameConfiguration gameConfiguration, SkinKind kind)
    {
        var used = gameConfiguration.Items.Where(i => kind.Groups.Contains(i.Group)).Select(i => i.Number).ToHashSet();
        return Enumerable.Range(kind.FirstNumber, SkinArmorSetBuilder.MaximumNumber - kind.FirstNumber + 1)
            .Select(n => (short)n)
            .Where(n => !used.Contains(n))
            .Select(n => (short?)n)
            .FirstOrDefault();
    }

    private static IEnumerable<CosmeticPieceDefinition> FindCosmeticPieces(CosmeticsConfiguration configuration, SkinKind kind, short number) =>
        configuration.Pieces.Where(p => p.ItemNumber == number && kind.Slots.Contains(p.Slot)).OrderBy(p => p.Slot);

    private static IReadOnlyList<CosmeticPiece> ToCosmeticPieces(CosmeticsConfiguration? configuration, SkinKind kind, short number) =>
        configuration is null
            ? []
            : FindCosmeticPieces(configuration, kind, number).Select(p => new CosmeticPiece(p.Number, SlotNames[p.Slot], p.Level, p.IsExcellent, p.OnlyOverItemNumber)).ToList();

    /// <summary>
    /// Adds the cosmetic pieces of the skin which are missing.
    /// </summary>
    /// <returns><c>true</c>, if a piece was added.</returns>
    private static bool AddMissingCosmeticPieces(CosmeticsConfiguration configuration, SkinKind kind, short number, short? onlyOver)
    {
        var changed = false;
        foreach (var slot in kind.Slots)
        {
            if (configuration.Pieces.Any(p => p.Slot == slot && p.ItemNumber == number))
            {
                continue;
            }

            var pieceNumber = kind.GetCosmeticNumber(number, slot);
            if (configuration.FindPiece(pieceNumber) is not null)
            {
                pieceNumber = (short)(configuration.Pieces.Max(p => p.Number) + 1);
            }

            configuration.Pieces.Add(new CosmeticPieceDefinition { Number = pieceNumber, Slot = slot, ItemNumber = number, OnlyOverItemNumber = onlyOver });
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Changes how the cosmetic pieces of the skin look and over which item they're shown.
    /// </summary>
    /// <returns><c>true</c>, if a piece was changed.</returns>
    private static bool ChangeLook(CosmeticsConfiguration configuration, SkinKind kind, short number, byte level, bool isExcellent, short? onlyOver)
    {
        var changed = false;
        foreach (var piece in FindCosmeticPieces(configuration, kind, number).Where(p => p.Level != level || p.IsExcellent != isExcellent || p.OnlyOverItemNumber != onlyOver))
        {
            piece.Level = level;
            piece.IsExcellent = isExcellent;
            piece.OnlyOverItemNumber = onlyOver;
            changed = true;
        }

        return changed;
    }

    private Task AuditAsync(AuditAction action, SkinKind kind, short number, string name, IReadOnlyList<AuditChange> changes) =>
        this._auditLog.AddAsync(new AuditEntry(DateTimeOffset.UtcNow, this.User.Identity?.Name, action, AuditEntityType, AuditEntityCaption, Guid.Empty, $"{name} ({kind.Key} {number})", changes));

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

    private IEnumerable<GameConfiguration> GetGameServerConfigurations() =>
        this._serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context.Configuration).Distinct();

    /// <summary>
    /// Applies the changes of a skin to the configuration which the game servers of this process use: takes the stats
    /// off its items, removes the set groups of an armor set and, if a name is given, renames its items.
    /// </summary>
    /// <remarks>
    /// New items reach the game servers through the configuration change listener, but the changes of items
    /// which already existed don't always: the removed links to set groups and options are rows of join tables,
    /// whose changes aren't published, and the cache doesn't update the instances which were added after it was
    /// loaded. So the changes are applied directly as well; where they already arrived, it changes nothing.
    /// </remarks>
    private void ApplyToGameServers(SkinKind kind, short number, string? name)
    {
        foreach (var configuration in this.GetGameServerConfigurations())
        {
            foreach (var group in kind.Groups)
            {
                if (SkinArmorSetBuilder.FindItem(configuration, group, number) is not { } item)
                {
                    continue;
                }

                SkinArmorSetBuilder.MakeSkinOnly(item);
                if (name is not null)
                {
                    item.Name = kind.GetPieceName(name, group);
                }
            }

            if (!kind.IsSingleItem)
            {
                SkinArmorSetBuilder.RemoveSetGroups(configuration, number);
            }
        }
    }

    /// <summary>
    /// Removes the items of a skin from the configuration which the game servers of this process use,
    /// because deleted configuration doesn't always reach them (see <see cref="ApplyToGameServers"/>).
    /// </summary>
    private void RemoveFromGameServers(SkinKind kind, short number)
    {
        foreach (var configuration in this.GetGameServerConfigurations())
        {
            foreach (var group in kind.Groups)
            {
                if (SkinArmorSetBuilder.FindItem(configuration, group, number) is { } item)
                {
                    configuration.Items.Remove(item);
                }
            }

            if (!kind.IsSingleItem)
            {
                SkinArmorSetBuilder.RemoveSetGroups(configuration, number);
            }
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
    private async Task<CosmeticsState> ChangeCosmeticsAsync(GameConfiguration gameConfiguration, SkinKind kind, short number, Func<CosmeticsConfiguration, bool> change, CancellationToken cancellationToken)
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

            // The data source reads the configuration of the plugin again, e.g. for the list of the skins.
            await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
        }

        return new CosmeticsState(true, plugIn.IsActive, ToCosmeticPieces(configuration, kind, number));
    }

    /// <summary>
    /// Checks the skin in the configuration which each game server uses: that every item exists once, without
    /// stats, and that the cosmetics plugin is active and has its pieces.
    /// </summary>
    private IReadOnlyList<LiveState> CheckGameServers(SkinKind kind, short number)
    {
        var result = new List<LiveState>();
        foreach (var server in this._serverProvider.Servers.OfType<IGameServerContextProvider>())
        {
            var manageable = (IManageableServer)server;
            var configuration = server.Context.Configuration;
            var problems = new List<string>();
            foreach (var group in kind.Groups)
            {
                var matches = configuration.Items.Where(i => i.Group == group && i.Number == number).ToList();
                if (matches.Count != 1)
                {
                    problems.Add(matches.Count == 0 ? $"Falta el ítem del grupo {group}." : $"El ítem del grupo {group} está {matches.Count} veces.");
                    continue;
                }

                var item = matches[0];
                if (item.DropsFromMonsters)
                {
                    problems.Add($"\"{item.Name.Value}\" cae de los monstruos.");
                }

                if (!SkinArmorSetBuilder.IsSkinOnly(item))
                {
                    problems.Add($"\"{item.Name.Value}\" tiene stats, opciones o bonos de set.");
                }
            }

            if (CosmeticsPlugIn.GetActivePlugIn(server.Context) is not { } plugIn)
            {
                problems.Add("El plugin de cosméticos no está activo.");
            }
            else if (plugIn.Configuration is not { } cosmetics || FindCosmeticPieces(cosmetics, kind, number).Select(p => p.Slot).Distinct().Count() != kind.Slots.Count)
            {
                problems.Add($"El plugin de cosméticos todavía no tiene {(kind.IsSingleItem ? "la pieza" : "las 5 piezas")}.");
            }

            result.Add(new LiveState(manageable.Id, manageable.Description, problems.Count == 0, problems));
        }

        return result;
    }

    /// <summary>
    /// The skins.
    /// </summary>
    /// <param name="Sets">The skins of all kinds, ordered by kind and number.</param>
    /// <param name="NextFreeNumbers">The number which a new skin of each kind gets, by the key of the kind; <c>null</c>, if there is none.</param>
    /// <param name="MaximumNumber">The highest item number of an item group.</param>
    /// <param name="CosmeticsInstalled"><c>true</c>, if the cosmetics plugin is installed.</param>
    /// <param name="PetBases">The pets and mounts which can have skins.</param>
    public record SkinSetOverview(IReadOnlyList<SkinSet> Sets, IReadOnlyDictionary<string, short?> NextFreeNumbers, short MaximumNumber, bool CosmeticsInstalled, IReadOnlyList<PetBase> PetBases);

    /// <summary>
    /// A pet or mount which can have skins.
    /// </summary>
    /// <param name="Number">The item number in the group 13.</param>
    /// <param name="Name">The name.</param>
    public record PetBase(short Number, string Name);

    /// <summary>
    /// A skin.
    /// </summary>
    /// <param name="Kind">The kind: "armor", "wings" or "pet".</param>
    /// <param name="Number">The item number.</param>
    /// <param name="Name">The name, e.g. "Toranaga".</param>
    /// <param name="Pieces">The names of the items, e.g. the helm, armor, pants, gloves and boots.</param>
    /// <param name="Cosmetics">The cosmetic pieces.</param>
    public record SkinSet(string Kind, short Number, string Name, IReadOnlyList<string> Pieces, IReadOnlyList<CosmeticPiece> Cosmetics);

    /// <summary>
    /// The request to add a skin.
    /// </summary>
    /// <param name="Kind">The kind: "armor" (the default), "wings" or "pet".</param>
    /// <param name="Number">The item number; <c>null</c> for the lowest free one.</param>
    /// <param name="Name">The name, e.g. "Toranaga"; the items are named after it.</param>
    /// <param name="OnlyOver">For a pet: the number of the pet or mount it's a skin of, e.g. 0 for the Guardian Angel.</param>
    public record CreateSkinSetRequest(string? Kind, short? Number, string? Name, short? OnlyOver);

    /// <summary>
    /// The request to change a skin.
    /// </summary>
    /// <param name="Name">The name; the items are renamed after it.</param>
    /// <param name="Level">The level which the cosmetic pieces show (0 to 15); from 7 on they glow.</param>
    /// <param name="IsExcellent">If set to <c>true</c>, the cosmetic pieces glow like excellent items.</param>
    /// <param name="OnlyOver">For a pet: the number of the pet or mount it's a skin of.</param>
    public record UpdateSkinSetRequest(string? Name, byte Level, bool IsExcellent, short? OnlyOver);

    /// <summary>
    /// The result of adding or changing a skin.
    /// </summary>
    /// <param name="Kind">The kind.</param>
    /// <param name="Number">The item number.</param>
    /// <param name="Name">The name.</param>
    /// <param name="Created"><c>true</c>, if items were created.</param>
    /// <param name="Cosmetics">The cosmetic pieces.</param>
    /// <param name="Servers">The state of each game server.</param>
    public record SkinSetResult(string Kind, short Number, string Name, bool Created, CosmeticsState Cosmetics, IReadOnlyList<LiveState> Servers);

    /// <summary>
    /// The result of deleting a skin.
    /// </summary>
    /// <param name="Kind">The kind.</param>
    /// <param name="Number">The item number.</param>
    /// <param name="Name">The name.</param>
    /// <param name="RevokedPieces">The number of unlocked cosmetic pieces which were taken from characters which weren't in the game.</param>
    public record DeleteSkinSetResult(string Kind, short Number, string Name, int RevokedPieces);

    /// <summary>
    /// The cosmetic pieces of a skin in the configuration of the cosmetics plugin.
    /// </summary>
    /// <param name="IsInstalled"><c>true</c>, if the cosmetics plugin is installed; otherwise the pieces couldn't be added.</param>
    /// <param name="IsActive"><c>true</c>, if the cosmetics plugin is active.</param>
    /// <param name="Pieces">The pieces.</param>
    public record CosmeticsState(bool IsInstalled, bool IsActive, IReadOnlyList<CosmeticPiece> Pieces);

    /// <summary>
    /// A cosmetic piece of a skin.
    /// </summary>
    /// <param name="Number">The number of the piece, which /darcosmetico takes.</param>
    /// <param name="Slot">The name of the slot, in Spanish.</param>
    /// <param name="Level">The level which the piece shows.</param>
    /// <param name="IsExcellent"><c>true</c>, if the piece glows like an excellent item.</param>
    /// <param name="OnlyOver">The number of the item over which it's only shown; <c>null</c>: always.</param>
    public record CosmeticPiece(short Number, string Slot, byte Level, bool IsExcellent, short? OnlyOver);

    /// <summary>
    /// Whether a game server has the skin.
    /// </summary>
    /// <param name="ServerId">The id of the game server.</param>
    /// <param name="ServerName">The name of the game server.</param>
    /// <param name="IsReady"><c>true</c>, if the game server has the whole skin.</param>
    /// <param name="Problems">What's missing or wrong, in Spanish.</param>
    public record LiveState(int ServerId, string ServerName, bool IsReady, IReadOnlyList<string> Problems);
}
