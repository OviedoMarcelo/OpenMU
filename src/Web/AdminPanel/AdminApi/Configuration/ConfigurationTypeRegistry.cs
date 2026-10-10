// <copyright file="ConfigurationTypeRegistry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Composition;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Web.Shared;

/// <summary>
/// Knows the types of the game configuration and describes them for the admin frontend.
/// </summary>
/// <remarks>
/// The types are found by walking the properties of <see cref="GameConfiguration"/>, so every type
/// which can appear in the configuration can be described. The kinds of the properties follow the
/// same rules as the generated forms of the admin panel (see the component builders of Web.Shared).
/// </remarks>
public class ConfigurationTypeRegistry
{
    private static readonly HashSet<Type> IntegerTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
    ];

    private static readonly HashSet<Type> DecimalTypes = [typeof(float), typeof(double), typeof(decimal)];

    /// <summary>
    /// The types which are left out of the generic configuration pages, because they have their
    /// own page. Determining the names of plugin configurations is also expensive (the plugin type
    /// is resolved by reflection), which made every page with them slow.
    /// </summary>
    private static readonly HashSet<Type> ExcludedTypes = [typeof(PlugInConfiguration)];

    private readonly Dictionary<string, Type> _typesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Type> _browsableTypes = [];
    private readonly ConcurrentDictionary<Type, TypeSchema> _schemas = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationTypeRegistry"/> class.
    /// </summary>
    public ConfigurationTypeRegistry()
    {
        this._browsableTypes.Add(typeof(GameConfiguration));
        foreach (var property in ConfigurationPropertyFilter.GetVisibleProperties(typeof(GameConfiguration)))
        {
            // Only the collections which the data source can enumerate.
            if (GetCollectionElementType(property.PropertyType) is { } elementType
                && GameConfigurationHelper.Enumerables.ContainsKey(elementType)
                && !ExcludedTypes.Contains(elementType))
            {
                this._browsableTypes.Add(elementType);
            }
        }

        // Some types of the menu aren't part of the game configuration, e.g. the game clients.
        foreach (var entry in ConfigurationTypeGroups.All.SelectMany(g => g.Entries))
        {
            this._browsableTypes.Add(entry.Type);
            this.Register(entry.Type);
        }

        this.Register(typeof(GameConfiguration));
        foreach (var type in GameConfigurationHelper.Enumerables.Keys)
        {
            this.Register(type);
        }
    }

    /// <summary>
    /// Gets the types which have their own list, i.e. the collections of the game configuration.
    /// </summary>
    public IReadOnlyCollection<Type> BrowsableTypes => this._browsableTypes;

    /// <summary>
    /// Gets the type with the specified name.
    /// </summary>
    /// <param name="name">The name of the type.</param>
    /// <returns>The type, or <c>null</c> if it's unknown.</returns>
    public Type? GetType(string name) => this._typesByName.GetValueOrDefault(name);

    /// <summary>
    /// Determines whether the objects of the type have their own list.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><c>true</c>, if the objects of the type have their own list.</returns>
    public bool IsBrowsable(Type type) => this._browsableTypes.Contains(type);

    /// <summary>
    /// Gets the name of the type in the API.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The name.</returns>
    public string GetName(Type type) => this._typesByName.GetValueOrDefault(type.Name) == type ? type.Name : type.FullName ?? type.Name;

    /// <summary>
    /// Gets the schema of the type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The schema.</returns>
    public TypeSchema GetSchema(Type type) => this._schemas.GetOrAdd(type, this.CreateSchema);

    /// <summary>
    /// Gets the caption of the type, preferring the Spanish caption of the admin panel menu.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The caption.</returns>
    public string GetCaption(Type type) =>
        ConfigurationTypeGroups.FindEntry(type)?.Caption
        ?? (type.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? type.GetTypeCaption());

    private static Type? GetCollectionElementType(Type type)
    {
        if (type == typeof(string) || type.IsArray || !type.IsGenericType)
        {
            return null;
        }

        var definition = type.GetGenericTypeDefinition();
        if (definition == typeof(ICollection<>) || definition == typeof(IList<>) || definition == typeof(List<>)
            || definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyCollection<>) || definition == typeof(IReadOnlyList<>))
        {
            return type.GenericTypeArguments[0];
        }

        return null;
    }

    private static bool IsDataModelType(Type type) =>
        type.IsClass && type != typeof(string) && (type.Namespace?.StartsWith("MUnique.OpenMU", StringComparison.Ordinal) ?? false);

    private static double? ToDouble(object? value) => value is null ? null : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);

    private static PropertyKind GetScalarKind(Type type)
    {
        if (type == typeof(string))
        {
            return PropertyKind.Text;
        }

        if (type == typeof(LocalizedString))
        {
            return PropertyKind.LocalizedText;
        }

        if (type == typeof(bool))
        {
            return PropertyKind.Boolean;
        }

        if (type.IsEnum)
        {
            return type.GetCustomAttribute<FlagsAttribute>() is null ? PropertyKind.Enum : PropertyKind.Flags;
        }

        if (IntegerTypes.Contains(type))
        {
            return PropertyKind.Integer;
        }

        if (DecimalTypes.Contains(type))
        {
            return PropertyKind.Decimal;
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            return PropertyKind.DateTime;
        }

        if (type == typeof(DateOnly))
        {
            return PropertyKind.Date;
        }

        if (type == typeof(TimeOnly))
        {
            return PropertyKind.Time;
        }

        if (type == typeof(TimeSpan))
        {
            return PropertyKind.TimeSpan;
        }

        if (type == typeof(byte[]))
        {
            return PropertyKind.Bytes;
        }

        return PropertyKind.Unknown;
    }

    private static IReadOnlyList<EnumValue> GetEnumValues(Type enumType)
    {
        return enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => new EnumValue(
                field.Name,
                Convert.ToInt64(field.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture),
                field.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? CaptionHelper.SeparateWords(field.Name)))
            .ToList();
    }

    /// <summary>
    /// Determines whether a property can't be changed: the id, values without a public setter, values
    /// which the API doesn't understand, and the collections whose objects have their own list (e.g. the
    /// items of the game configuration), because these are changed on their own pages.
    /// </summary>
    private static bool IsReadOnly(PropertyInfo property, PropertyKind kind, bool targetIsBrowsable)
    {
        return kind switch
        {
            PropertyKind.Unknown => true,
            PropertyKind.Embedded or PropertyKind.EmbeddedList when targetIsBrowsable => true,
            PropertyKind.EmbeddedList or PropertyKind.ReferenceList or PropertyKind.ValueList => false,
            _ => property.Name == "Id" || property.SetMethod is not { IsPublic: true },
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void Register(Type type)
    {
        var pending = new Queue<Type>();
        pending.Enqueue(type);
        while (pending.TryDequeue(out var current))
        {
            if (!this._typesByName.TryAdd(current.Name, current))
            {
                // Either already registered, or another type has the same name - then it's only reachable by its full name.
                if (this._typesByName[current.Name] == current || !this._typesByName.TryAdd(current.FullName ?? current.Name, current))
                {
                    continue;
                }
            }

            foreach (var property in ConfigurationPropertyFilter.GetVisibleProperties(current))
            {
                var target = GetCollectionElementType(property.PropertyType) ?? property.PropertyType;
                if (IsDataModelType(target) && !ExcludedTypes.Contains(target) && !this._typesByName.ContainsKey(target.Name))
                {
                    pending.Enqueue(target);
                }
            }
        }
    }

    private TypeSchema CreateSchema(Type type)
    {
        var properties = ConfigurationPropertyFilter.GetVisibleProperties(type)
            .Select((property, index) => (Property: this.CreatePropertySchema(type, property), Index: index))
            .Where(p => p.Property.TargetClrType is null || !ExcludedTypes.Contains(p.Property.TargetClrType))
            .OrderBy(p => p.Property.Order ?? int.MaxValue)
            .ThenBy(p => p.Index)
            .Select(p => p.Property)
            .ToList();

        var nameProperty = properties.FirstOrDefault(p => p.Name == "Name")
                           ?? properties.FirstOrDefault(p => p.Name == "Caption")
                           ?? properties.FirstOrDefault(p => p.Name == "Designation")
                           ?? properties.FirstOrDefault(p => p.Name == "Description");
        if (nameProperty is not null && nameProperty.Kind is not (PropertyKind.Text or PropertyKind.LocalizedText))
        {
            nameProperty = null;
        }

        var entry = ConfigurationTypeGroups.FindEntry(type);
        var listColumns = entry?.ListColumns.Where(c => properties.Any(p => p.Name == c)).ToList()
                          ?? properties
                              .Where(p => p != nameProperty && p.Name != "Id")
                              .Where(p => p.Kind is PropertyKind.Text or PropertyKind.LocalizedText or PropertyKind.Integer or PropertyKind.Decimal
                                  or PropertyKind.Boolean or PropertyKind.Enum or PropertyKind.Reference)
                              .Take(5)
                              .Select(p => p.Name)
                              .ToList();

        return new TypeSchema(
            this.GetName(type),
            this.GetCaption(type),
            entry?.Description ?? type.GetCustomAttribute<DisplayAttribute>()?.GetDescription(),
            this.IsBrowsable(type),
            nameProperty?.Name,
            listColumns,
            properties,
            this.IsBrowsable(type) && type != typeof(GameConfiguration));
    }

    private PropertySchema CreatePropertySchema(Type declaringType, PropertyInfo property)
    {
        var display = property.GetCustomAttribute<DisplayAttribute>(true);
        var caption = display?.GetName() ?? declaringType.GetPropertyCaption(property.Name);
        var description = display?.GetDescription() ?? NullIfEmpty(declaringType.GetPropertyDescription(property.Name));
        var help = SpanishHelp.TryGet(declaringType, property.Name, out var entry) ? new HelpText(entry.Info, entry.Impact) : null;
        var range = property.GetCustomAttribute<RangeAttribute>();
        var propertyType = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propertyType);
        var valueType = underlyingType ?? propertyType;
        var isMemberOfAggregate = property.GetCustomAttribute<MemberOfAggregateAttribute>() is not null;

        PropertyKind kind;
        Type? target = null;
        Type? enumType = null;
        if (GetCollectionElementType(propertyType) is { } elementType)
        {
            if (elementType.IsValueType || elementType == typeof(string))
            {
                kind = PropertyKind.ValueList;
                valueType = Nullable.GetUnderlyingType(elementType) ?? elementType;
                enumType = valueType.IsEnum ? valueType : null;
            }
            else
            {
                kind = isMemberOfAggregate ? PropertyKind.EmbeddedList : PropertyKind.ReferenceList;
                target = elementType;
            }
        }
        else
        {
            kind = GetScalarKind(valueType);
            if (kind is PropertyKind.Enum or PropertyKind.Flags)
            {
                enumType = valueType;
            }
            else if (kind == PropertyKind.Unknown && IsDataModelType(valueType))
            {
                kind = isMemberOfAggregate ? PropertyKind.Embedded : PropertyKind.Reference;
                target = valueType;
            }
        }

        return new PropertySchema(
            property.Name,
            caption,
            description,
            help,
            kind,
            display?.GetGroupName(),
            display?.GetOrder(),
            underlyingType is not null || (!propertyType.IsValueType && kind is not (PropertyKind.EmbeddedList or PropertyKind.ReferenceList or PropertyKind.ValueList)),
            kind is PropertyKind.Integer or PropertyKind.Decimal or PropertyKind.ValueList ? valueType.Name : null,
            ToDouble(range?.Minimum),
            ToDouble(range?.Maximum),
            enumType is null ? null : GetEnumValues(enumType),
            target is null ? null : this.GetName(target),
            target is not null && this.IsBrowsable(target),
            IsReadOnly(property, kind, target is not null && this.IsBrowsable(target)))
        {
            Property = property,
            TargetClrType = target,
        };
    }
}
