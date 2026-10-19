// <copyright file="SkinSetsController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Items;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Adds the armor sets of skin packs (see <see cref="SkinArmorSetBuilder"/>) while the game servers run.
/// </summary>
/// <remarks>
/// The set is built on the context of the data source, which has the whole configuration loaded, like the edit
/// pages of the admin panel. Each step of the builder is saved on its own, which publishes it to the running game
/// servers through the configuration change listener, so no restart is needed. Afterwards, every game server of
/// this process is checked for the set.
/// </remarks>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/skin-sets")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public partial class SkinSetsController : ControllerBase
{
    private const string AuditEntityType = "SkinArmorSet";

    private const string AuditEntityCaption = "Set de skins";

    private const int MaximumNameLength = 40;

    private readonly IDataSource<GameConfiguration> _dataSource;
    private readonly IServerProvider _serverProvider;
    private readonly AdminAuditLog _auditLog;
    private readonly ILogger<SkinSetsController> _logger;
    /// <summary>
    /// Initializes a new instance of the <see cref="SkinSetsController"/> class.
    /// </summary>
    /// <param name="dataSource">The data source of the game configuration.</param>
    /// <param name="serverProvider">The server provider.</param>
    /// <param name="auditLog">The audit log.</param>
    /// <param name="logger">The logger.</param>
    public SkinSetsController(IDataSource<GameConfiguration> dataSource, IServerProvider serverProvider, AdminAuditLog auditLog, ILogger<SkinSetsController> logger)
    {
        this._dataSource = dataSource;
        this._serverProvider = serverProvider;
        this._auditLog = auditLog;
        this._logger = logger;
    }

    /// <summary>
    /// Gets the item numbers which the armor groups use, with the names of their pieces, and the lowest number
    /// which none of them uses.
    /// </summary>
    /// <returns>The overview.</returns>
    [HttpGet]
    public async Task<ActionResult<SkinSetOverview>> GetAsync()
    {
        var gameConfiguration = await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        var numbers = gameConfiguration.Items
            .Where(i => SkinArmorSetBuilder.PieceGroups.Contains(i.Group))
            .GroupBy(i => i.Number)
            .OrderBy(g => g.Key)
            .Select(g => new ArmorNumber(
                g.Key,
                SkinArmorSetBuilder.PieceGroups.Select(group => g.FirstOrDefault(i => i.Group == group)?.Name.Value).ToList()))
            .ToList();
        var used = numbers.Select(n => n.Number).ToHashSet();
        var free = Enumerable.Range(0, SkinArmorSetBuilder.MaximumNumber + 1).Where(n => !used.Contains((short)n)).Select(n => (short?)n).FirstOrDefault();
        return new SkinSetOverview(numbers, free, SkinArmorSetBuilder.MaximumNumber);
    }

    /// <summary>
    /// Checks whether the game servers of this process have the set with the number.
    /// </summary>
    /// <param name="number">The item number of the pieces.</param>
    /// <returns>The state of each game server.</returns>
    [HttpGet("{number:int}/live")]
    public ActionResult<IReadOnlyList<LiveState>> GetLive(short number) => this.Ok(this.CheckGameServers(number));

    /// <summary>
    /// Adds an armor set. When the pieces with the number already exist and have the names of the set, the set is
    /// completed, if it isn't yet.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The result, with the state of each game server.</returns>
    [HttpPost]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<CreateSkinSetResult>> CreateAsync([FromBody] CreateSkinSetRequest request)
    {
        var name = WhiteSpace().Replace(request.Name ?? string.Empty, " ").Trim();
        if (name.Length is < 2 or > MaximumNameLength || !AllowedName().IsMatch(name))
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse(
                "Hay valores inválidos.",
                [new FieldError("name", $"El nombre tiene que tener entre 2 y {MaximumNameLength} letras, números o espacios.")]));
        }

        if (request.Number is < 0 or > SkinArmorSetBuilder.MaximumNumber)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse(
                "Hay valores inválidos.",
                [new FieldError("number", $"El número tiene que estar entre 0 y {SkinArmorSetBuilder.MaximumNumber}.")]));
        }

        var number = request.Number;
        var cancellationToken = this.HttpContext.RequestAborted;
        bool created;
        await ConfigurationEditController.DataSourceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var gameConfiguration = await this._dataSource.GetOwnerAsync(default, cancellationToken).ConfigureAwait(false);
            var context = await this._dataSource.GetContextAsync(cancellationToken).ConfigureAwait(false);

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
                // Each step is saved on its own, so that it's published to the game servers before the next step references it.
                var pieces = SkinArmorSetBuilder.AddMissingPieces(context, gameConfiguration, number, name);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                var setGroups = SkinArmorSetBuilder.AddMissingSetGroups(context, gameConfiguration, pieces);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                if (SkinArmorSetBuilder.LinkSetGroups(pieces, setGroups))
                {
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                this.LinkSetGroupsOfGameServers(number);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "The skin set {Number} \"{Name}\" couldn't be saved.", number, name);
                await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
                return this.Conflict(new ConfigurationEditController.ErrorResponse(
                    $"No se pudo guardar: {(ex.InnerException ?? ex).Message}. Lo que se guardó antes del error se completa al volver a agregar el set."));
            }
        }
        finally
        {
            ConfigurationEditController.DataSourceLock.Release();
        }

        await this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            created ? AuditAction.Created : AuditAction.Updated,
            AuditEntityType,
            AuditEntityCaption,
            Guid.Empty,
            $"{name} ({number})",
            [new AuditChange("Set", "Set", null, $"{name} ({number.ToString(CultureInfo.InvariantCulture)})")])).ConfigureAwait(false);

        var states = this.CheckGameServers(number);
        return this.StatusCode(created ? StatusCodes.Status201Created : StatusCodes.Status200OK, new CreateSkinSetResult(number, name, created, states));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    /// <summary>
    /// Links the pieces to their set groups in the configuration which the game servers of this process use.
    /// </summary>
    /// <remarks>
    /// The links are rows of a join table, whose changes aren't published to the game servers. Publishing the pieces
    /// as changed doesn't help either: the cache only updates the instances which it had when it was loaded, not the
    /// ones which were added afterwards. The pieces and set groups themselves are already there, from the steps
    /// before, so they are linked directly.
    /// </remarks>
    private void LinkSetGroupsOfGameServers(short number)
    {
        var configurations = this._serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context.Configuration).Distinct();
        foreach (var configuration in configurations)
        {
            var pieces = SkinArmorSetBuilder.PieceGroups.Select(group => SkinArmorSetBuilder.FindItem(configuration, group, number)).OfType<ItemDefinition>().ToList();
            var setGroups = SkinArmorSetBuilder.FindSetGroups(configuration, number);
            if (pieces.Count == SkinArmorSetBuilder.PieceGroups.Count && setGroups.Count > 0)
            {
                SkinArmorSetBuilder.LinkSetGroups(pieces, setGroups);
            }
        }
    }

    [GeneratedRegex(@"^[\p{L}\p{N} ]+$")]
    private static partial Regex AllowedName();

    /// <summary>
    /// Checks the set in the configuration which each game server uses: that every piece exists once, with one
    /// defense value, and that it's linked to the set groups of the configuration (and not to copies of them).
    /// </summary>
    private IReadOnlyList<LiveState> CheckGameServers(short number)
    {
        var result = new List<LiveState>();
        foreach (var server in this._serverProvider.Servers.OfType<IGameServerContextProvider>())
        {
            var manageable = (IManageableServer)server;
            var configuration = server.Context.Configuration;
            var problems = new List<string>();
            var pieces = new List<ItemDefinition>();
            foreach (var group in SkinArmorSetBuilder.PieceGroups)
            {
                var matches = configuration.Items.Where(i => i.Group == group && i.Number == number).ToList();
                if (matches.Count != 1)
                {
                    problems.Add(matches.Count == 0 ? $"Falta la pieza del grupo {group}." : $"La pieza del grupo {group} está {matches.Count} veces.");
                    continue;
                }

                var piece = matches[0];
                pieces.Add(piece);
                var defenseValues = piece.BasePowerUpAttributes.Count(p => p.TargetAttribute?.Id == Stats.DefenseBase.Id);
                if (defenseValues != 1)
                {
                    problems.Add($"\"{piece.Name.Value}\" tiene {defenseValues} valores de defensa.");
                }

                if (piece.PossibleItemSetGroups.Count == 0)
                {
                    problems.Add($"\"{piece.Name.Value}\" no tiene los bonos de set completo.");
                }
                else if (piece.PossibleItemSetGroups.Any(g => !configuration.ItemSetGroups.Any(c => ReferenceEquals(c, g))))
                {
                    problems.Add($"\"{piece.Name.Value}\" usa bonos de set que no están en la configuración.");
                }
            }

            if (problems.Count == 0)
            {
                var setGroups = configuration.ItemSetGroups.Where(g => g.Items.Count > 0 && g.Items.All(i => i.ItemDefinition is { } item && item.Number == number && SkinArmorSetBuilder.PieceGroups.Contains(item.Group))).ToList();
                if (setGroups.Any(g => g.Items.Count != pieces.Count))
                {
                    problems.Add("Los bonos de set completo no tienen las 5 piezas.");
                }
            }

            result.Add(new LiveState(manageable.Id, manageable.Description, problems.Count == 0, problems));
        }

        return result;
    }

    /// <summary>
    /// The item numbers which the armor groups use.
    /// </summary>
    /// <param name="Numbers">The numbers, ordered.</param>
    /// <param name="NextFreeNumber">The lowest number which no armor group uses; <c>null</c>, if there is none.</param>
    /// <param name="MaximumNumber">The highest item number of an item group.</param>
    public record SkinSetOverview(IReadOnlyList<ArmorNumber> Numbers, short? NextFreeNumber, short MaximumNumber);

    /// <summary>
    /// An item number of the armor groups.
    /// </summary>
    /// <param name="Number">The number.</param>
    /// <param name="Pieces">The names of the helm, armor, pants, gloves and boots with this number; <c>null</c> where there is none.</param>
    public record ArmorNumber(short Number, IReadOnlyList<string?> Pieces);

    /// <summary>
    /// The request to add an armor set.
    /// </summary>
    /// <param name="Number">The item number of the pieces.</param>
    /// <param name="Name">The name of the set, e.g. "Pink Gold"; the pieces are named after it.</param>
    public record CreateSkinSetRequest(short Number, string? Name);

    /// <summary>
    /// The result of adding an armor set.
    /// </summary>
    /// <param name="Number">The item number of the pieces.</param>
    /// <param name="Name">The name of the set.</param>
    /// <param name="Created"><c>true</c>, if pieces were created; <c>false</c>, if they existed and the set was only completed, if needed.</param>
    /// <param name="Servers">The state of each game server.</param>
    public record CreateSkinSetResult(short Number, string Name, bool Created, IReadOnlyList<LiveState> Servers);

    /// <summary>
    /// Whether a game server has the set.
    /// </summary>
    /// <param name="ServerId">The id of the game server.</param>
    /// <param name="ServerName">The name of the game server.</param>
    /// <param name="IsReady"><c>true</c>, if the game server has the whole set.</param>
    /// <param name="Problems">What's missing or wrong, in Spanish.</param>
    public record LiveState(int ServerId, string ServerName, bool IsReady, IReadOnlyList<string> Problems);
}
