// <copyright file="ConfigurationValueSerializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Serializes configuration objects for the admin frontend, following their <see cref="TypeSchema"/>.
/// </summary>
/// <remarks>
/// An object is serialized as <c>{ id, name, values: { property: value } }</c>.
/// References are serialized as <c>{ id, name, type }</c>, so they can be shown and linked
/// without loading the referenced object. Embedded objects are serialized completely, except
/// when their type has its own list - then a single one is serialized like a reference, and a
/// list only as <c>{ count }</c>.
/// </remarks>
public class ConfigurationValueSerializer
{
    private const int MaximumDepth = 8;

    private readonly ConfigurationTypeRegistry _registry;
    private readonly ILogger<ConfigurationValueSerializer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationValueSerializer"/> class.
    /// </summary>
    /// <param name="registry">The type registry.</param>
    /// <param name="logger">The logger.</param>
    public ConfigurationValueSerializer(ConfigurationTypeRegistry registry, ILogger<ConfigurationValueSerializer> logger)
    {
        this._registry = registry;
        this._logger = logger;
    }

    /// <summary>
    /// Serializes the object with all of its properties.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <param name="type">The type of the object, as described by the schema.</param>
    /// <returns>The serialized object.</returns>
    public JsonObject SerializeObject(object obj, Type type) => this.SerializeObject(obj, type, 0, null);

    /// <summary>
    /// Serializes the object as a row of a list, with only the specified columns.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <param name="type">The type of the object, as described by the schema.</param>
    /// <returns>The serialized object.</returns>
    public JsonObject SerializeRow(object obj, Type type)
    {
        var schema = this._registry.GetSchema(type);
        return this.SerializeObject(obj, type, MaximumDepth, schema.ListColumns);
    }

    /// <summary>
    /// Serializes a reference to the object.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <param name="type">The declared type of the object.</param>
    /// <returns>The serialized reference.</returns>
    public JsonObject SerializeReference(object obj, Type type) => new()
    {
        ["id"] = obj.GetId().ToString(),
        ["name"] = obj.GetName(),
        ["type"] = this._registry.GetName(type),
    };

    private static JsonNode? SerializeSimpleValue(object? value)
    {
        return value switch
        {
            null => null,
            Enum enumValue => enumValue.ToString(),
            LocalizedString localizedString => localizedString.ValueInNeutralLanguage,
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            byte[] bytes => Convert.ToHexString(bytes),
            float.NaN or double.NaN => null,
            _ => JsonSerializer.SerializeToNode(value),
        };
    }

    private static JsonArray SerializeFlags(Enum value)
    {
        var flags = Enum.GetValues(value.GetType()).Cast<Enum>()
            .Where(flag => Convert.ToInt64(flag, CultureInfo.InvariantCulture) != 0 && value.HasFlag(flag))
            .Select(flag => (JsonNode)flag.ToString())
            .ToArray();
        return new JsonArray(flags);
    }

    private JsonObject SerializeObject(object obj, Type type, int depth, IReadOnlyList<string>? onlyProperties)
    {
        var schema = this._registry.GetSchema(type);
        var values = new JsonObject();
        foreach (var property in schema.Properties)
        {
            if (onlyProperties is not null && !onlyProperties.Contains(property.Name))
            {
                continue;
            }

            try
            {
                values[property.Name] = this.SerializeValue(property.Property.GetValue(obj), property, depth);
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "The property {Type}.{Property} couldn't be serialized.", type.Name, property.Name);
                values[property.Name] = null;
            }
        }

        return new JsonObject
        {
            ["id"] = obj.GetId().ToString(),
            ["name"] = obj.GetName(),
            ["values"] = values,
        };
    }

    private JsonNode? SerializeValue(object? value, PropertySchema property, int depth)
    {
        if (value is null)
        {
            return null;
        }

        switch (property.Kind)
        {
            case PropertyKind.Reference:
                return this.SerializeReference(value, property.TargetClrType!);
            case PropertyKind.ReferenceList:
                return new JsonArray(((IEnumerable)value).OfType<object>()
                    .Select(item => (JsonNode)this.SerializeReference(item, property.TargetClrType!))
                    .ToArray());
            case PropertyKind.Embedded:
                return property.TargetIsBrowsable || depth >= MaximumDepth
                    ? this.SerializeReference(value, property.TargetClrType!)
                    : this.SerializeObject(value, property.TargetClrType!, depth + 1, null);
            case PropertyKind.EmbeddedList when property.TargetIsBrowsable:
                // The objects have their own list (e.g. the items of the game configuration), so only the number is relevant here.
                return new JsonObject { ["count"] = ((IEnumerable)value).OfType<object>().Count() };
            case PropertyKind.EmbeddedList:
                return new JsonArray(((IEnumerable)value).OfType<object>()
                    .Select(item => (JsonNode)(depth >= MaximumDepth
                        ? this.SerializeReference(item, property.TargetClrType!)
                        : this.SerializeObject(item, property.TargetClrType!, depth + 1, null)))
                    .ToArray());
            case PropertyKind.ValueList:
                return new JsonArray(((IEnumerable)value).OfType<object>().Select(SerializeSimpleValue).ToArray());
            case PropertyKind.Flags:
                return SerializeFlags((Enum)value);
            case PropertyKind.Unknown:
                return null;
            default:
                return SerializeSimpleValue(value);
        }
    }
}
