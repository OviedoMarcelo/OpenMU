// <copyright file="ConfigurationEditController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Creates, changes and deletes objects of the game configuration.
/// </summary>
/// <remarks>
/// Every change is made on its own typed context, like the "create" of the configuration grid of the
/// admin panel: nothing is saved when a value is invalid, and saving publishes the changes to the
/// running game servers through the configuration change listener, so no restart is needed.
/// Afterwards the cached configuration of the admin panel is discarded, so it's loaded again with the
/// changes. Every change is recorded in the <see cref="AdminAuditLog"/>.
/// </remarks>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/config")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
public class ConfigurationEditController : ControllerBase
{
    /// <summary>
    /// Changes on the context of the data source are made one after another; a context isn't thread safe.
    /// </summary>
    private static readonly SemaphoreSlim DataSourceLock = new(1, 1);

    private readonly IDataSource<GameConfiguration> _dataSource;
    private readonly IPersistenceContextProvider _contextProvider;
    private readonly ConfigurationTypeRegistry _registry;
    private readonly ConfigurationValueSerializer _serializer;
    private readonly ConfigurationValueWriter _writer;
    private readonly AdminAuditLog _auditLog;
    private readonly ILogger<ConfigurationEditController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationEditController"/> class.
    /// </summary>
    /// <param name="dataSource">The data source of the game configuration.</param>
    /// <param name="contextProvider">The persistence context provider.</param>
    /// <param name="registry">The type registry.</param>
    /// <param name="serializer">The serializer.</param>
    /// <param name="writer">The writer.</param>
    /// <param name="auditLog">The audit log.</param>
    /// <param name="logger">The logger.</param>
    public ConfigurationEditController(
        IDataSource<GameConfiguration> dataSource,
        IPersistenceContextProvider contextProvider,
        ConfigurationTypeRegistry registry,
        ConfigurationValueSerializer serializer,
        ConfigurationValueWriter writer,
        AdminAuditLog auditLog,
        ILogger<ConfigurationEditController> logger)
    {
        this._dataSource = dataSource;
        this._contextProvider = contextProvider;
        this._registry = registry;
        this._serializer = serializer;
        this._writer = writer;
        this._auditLog = auditLog;
        this._logger = logger;
    }

    /// <summary>
    /// Changes an object. Only the sent values are changed.
    /// </summary>
    /// <param name="type">The name of the type.</param>
    /// <param name="id">The id of the object.</param>
    /// <param name="request">The values.</param>
    /// <returns>The changed object.</returns>
    [HttpPut("{type}/{id:guid}")]
    public async Task<ActionResult<JsonObject>> UpdateAsync(string type, Guid id, [FromBody] ValuesRequest request)
    {
        if (this._registry.GetType(type) is not { } clrType)
        {
            return this.NotFound();
        }

        if (this._dataSource.IsSupporting(clrType))
        {
            return await this.UpdateInDataSourceAsync(clrType, id, request.Values).ConfigureAwait(false);
        }

        using var context = await this.CreateContextAsync(clrType).ConfigureAwait(false);
        var obj = await context.GetByIdAsync(id, clrType, this.HttpContext.RequestAborted).ConfigureAwait(false);
        if (obj is null)
        {
            return this.NotFound();
        }

        var before = this._serializer.SerializeObject(obj, clrType);
        if (await this.ApplyAndSaveAsync(obj, clrType, request.Values, context).ConfigureAwait(false) is { } error)
        {
            return error;
        }

        var after = this._serializer.SerializeObject(obj, clrType);
        await this.AuditAsync(AuditAction.Updated, clrType, obj, AuditChanges.Between(this._registry.GetSchema(clrType), before, after)).ConfigureAwait(false);
        return after;
    }

    /// <summary>
    /// Creates an object of a type which has its own list.
    /// </summary>
    /// <param name="type">The name of the type.</param>
    /// <param name="request">The values.</param>
    /// <returns>The created object.</returns>
    [HttpPost("{type}")]
    public async Task<ActionResult<JsonObject>> CreateAsync(string type, [FromBody] ValuesRequest request)
    {
        if (this._registry.GetType(type) is not { } clrType)
        {
            return this.NotFound();
        }

        if (!this._registry.GetSchema(clrType).CanCreate)
        {
            return this.BadRequest(new ErrorResponse("No se pueden crear objetos de este tipo."));
        }

        using var context = await this.CreateContextAsync(clrType).ConfigureAwait(false);
        var obj = context.CreateNew(clrType);
        var before = new JsonObject { ["values"] = new JsonObject() };
        if (await this.ApplyAndSaveAsync(obj, clrType, request.Values, context).ConfigureAwait(false) is { } error)
        {
            return error;
        }

        var after = this._serializer.SerializeObject(obj, clrType);
        await this.AuditAsync(AuditAction.Created, clrType, obj, AuditChanges.Between(this._registry.GetSchema(clrType), before, after)).ConfigureAwait(false);
        return this.StatusCode(StatusCodes.Status201Created, after);
    }

    /// <summary>
    /// Deletes an object of a type which has its own list.
    /// </summary>
    /// <param name="type">The name of the type.</param>
    /// <param name="id">The id of the object.</param>
    /// <returns>The result.</returns>
    [HttpDelete("{type}/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(string type, Guid id)
    {
        if (this._registry.GetType(type) is not { } clrType)
        {
            return this.NotFound();
        }

        if (!this._registry.GetSchema(clrType).CanCreate)
        {
            return this.BadRequest(new ErrorResponse("No se pueden borrar objetos de este tipo."));
        }

        using var context = await this.CreateContextAsync(clrType, false).ConfigureAwait(false);
        var obj = await context.GetByIdAsync(id, clrType, this.HttpContext.RequestAborted).ConfigureAwait(false);
        if (obj is null)
        {
            return this.NotFound();
        }

        var name = obj.GetName();
        try
        {
            await context.DeleteAsync(obj).ConfigureAwait(false);
            await context.SaveChangesAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "{Type} {Id} couldn't be deleted, probably because it's used by another object.", clrType.Name, id);
            return this.Conflict(new ErrorResponse($"No se pudo borrar \"{name}\": probablemente otro objeto lo usa."));
        }

        await this.ReloadCachedConfigurationAsync().ConfigureAwait(false);
        await this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            AuditAction.Deleted,
            this._registry.GetName(clrType),
            this._registry.GetCaption(clrType),
            id,
            name,
            [])).ConfigureAwait(false);
        return this.NoContent();
    }

    /// <summary>
    /// Changes an object of the game configuration on the context of the data source, like the edit pages
    /// of the admin panel do. That context has the whole configuration loaded - a typed context doesn't
    /// load the owned objects of every owned object (e.g. the items of the store of a merchant), so they
    /// would be taken as new. When a value is invalid or saving fails, the changes are discarded.
    /// </summary>
    private async Task<ActionResult<JsonObject>> UpdateInDataSourceAsync(Type type, Guid id, JsonObject? values)
    {
        await DataSourceLock.WaitAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        try
        {
            await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
            var context = await this._dataSource.GetContextAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
            object? obj = type == typeof(GameConfiguration)
                ? await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false)
                : this._dataSource.Get(id);
            if (obj is null || !type.IsInstanceOfType(obj) || obj.GetId() != id)
            {
                return this.NotFound();
            }

            var before = this._serializer.SerializeObject(obj, type);
            try
            {
                await this._writer.ApplyAsync(obj, type, values ?? [], context, this.HttpContext.RequestAborted).ConfigureAwait(false);
            }
            catch (ConfigurationValidationException ex)
            {
                await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
                return this.BadRequest(new ErrorResponse("Hay valores inválidos.", ex.Errors));
            }

            try
            {
                await context.SaveChangesAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "{Type} {Id} couldn't be saved.", type.Name, id);
                await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
                return this.Conflict(new ErrorResponse($"No se pudo guardar: {(ex.InnerException ?? ex).Message}"));
            }

            var after = this._serializer.SerializeObject(obj, type);
            await this.AuditAsync(AuditAction.Updated, type, obj, AuditChanges.Between(this._registry.GetSchema(type), before, after)).ConfigureAwait(false);
            return after;
        }
        finally
        {
            DataSourceLock.Release();
        }
    }

    private async Task<IContext> CreateContextAsync(Type type, bool useCache = true)
    {
        var gameConfiguration = await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        return this._contextProvider.CreateNewTypedContext(type, useCache, gameConfiguration);
    }

    /// <summary>
    /// Applies the values and saves them. Returns the response when it failed.
    /// </summary>
    private async Task<ActionResult?> ApplyAndSaveAsync(object obj, Type type, JsonObject? values, IContext context)
    {
        try
        {
            await this._writer.ApplyAsync(obj, type, values ?? [], context, this.HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (ConfigurationValidationException ex)
        {
            return this.BadRequest(new ErrorResponse("Hay valores inválidos.", ex.Errors));
        }

        try
        {
            await context.SaveChangesAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "{Type} {Id} couldn't be saved.", type.Name, obj.GetId());
            return this.Conflict(new ErrorResponse($"No se pudo guardar: {(ex.InnerException ?? ex).Message}"));
        }

        await this.ReloadCachedConfigurationAsync().ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Discards the cached configuration of the admin panel, which doesn't contain the change, and
    /// starts to load it again right away, so that the next request doesn't have to wait for it.
    /// </summary>
    private async Task ReloadCachedConfigurationAsync()
    {
        await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
        _ = Task.Run(async () =>
        {
            try
            {
                await this._dataSource.GetOwnerAsync(default, default).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "The configuration couldn't be loaded again after a change.");
            }
        });
    }

    private Task AuditAsync(AuditAction action, Type type, object obj, IReadOnlyList<AuditChange> changes)
    {
        if (action == AuditAction.Updated && changes.Count == 0)
        {
            return Task.CompletedTask;
        }

        return this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            action,
            this._registry.GetName(type),
            this._registry.GetCaption(type),
            obj.GetId(),
            obj.GetName(),
            changes));
    }

    /// <summary>
    /// The values of an object to create or change.
    /// </summary>
    /// <param name="Values">The values, by the names of the properties; in the same format as they are read.</param>
    public record ValuesRequest(JsonObject? Values);

    /// <summary>
    /// The response when a change failed.
    /// </summary>
    /// <param name="Message">The message, in Spanish.</param>
    /// <param name="Errors">The invalid values, if any.</param>
    public record ErrorResponse(string Message, IReadOnlyList<FieldError>? Errors = null);
}
