// <copyright file="PlugInsController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Administration;

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// Lists the plugins and chat commands, activates them and changes their custom configuration, like
/// the plugins and chat commands pages of the admin panel (see <see cref="PlugInController"/>).
/// </summary>
/// <remarks>
/// The custom configuration of a plugin is a plain object which is stored as JSON. It's described
/// and edited like the configuration objects (see <see cref="ConfigurationTypeRegistry"/>); references
/// to the configuration, e.g. to item definitions, are resolved by the data source.
/// </remarks>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/plugins")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Administrator)]
public class PlugInsController : ControllerBase
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es");

    private static readonly Lazy<IReadOnlyDictionary<Guid, Type>> PlugInTypes = new(FindPlugInTypes);

    private readonly IDataSource<GameConfiguration> _dataSource;
    private readonly IPersistenceContextProvider _contextProvider;
    private readonly ConfigurationTypeRegistry _registry;
    private readonly ConfigurationValueSerializer _serializer;
    private readonly ConfigurationValueWriter _writer;
    private readonly AdminAuditLog _auditLog;
    private readonly ILogger<PlugInsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlugInsController"/> class.
    /// </summary>
    /// <param name="dataSource">The data source of the game configuration.</param>
    /// <param name="contextProvider">The persistence context provider.</param>
    /// <param name="registry">The type registry.</param>
    /// <param name="serializer">The serializer.</param>
    /// <param name="writer">The writer.</param>
    /// <param name="auditLog">The audit log.</param>
    /// <param name="logger">The logger.</param>
    public PlugInsController(
        IDataSource<GameConfiguration> dataSource,
        IPersistenceContextProvider contextProvider,
        ConfigurationTypeRegistry registry,
        ConfigurationValueSerializer serializer,
        ConfigurationValueWriter writer,
        AdminAuditLog auditLog,
        ILogger<PlugInsController> logger)
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
    /// Gets all plugins of the game configuration.
    /// </summary>
    /// <returns>The plugins.</returns>
    [HttpGet]
    public async Task<IEnumerable<PlugInInfo>> GetPlugInsAsync()
    {
        var configuration = await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        return configuration.PlugInConfigurations
            .Select(c => PlugInTypes.Value.TryGetValue(c.TypeId, out var type) ? this.ToInfo(c, type) : null)
            .OfType<PlugInInfo>()
            .OrderBy(p => p.PointName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Gets a plugin with its custom configuration.
    /// </summary>
    /// <param name="id">The id of the plugin configuration.</param>
    /// <returns>The plugin.</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlugInDetails>> GetPlugInAsync(Guid id)
    {
        // A fresh context, because a plugin may change its own configuration on the game server.
        using var context = this._contextProvider.CreateNewTypedContext(typeof(PlugInConfiguration), false);
        if (await context.GetByIdAsync<PlugInConfiguration>(id, this.HttpContext.RequestAborted).ConfigureAwait(false) is not { } plugIn
            || !PlugInTypes.Value.TryGetValue(plugIn.TypeId, out var plugInType))
        {
            return this.NotFound();
        }

        var info = this.ToInfo(plugIn, plugInType);
        if (plugInType.GetCustomConfigurationType() is not { } configurationType)
        {
            return new PlugInDetails(info, null);
        }

        await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        var configuration = this.ReadConfiguration(plugIn, plugInType, configurationType);
        return new PlugInDetails(info, configuration is null ? null : this._serializer.SerializeObject(configuration, configurationType));
    }

    /// <summary>
    /// Activates or deactivates a plugin. The game servers apply it right away.
    /// </summary>
    /// <param name="id">The id of the plugin configuration.</param>
    /// <param name="request">Whether the plugin is active.</param>
    /// <returns>The plugin.</returns>
    [HttpPost("{id:guid}/active")]
    public async Task<ActionResult<PlugInInfo>> SetActiveAsync(Guid id, [FromBody] ActiveRequest request)
    {
        return await this.ChangeAsync(id, (plugIn, _) =>
        {
            plugIn.IsActive = request.IsActive;
            return Task.FromResult<IReadOnlyList<AuditChange>>([new AuditChange("IsActive", "Estado", null, request.IsActive ? "Activo" : "Inactivo")]);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the custom configuration of a plugin. Only the sent values are changed.
    /// </summary>
    /// <param name="id">The id of the plugin configuration.</param>
    /// <param name="request">The values, in the format of the configuration objects.</param>
    /// <returns>The plugin.</returns>
    [HttpPut("{id:guid}/configuration")]
    public async Task<ActionResult<PlugInInfo>> SetConfigurationAsync(Guid id, [FromBody] ConfigurationEditController.ValuesRequest request)
    {
        return await this.ChangeAsync(id, async (plugIn, plugInType) =>
        {
            var configurationType = plugInType.GetCustomConfigurationType()
                                    ?? throw new ConfigurationValidationException([new FieldError(string.Empty, "Este plugin no tiene configuración.")]);
            var configuration = this.ReadConfiguration(plugIn, plugInType, configurationType)
                                ?? throw new InvalidOperationException($"The configuration of {plugInType} couldn't be created.");
            var before = this._serializer.SerializeObject(configuration, configurationType);

            // References to the configuration are resolved by the context of the data source, like
            // the reference handler resolves them when the configuration is read.
            var dataSourceContext = await this._dataSource.GetContextAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
            await this._writer.ApplyAsync(configuration, configurationType, request.Values ?? [], dataSourceContext, this.HttpContext.RequestAborted, plainObjects: true).ConfigureAwait(false);
            plugIn.SetConfiguration(configuration, new ByDataSourceReferenceHandler(this._dataSource));

            var after = this._serializer.SerializeObject(configuration, configurationType);
            return AuditChanges.Between(this._registry.GetSchema(configurationType), before, after);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the chat commands, which are plugins too.
    /// </summary>
    /// <returns>The chat commands.</returns>
    [HttpGet("chat-commands")]
    public async Task<IEnumerable<ChatCommand>> GetChatCommandsAsync()
    {
        var configuration = await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        return configuration.PlugInConfigurations
            .Select(c =>
            {
                if (!PlugInTypes.Value.TryGetValue(c.TypeId, out var type)
                    || !typeof(IChatCommandPlugIn).IsAssignableFrom(type)
                    || ChatCommandTypeExtensions.TryCreateChatCommandInfo(type, Spanish) is not { } info)
                {
                    return null;
                }

                return new ChatCommand(c.GetId(), info.Command, info.Name, info.Description, info.Usage, info.MinimumCharacterStatus.ToString(), c.IsActive, type.GetCustomConfigurationType() is not null);
            })
            .OfType<ChatCommand>()
            .OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyDictionary<Guid, Type> FindPlugInTypes()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.DefinedTypes.Where(type => type.GetCustomAttribute<PlugInAttribute>() is not null).Select(type => type.AsType());
                }
                catch (ReflectionTypeLoadException)
                {
                    return [];
                }
            })
            .DistinctBy(type => type.GUID)
            .ToDictionary(type => type.GUID);
    }

    /// <summary>
    /// Reads the custom configuration of a plugin, or creates a new one with its defaults.
    /// </summary>
    private object? ReadConfiguration(PlugInConfiguration plugIn, Type plugInType, Type configurationType)
    {
        try
        {
            if (plugIn.GetConfiguration(configurationType, new ByDataSourceReferenceHandler(this._dataSource)) is { } configuration)
            {
                return configuration;
            }
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "The configuration of {PlugIn} couldn't be read; the defaults are used.", plugInType.Name);
        }

        // Some plugins know better defaults than the empty object, see ISupportDefaultCustomConfiguration.
        try
        {
            if (typeof(ISupportDefaultCustomConfiguration).IsAssignableFrom(plugInType)
                && Activator.CreateInstance(plugInType) is ISupportDefaultCustomConfiguration withDefaults)
            {
                return withDefaults.CreateDefaultConfig();
            }
        }
        catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException)
        {
            this._logger.LogDebug(ex, "The defaults of {PlugIn} couldn't be created.", plugInType.Name);
        }

        return Activator.CreateInstance(configurationType);
    }

    private async Task<ActionResult<PlugInInfo>> ChangeAsync(Guid id, Func<PlugInConfiguration, Type, Task<IReadOnlyList<AuditChange>>> change)
    {
        var gameConfiguration = await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        using var context = this._contextProvider.CreateNewTypedContext(typeof(PlugInConfiguration), true, gameConfiguration);
        if (await context.GetByIdAsync<PlugInConfiguration>(id, this.HttpContext.RequestAborted).ConfigureAwait(false) is not { } plugIn
            || !PlugInTypes.Value.TryGetValue(plugIn.TypeId, out var plugInType))
        {
            return this.NotFound();
        }

        IReadOnlyList<AuditChange> changes;
        try
        {
            changes = await change(plugIn, plugInType).ConfigureAwait(false);
        }
        catch (ConfigurationValidationException ex)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("Hay valores inválidos.", ex.Errors));
        }

        try
        {
            await context.SaveChangesAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "The plugin configuration {Id} couldn't be saved.", id);
            return this.Conflict(new ConfigurationEditController.ErrorResponse($"No se pudo guardar: {(ex.InnerException ?? ex).Message}"));
        }

        await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
        var info = this.ToInfo(plugIn, plugInType);
        if (changes.Count > 0)
        {
            await this._auditLog.AddAsync(new AuditEntry(DateTimeOffset.UtcNow, this.User.Identity?.Name, AuditAction.Updated, "PlugIn", "Plugins", id, info.Name, changes)).ConfigureAwait(false);
        }

        return info;
    }

    private PlugInInfo ToInfo(PlugInConfiguration plugIn, Type plugInType)
    {
        var display = plugInType.GetCustomAttribute<DisplayAttribute>();
        var point = plugInType.GetInterfaces().Select(i => i.GetCustomAttribute<PlugInPointAttribute>()).FirstOrDefault(a => a is not null);
        var container = plugInType.GetInterfaces().Select(i => i.GetCustomAttribute<CustomPlugInContainerAttribute>()).FirstOrDefault(a => a is not null);
        var configurationType = plugInType.GetCustomConfigurationType();
        return new PlugInInfo(
            plugIn.GetId(),
            display?.GetName() ?? plugInType.Name,
            display?.GetDescription(),
            PlugInPointCaption.Get(point?.Name ?? container?.Name ?? "N/A"),
            plugIn.IsActive,
            plugInType.FullName ?? plugInType.Name,
            configurationType is null ? null : this._registry.GetName(configurationType),
            typeof(IChatCommandPlugIn).IsAssignableFrom(plugInType));
    }

    /// <summary>
    /// A plugin.
    /// </summary>
    /// <param name="Id">The id of the plugin configuration.</param>
    /// <param name="Name">The name.</param>
    /// <param name="Description">The description.</param>
    /// <param name="PointName">The name of the extension point, i.e. what the plugin extends.</param>
    /// <param name="IsActive">If set to <c>true</c>, the plugin is active.</param>
    /// <param name="TypeName">The full name of the type of the plugin.</param>
    /// <param name="ConfigurationType">The type of its custom configuration (see the schemas), if it has one.</param>
    /// <param name="IsChatCommand">If set to <c>true</c>, the plugin is a chat command.</param>
    public record PlugInInfo(Guid Id, string Name, string? Description, string PointName, bool IsActive, string TypeName, string? ConfigurationType, bool IsChatCommand);

    /// <summary>
    /// A plugin with its custom configuration.
    /// </summary>
    /// <param name="PlugIn">The plugin.</param>
    /// <param name="Configuration">The configuration, in the format of the configuration objects; <c>null</c> if it has none.</param>
    public record PlugInDetails(PlugInInfo PlugIn, JsonObject? Configuration);

    /// <summary>
    /// A chat command.
    /// </summary>
    /// <param name="PlugInId">The id of the plugin configuration of the command.</param>
    /// <param name="Command">The command, e.g. <c>/item</c>.</param>
    /// <param name="Name">The name.</param>
    /// <param name="Description">The description.</param>
    /// <param name="Usage">How the command is used.</param>
    /// <param name="MinimumStatus">The character status which is required, e.g. <c>GameMaster</c>.</param>
    /// <param name="IsActive">If set to <c>true</c>, the command is active.</param>
    /// <param name="HasConfiguration">If set to <c>true</c>, the command has a custom configuration.</param>
    public record ChatCommand(Guid PlugInId, string Command, string Name, string Description, string Usage, string MinimumStatus, bool IsActive, bool HasConfiguration);

    /// <summary>
    /// Whether a plugin is active.
    /// </summary>
    /// <param name="IsActive">If set to <c>true</c>, the plugin is active.</param>
    public record ActiveRequest(bool IsActive);
}
