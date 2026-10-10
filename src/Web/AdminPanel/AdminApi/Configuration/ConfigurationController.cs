// <copyright file="ConfigurationController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Describes and lists the game configuration. Read only; the objects come from the same cached
/// <see cref="IDataSource{GameConfiguration}"/> as the configuration pages of the admin panel.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/config")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class ConfigurationController : ControllerBase
{
    private const int MaximumResults = 200;

    private readonly IDataSource<GameConfiguration> _dataSource;
    private readonly IPersistenceContextProvider _contextProvider;
    private readonly ConfigurationTypeRegistry _registry;
    private readonly ConfigurationValueSerializer _serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationController"/> class.
    /// </summary>
    /// <param name="dataSource">The data source of the game configuration.</param>
    /// <param name="contextProvider">The persistence context provider, for the types which aren't part of the game configuration.</param>
    /// <param name="registry">The type registry.</param>
    /// <param name="serializer">The serializer.</param>
    public ConfigurationController(IDataSource<GameConfiguration> dataSource, IPersistenceContextProvider contextProvider, ConfigurationTypeRegistry registry, ConfigurationValueSerializer serializer)
    {
        this._dataSource = dataSource;
        this._contextProvider = contextProvider;
        this._registry = registry;
        this._serializer = serializer;
    }

    /// <summary>
    /// Gets the types which have their own list, grouped by topic.
    /// </summary>
    /// <returns>The groups.</returns>
    [HttpGet("types")]
    public async Task<IEnumerable<TypeGroupDto>> GetTypesAsync()
    {
        var groups = new List<TypeGroupDto>();
        foreach (var group in ConfigurationTypeGroups.All)
        {
            var types = new List<TypeInfoDto>();
            foreach (var entry in group.Entries)
            {
                types.Add(await this.CreateTypeInfoAsync(entry.Type).ConfigureAwait(false));
            }

            groups.Add(new TypeGroupDto(group.Caption, types));
        }

        var grouped = ConfigurationTypeGroups.All.SelectMany(g => g.Entries).Select(e => e.Type).ToHashSet();
        var others = new List<TypeInfoDto>();
        foreach (var type in this._registry.BrowsableTypes.Where(type => !grouped.Contains(type)))
        {
            others.Add(await this.CreateTypeInfoAsync(type).ConfigureAwait(false));
        }

        groups.Add(new TypeGroupDto(
            ConfigurationTypeGroups.OtherGroupCaption,
            others.OrderBy(info => info.Caption, StringComparer.CurrentCultureIgnoreCase).ToList()));
        return groups;
    }

    /// <summary>
    /// Gets the schema of a type.
    /// </summary>
    /// <param name="type">The name of the type.</param>
    /// <returns>The schema.</returns>
    [HttpGet("schema/{type}")]
    public ActionResult<TypeSchema> GetSchema(string type)
    {
        return this._registry.GetType(type) is { } clrType
            ? this._registry.GetSchema(clrType)
            : this.NotFound();
    }

    /// <summary>
    /// Gets all objects of a type which has its own list, with the values of its list columns.
    /// </summary>
    /// <param name="type">The name of the type.</param>
    /// <returns>The objects.</returns>
    [HttpGet("{type}")]
    public async Task<ActionResult<JsonArray>> GetListAsync(string type)
    {
        if (this._registry.GetType(type) is not { } clrType || !this._registry.IsBrowsable(clrType))
        {
            return this.NotFound();
        }

        var rows = (await this.GetAllAsync(clrType).ConfigureAwait(false)).Select(obj => (JsonNode)this._serializer.SerializeRow(obj, clrType)).ToArray();
        return new JsonArray(rows);
    }

    /// <summary>
    /// Gets an object with all of its values.
    /// </summary>
    /// <param name="type">The name of the type.</param>
    /// <param name="id">The id of the object.</param>
    /// <returns>The object.</returns>
    [HttpGet("{type}/{id:guid}")]
    public async Task<ActionResult<JsonObject>> GetObjectAsync(string type, Guid id)
    {
        if (this._registry.GetType(type) is not { } clrType)
        {
            return this.NotFound();
        }

        object? obj;
        if (this._registry.IsBrowsable(clrType))
        {
            obj = (await this.GetAllAsync(clrType).ConfigureAwait(false)).FirstOrDefault(o => o.GetId() == id);
        }
        else
        {
            await this.LoadAsync().ConfigureAwait(false);
            obj = this._dataSource.Get(id);
        }

        if (obj is null || !clrType.IsInstanceOfType(obj))
        {
            return this.NotFound();
        }

        return this._serializer.SerializeObject(obj, clrType);
    }

    /// <summary>
    /// Gets the objects of a type which can be selected for a reference, filtered by their name.
    /// </summary>
    /// <param name="type">The name of the type.</param>
    /// <param name="q">The text which the name has to contain.</param>
    /// <param name="limit">The maximum number of results.</param>
    /// <returns>The matching objects, as references.</returns>
    [HttpGet("lookup/{type}")]
    public async Task<ActionResult<JsonArray>> LookupAsync(string type, [FromQuery] string? q = null, [FromQuery] int limit = 50)
    {
        if (this._registry.GetType(type) is not { } clrType || (!this._registry.IsBrowsable(clrType) && !this._dataSource.IsSupporting(clrType)))
        {
            return this.NotFound();
        }

        var results = (await this.GetAllAsync(clrType).ConfigureAwait(false))
            .Where(obj => string.IsNullOrWhiteSpace(q) || obj.GetName().Contains(q.Trim(), StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(obj => obj.GetName(), StringComparer.CurrentCultureIgnoreCase)
            .Take(Math.Clamp(limit, 1, MaximumResults))
            .Select(obj => (JsonNode)this._serializer.SerializeReference(obj, clrType))
            .ToArray();
        return new JsonArray(results);
    }

    /// <summary>
    /// Searches the objects of all types which have their own list by their name.
    /// </summary>
    /// <param name="q">The text which the name has to contain.</param>
    /// <param name="limit">The maximum number of results.</param>
    /// <returns>The matching objects, as references.</returns>
    [HttpGet("search")]
    public async Task<ActionResult<JsonArray>> SearchAsync([FromQuery] string? q = null, [FromQuery] int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return new JsonArray();
        }

        var term = q.Trim();
        var candidates = new List<(Type Type, object Object, string Name)>();
        foreach (var type in this._registry.BrowsableTypes.Where(type => type != typeof(GameConfiguration)))
        {
            candidates.AddRange((await this.GetAllAsync(type).ConfigureAwait(false)).Select(obj => (type, obj, obj.GetName())));
        }

        var results = candidates
            .Where(r => r.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(r => r.Name.StartsWith(term, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1)
            .ThenBy(r => r.Name.Length)
            .Take(Math.Clamp(limit, 1, MaximumResults))
            .Select(r =>
            {
                var reference = this._serializer.SerializeReference(r.Object, r.Type);
                reference["typeCaption"] = this._registry.GetCaption(r.Type);
                return (JsonNode)reference;
            })
            .ToArray();
        return new JsonArray(results);
    }

    private async Task<TypeInfoDto> CreateTypeInfoAsync(Type type)
    {
        var schema = this._registry.GetSchema(type);
        var count = (await this.GetAllAsync(type).ConfigureAwait(false)).Count;
        return new TypeInfoDto(schema.Name, schema.Caption, schema.Description, type == typeof(GameConfiguration), count);
    }

    private async Task LoadAsync()
    {
        await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a copy of the objects of the type, because the cached configuration may be changed
    /// by the admin panel at the same time. Types which aren't part of the game configuration
    /// are loaded from the database, like the configuration grid of the admin panel does.
    /// </summary>
    private async Task<List<object>> GetAllAsync(Type type)
    {
        var gameConfiguration = await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        if (this._dataSource.IsSupporting(type))
        {
            return this._dataSource.GetAll(type).OfType<object>().ToList();
        }

        using var context = this._contextProvider.CreateNewTypedContext(type, true, gameConfiguration);
        var objects = await context.GetAsync(type, this.HttpContext.RequestAborted).ConfigureAwait(false);
        return objects.OfType<object>().ToList();
    }

    /// <summary>
    /// A group of configuration types.
    /// </summary>
    /// <param name="Caption">The caption.</param>
    /// <param name="Types">The types.</param>
    public record TypeGroupDto(string Caption, IReadOnlyList<TypeInfoDto> Types);

    /// <summary>
    /// A configuration type which has its own list.
    /// </summary>
    /// <param name="Name">The name of the type.</param>
    /// <param name="Caption">The caption.</param>
    /// <param name="Description">The description.</param>
    /// <param name="IsSingleton">If set to <c>true</c>, there is only one object of the type.</param>
    /// <param name="Count">The number of objects.</param>
    public record TypeInfoDto(string Name, string Caption, string? Description, bool IsSingleton, int Count);
}
