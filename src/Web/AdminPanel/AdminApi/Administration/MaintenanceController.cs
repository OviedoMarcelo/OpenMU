// <copyright file="MaintenanceController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Administration;

using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// The updates of the data and the backups, like the updates page and the backup downloads of the
/// admin panel. The initialization of the database (the setup page) is left out on purpose: it
/// deletes all data.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/maintenance")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Administrator)]
public class MaintenanceController : ControllerBase
{
    private readonly DataUpdateService _updateService;
    private readonly IBackupService _backupService;
    private readonly IDataSource<GameConfiguration> _dataSource;
    private readonly AdminAuditLog _auditLog;
    private readonly ILogger<MaintenanceController> _logger;
    private readonly IDatabaseSnapshotService? _snapshotService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaintenanceController"/> class.
    /// </summary>
    /// <param name="updateService">The service of the data updates.</param>
    /// <param name="backupService">The backup service.</param>
    /// <param name="dataSource">The data source of the game configuration, which is loaded again after updates.</param>
    /// <param name="auditLog">The audit log.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="snapshotService">The snapshot service, if the persistence supports it.</param>
    public MaintenanceController(
        DataUpdateService updateService,
        IBackupService backupService,
        IDataSource<GameConfiguration> dataSource,
        AdminAuditLog auditLog,
        ILogger<MaintenanceController> logger,
        IDatabaseSnapshotService? snapshotService = null)
    {
        this._updateService = updateService;
        this._backupService = backupService;
        this._dataSource = dataSource;
        this._auditLog = auditLog;
        this._logger = logger;
        this._snapshotService = snapshotService;
    }

    /// <summary>
    /// Gets the updates of the data which aren't installed yet.
    /// </summary>
    /// <returns>The updates, in the order in which they're installed.</returns>
    [HttpGet("updates")]
    public async Task<IEnumerable<DataUpdate>> GetUpdatesAsync()
    {
        var updates = await this._updateService.DetermineAvailableUpdatesAsync().ConfigureAwait(false);
        return updates.Select(u => new DataUpdate(u.Key, u.Name, u.Description, u.IsMandatory, u.CreatedAt)).ToList();
    }

    /// <summary>
    /// Installs updates of the data. The updates which they depend on have to be installed or selected.
    /// </summary>
    /// <param name="request">The keys of the updates.</param>
    /// <returns>The installed updates, and the error, if one failed.</returns>
    [HttpPost("updates")]
    public async Task<ActionResult<UpdateResult>> InstallUpdatesAsync([FromBody] InstallRequest request)
    {
        var available = await this._updateService.DetermineAvailableUpdatesAsync().ConfigureAwait(false);
        var selected = available.Where(u => request.Keys.Contains(u.Key)).ToList();
        if (selected.Count == 0)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("No se eligió ninguna actualización pendiente."));
        }

        var installed = new List<Guid>();
        var progress = new SynchronousProgress(args =>
        {
            if (args.IsCompleted && args.CurrentUpdatingKey != Guid.Empty)
            {
                installed.Add(args.CurrentUpdatingKey);
            }
        });

        string? error = null;
        try
        {
            await this._updateService.ApplyUpdatesAsync(selected, progress).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Installing the data updates failed.");
            error = ex.Message;
        }

        await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
        var names = selected.Where(u => installed.Contains(u.Key)).Select(u => u.Name).ToList();
        if (names.Count > 0)
        {
            await this._auditLog.AddAsync(new AuditEntry(
                DateTimeOffset.UtcNow,
                this.User.Identity?.Name,
                AuditAction.Created,
                "DataUpdate",
                "Actualizaciones de datos",
                Guid.Empty,
                string.Join(", ", names),
                [])).ConfigureAwait(false);
        }

        return new UpdateResult(installed, error);
    }

    /// <summary>
    /// Downloads a backup of the configuration and, optionally, of the accounts.
    /// </summary>
    /// <param name="includeAccounts">If set to <c>true</c>, the accounts are part of the backup.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The backup as a zip file.</returns>
    [HttpGet("backup")]
    public async Task<IActionResult> DownloadBackupAsync([FromQuery] bool includeAccounts, CancellationToken cancellationToken)
    {
        var stream = new MemoryStream();
        await this._backupService.CreateBackupAsync(stream, new BackupOptions { IncludeAccounts = includeAccounts }, cancellationToken).ConfigureAwait(false);
        stream.Position = 0;
        await this.AuditDownloadAsync(includeAccounts ? "Backup con cuentas" : "Backup de la configuración").ConfigureAwait(false);
        return this.File(stream, "application/zip", $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
    }

    /// <summary>
    /// Downloads a snapshot of the database, which can only be restored into a database with the same schema.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot as a zip file.</returns>
    [HttpGet("snapshot")]
    public async Task<IActionResult> DownloadSnapshotAsync(CancellationToken cancellationToken)
    {
        if (this._snapshotService is not { } snapshotService)
        {
            return this.NotFound(new ConfigurationEditController.ErrorResponse("La base de datos no permite snapshots."));
        }

        var stream = new MemoryStream();
        await snapshotService.CreateSnapshotAsync(stream, cancellationToken).ConfigureAwait(false);
        stream.Position = 0;
        await this.AuditDownloadAsync("Snapshot de la base").ConfigureAwait(false);
        return this.File(stream, "application/zip", $"snapshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
    }

    private Task AuditDownloadAsync(string what)
    {
        return this._auditLog.AddAsync(new AuditEntry(DateTimeOffset.UtcNow, this.User.Identity?.Name, AuditAction.Created, "Backup", "Backups", Guid.Empty, what, []));
    }

    /// <summary>
    /// An update of the data.
    /// </summary>
    /// <param name="Key">The key.</param>
    /// <param name="Name">The name.</param>
    /// <param name="Description">The description.</param>
    /// <param name="IsMandatory">If set to <c>true</c>, the update is required for the server to work correctly.</param>
    /// <param name="CreatedAt">When the update was created.</param>
    public record DataUpdate(Guid Key, string Name, string Description, bool IsMandatory, DateTime CreatedAt);

    /// <summary>
    /// The updates to install.
    /// </summary>
    /// <param name="Keys">The keys of the updates.</param>
    public record InstallRequest(IReadOnlyList<Guid> Keys);

    /// <summary>
    /// The result of installing updates.
    /// </summary>
    /// <param name="Installed">The keys of the installed updates.</param>
    /// <param name="Error">The error, if an update failed; the following ones weren't installed.</param>
    public record UpdateResult(IReadOnlyList<Guid> Installed, string? Error);

    /// <summary>
    /// A progress which reports synchronously, unlike <see cref="Progress{T}"/>, which posts to the
    /// synchronization context - the result has to be complete when the updates are done.
    /// </summary>
    private sealed class SynchronousProgress : IProgress<(Guid CurrentUpdatingKey, bool IsCompleted)>
    {
        private readonly Action<(Guid CurrentUpdatingKey, bool IsCompleted)> _report;

        public SynchronousProgress(Action<(Guid CurrentUpdatingKey, bool IsCompleted)> report) => this._report = report;

        public void Report((Guid CurrentUpdatingKey, bool IsCompleted) value) => this._report(value);
    }
}
