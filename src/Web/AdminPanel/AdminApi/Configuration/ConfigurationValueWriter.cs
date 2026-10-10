// <copyright file="ConfigurationValueWriter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Applies values which were sent to the admin API to configuration objects. It's the counterpart
/// of the <see cref="ConfigurationValueSerializer"/>: the values have the same format.
/// </summary>
/// <remarks>
/// Only the properties which are sent are changed. References are resolved with the context,
/// owned objects are changed in place (matched by their id in lists), created with the context
/// when they're new, and deleted when they're left out. All errors are collected, so that the
/// frontend can show them at once; then nothing must be saved.
/// </remarks>
public class ConfigurationValueWriter
{
    private static readonly CultureInfo NeutralCulture = CultureInfo.GetCultureInfo(LocalizedString.NeutralLanguageCode);

    private readonly ConfigurationTypeRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationValueWriter"/> class.
    /// </summary>
    /// <param name="registry">The type registry.</param>
    public ConfigurationValueWriter(ConfigurationTypeRegistry registry)
    {
        this._registry = registry;
    }

    /// <summary>
    /// Applies the values to the object.
    /// </summary>
    /// <param name="target">The object, which belongs to the <paramref name="context"/>.</param>
    /// <param name="type">The type of the object, as described by the schema.</param>
    /// <param name="values">The values, by the names of the properties.</param>
    /// <param name="context">The context which loaded the object; it resolves references and creates owned objects.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="plainObjects">
    /// If set to <c>true</c>, the object is a plain object (e.g. the configuration of a plugin) instead
    /// of an entity: its owned objects are created without the context and are not deleted, and the
    /// objects of its lists, which have no ids, are matched by their position.
    /// </param>
    /// <exception cref="ConfigurationValidationException">When values are invalid.</exception>
    public async Task ApplyAsync(object target, Type type, JsonObject values, IContext context, CancellationToken cancellationToken = default, bool plainObjects = false)
    {
        var state = new WriteState(context, cancellationToken, plainObjects);
        await this.ApplyObjectAsync(target, type, values, string.Empty, state).ConfigureAwait(false);
        if (state.Errors.Count > 0)
        {
            throw new ConfigurationValidationException(state.Errors);
        }
    }

    private static string Combine(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    private static Type GetElementType(PropertySchema property) => property.Property.PropertyType.GenericTypeArguments[0];

    private static CollectionAccessor GetCollection(object target, PropertySchema property)
    {
        var collection = property.Property.GetValue(target) ?? throw new InvalidValueException("La lista no está disponible.");
        return new CollectionAccessor(collection, GetElementType(property));
    }

    /// <summary>
    /// Deletes an owned object. The generic delete of the context has to know the actual type of it.
    /// </summary>
    private static async ValueTask DeleteAsync(IContext context, object obj)
    {
        var method = typeof(IContext).GetMethod(nameof(IContext.DeleteAsync))!.MakeGenericMethod(obj.GetType());
        await ((ValueTask<bool>)method.Invoke(context, [obj])!).ConfigureAwait(false);
    }

    private static object CreateOwned(WriteState state, Type type) =>
        state.PlainObjects
            ? Activator.CreateInstance(type) ?? throw new InvalidOperationException($"{type} can't be created.")
            : state.Context.CreateNew(type);

    private static ValueTask DeleteOwnedAsync(WriteState state, object obj) =>
        state.PlainObjects ? ValueTask.CompletedTask : DeleteAsync(state.Context, obj);

    private static Guid? ReadId(JsonNode? node)
    {
        var id = node is JsonObject obj ? obj["id"] : null;
        if (id is null)
        {
            return null;
        }

        return Guid.TryParse(id.GetValue<string>(), out var guid) && guid != Guid.Empty ? guid : null;
    }

    private static void Validate(object target, string path, WriteState state)
    {
        // A spawn without a monster would break the spawning on the game server.
        if (target is MonsterSpawnArea { MonsterDefinition: null })
        {
            state.Errors.Add(new FieldError(Combine(path, nameof(MonsterSpawnArea.MonsterDefinition)), "Elegí el monstruo del spawn."));
        }

        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(target, new ValidationContext(target), results, true))
        {
            return;
        }

        foreach (var result in results)
        {
            foreach (var member in result.MemberNames.DefaultIfEmpty(string.Empty))
            {
                var memberPath = member.Length == 0 ? path : Combine(path, member);

                // A range is already checked when the value is applied, with a Spanish message.
                if (state.Errors.All(e => e.Path != memberPath))
                {
                    state.Errors.Add(new FieldError(memberPath, SpanishMessage(target, member) ?? result.ErrorMessage ?? "Valor inválido."));
                }
            }
        }
    }

    /// <summary>
    /// Gets a Spanish message for the first validation attribute of the property which fails; the
    /// messages of the data annotations are English.
    /// </summary>
    private static string? SpanishMessage(object target, string member)
    {
        if (member.Length == 0 || target.GetType().GetProperty(member) is not { } property)
        {
            return null;
        }

        var value = property.GetValue(target);
        var failed = property.GetCustomAttributes<ValidationAttribute>(true).FirstOrDefault(a => !a.IsValid(value));
        return failed switch
        {
            RequiredAttribute => "Es obligatorio.",
            RangeAttribute range => $"Tiene que estar entre {range.Minimum} y {range.Maximum}.",
            StringLengthAttribute length when length.MinimumLength > 0 => $"Tiene que tener entre {length.MinimumLength} y {length.MaximumLength} caracteres.",
            StringLengthAttribute length => $"Tiene que tener como máximo {length.MaximumLength} caracteres.",
            MaxLengthAttribute maxLength => $"Tiene que tener como máximo {maxLength.Length}.",
            MinLengthAttribute minLength => $"Tiene que tener como mínimo {minLength.Length}.",
            RegularExpressionAttribute => "No tiene el formato correcto.",
            _ => null,
        };
    }

    private static JsonObject ReadValues(JsonNode? node, string path)
    {
        return node is JsonObject { } obj && obj["values"] is JsonObject values
            ? values
            : throw new InvalidValueException("Se esperaba un objeto con sus valores.", path);
    }

    /// <summary>
    /// Reads a number, no matter whether the node was parsed (then it holds a <see cref="JsonElement"/>) or created.
    /// </summary>
    private static decimal ReadNumber(JsonNode node)
    {
        return node.GetValueKind() == JsonValueKind.Number
            ? decimal.Parse(node.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture)
            : throw new InvalidValueException("Tiene que ser un número.");
    }

    private static object ConvertListValue(JsonNode? value, PropertySchema property, Type valueType)
    {
        if (value is null)
        {
            throw new InvalidValueException("La lista no puede tener valores vacíos.");
        }

        if (valueType.IsEnum)
        {
            return ParseEnum(valueType, value.GetValue<string>());
        }

        if (valueType == typeof(string))
        {
            return value.GetValue<string>();
        }

        var isInteger = valueType != typeof(float) && valueType != typeof(double) && valueType != typeof(decimal);
        return ConvertNumber(ReadNumber(value), property with { Minimum = null, Maximum = null }, valueType, isInteger);
    }

    private static object? ConvertScalar(JsonNode? node, PropertySchema property, object? currentValue)
    {
        var propertyType = property.Property.PropertyType;
        var valueType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (node is null)
        {
            return property.IsNullable ? null : throw new InvalidValueException("Es obligatorio.");
        }

        switch (property.Kind)
        {
            case PropertyKind.Text:
                return node.GetValue<string>();
            case PropertyKind.LocalizedText:
                return ConvertLocalizedText(node.GetValue<string>(), currentValue as LocalizedString?);
            case PropertyKind.Boolean:
                return node.GetValue<bool>();
            case PropertyKind.Integer:
                return ConvertNumber(ReadNumber(node), property, valueType, true);
            case PropertyKind.Decimal:
                return ConvertNumber(ReadNumber(node), property, valueType, false);
            case PropertyKind.Enum:
                return ParseEnum(valueType, node.GetValue<string>());
            case PropertyKind.Flags:
                var flags = node.AsArray().Select(flag => Convert.ToInt64(ParseEnum(valueType, flag!.GetValue<string>()), CultureInfo.InvariantCulture))
                    .Aggregate(0L, (all, flag) => all | flag);
                return Enum.ToObject(valueType, flags);
            case PropertyKind.DateTime when valueType == typeof(DateTimeOffset):
                return DateTimeOffset.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            case PropertyKind.DateTime:
                return DateTime.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            case PropertyKind.Date:
                return DateOnly.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture);
            case PropertyKind.Time:
                return TimeOnly.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture);
            case PropertyKind.TimeSpan:
                return TimeSpan.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture);
            case PropertyKind.Bytes:
                return Convert.FromHexString(node.GetValue<string>());
            default:
                throw new InvalidValueException("Este valor no se puede modificar.");
        }
    }

    /// <summary>
    /// Changes the text in the neutral language, which is the text the serializer shows, and keeps the translations.
    /// </summary>
    private static LocalizedString ConvertLocalizedText(string text, LocalizedString? current)
    {
        if (current is not { } currentValue)
        {
            return new LocalizedString(text);
        }

        return currentValue.ValueInNeutralLanguage == text ? currentValue : currentValue.WithTranslation(NeutralCulture, text);
    }

    private static object ConvertNumber(decimal value, PropertySchema property, Type valueType, bool isInteger)
    {
        if (isInteger && value != decimal.Truncate(value))
        {
            throw new InvalidValueException("Tiene que ser un número entero.");
        }

        var minimum = property.Minimum ?? GetLimit(valueType, "MinValue");
        var maximum = property.Maximum ?? GetLimit(valueType, "MaxValue");
        if ((minimum is { } min && (double)value < min) || (maximum is { } max && (double)value > max))
        {
            throw new InvalidValueException($"Tiene que estar entre {FormatLimit(minimum)} y {FormatLimit(maximum)}.");
        }

        try
        {
            return Convert.ChangeType(value, valueType, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            throw new InvalidValueException("El número es demasiado grande.");
        }
    }

    private static double? GetLimit(Type numberType, string fieldName)
    {
        // The limits of float and double are too large to be meaningful for the admin.
        if (numberType == typeof(float) || numberType == typeof(double) || numberType == typeof(decimal))
        {
            return null;
        }

        return numberType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is { } limit
            ? Convert.ToDouble(limit, CultureInfo.InvariantCulture)
            : null;
    }

    private static string FormatLimit(double? limit) => limit?.ToString(CultureInfo.InvariantCulture) ?? "∞";

    private static object ParseEnum(Type enumType, string name)
    {
        return Enum.TryParse(enumType, name, false, out var value) && Enum.IsDefined(enumType, value!)
            ? value!
            : throw new InvalidValueException($"La opción \"{name}\" no existe.");
    }

    private async Task ApplyObjectAsync(object target, Type type, JsonObject values, string path, WriteState state)
    {
        var schema = this._registry.GetSchema(type);
        foreach (var (name, node) in values)
        {
            var propertyPath = Combine(path, name);
            var property = schema.Properties.FirstOrDefault(p => p.Name == name);
            if (property is null)
            {
                state.Errors.Add(new FieldError(propertyPath, "Propiedad desconocida."));
                continue;
            }

            if (property.IsReadOnly)
            {
                state.Errors.Add(new FieldError(propertyPath, "Este valor no se puede modificar."));
                continue;
            }

            try
            {
                await this.ApplyPropertyAsync(target, property, node, propertyPath, state).ConfigureAwait(false);
            }
            catch (InvalidValueException ex)
            {
                state.Errors.Add(new FieldError(ex.Path ?? propertyPath, ex.Message));
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or InvalidOperationException or JsonException or ArgumentException or OverflowException or TargetInvocationException)
            {
                state.Errors.Add(new FieldError(propertyPath, "Valor inválido."));
            }
        }

        Validate(target, path, state);
    }

    private async Task ApplyPropertyAsync(object target, PropertySchema property, JsonNode? node, string path, WriteState state)
    {
        switch (property.Kind)
        {
            case PropertyKind.Reference:
                property.Property.SetValue(target, await this.ResolveReferenceAsync(node, property.TargetClrType!, path, state).ConfigureAwait(false));
                break;
            case PropertyKind.ReferenceList:
                await this.ApplyReferenceListAsync(target, property, node, path, state).ConfigureAwait(false);
                break;
            case PropertyKind.Embedded:
                await this.ApplyEmbeddedAsync(target, property, node, path, state).ConfigureAwait(false);
                break;
            case PropertyKind.EmbeddedList:
                await this.ApplyEmbeddedListAsync(target, property, node, path, state).ConfigureAwait(false);
                break;
            case PropertyKind.ValueList:
                var elementType = GetElementType(property);
                var valueType = Nullable.GetUnderlyingType(elementType) ?? elementType;
                var collection = GetCollection(target, property);
                var newValues = (node?.AsArray() ?? []).Select(value => ConvertListValue(value, property, valueType)).ToList();
                collection.Clear();
                newValues.ForEach(collection.Add);
                break;
            default:
                property.Property.SetValue(target, ConvertScalar(node, property, property.Property.GetValue(target)));
                break;
        }
    }

    private async Task<object?> ResolveReferenceAsync(JsonNode? node, Type targetType, string path, WriteState state)
    {
        if (node is null)
        {
            return null;
        }

        var id = ReadId(node) ?? throw new InvalidValueException("Falta el id del objeto referenciado.", path);
        return await state.Context.GetByIdAsync(id, targetType, state.CancellationToken).ConfigureAwait(false)
               ?? throw new InvalidValueException("El objeto referenciado no existe.", path);
    }

    private async Task ApplyReferenceListAsync(object target, PropertySchema property, JsonNode? node, string path, WriteState state)
    {
        var wanted = new List<object>();
        var index = 0;
        foreach (var item in node?.AsArray() ?? [])
        {
            if (await this.ResolveReferenceAsync(item, property.TargetClrType!, $"{path}[{index}]", state).ConfigureAwait(false) is { } resolved
                && wanted.All(w => w.GetId() != resolved.GetId()))
            {
                wanted.Add(resolved);
            }

            index++;
        }

        var collection = GetCollection(target, property);
        var wantedIds = wanted.Select(w => w.GetId()).ToHashSet();
        foreach (var removed in collection.Items.Where(item => !wantedIds.Contains(item.GetId())).ToList())
        {
            collection.Remove(removed);
        }

        var existingIds = collection.Items.Select(item => item.GetId()).ToHashSet();
        foreach (var added in wanted.Where(w => !existingIds.Contains(w.GetId())))
        {
            collection.Add(added);
        }
    }

    private async Task ApplyEmbeddedAsync(object target, PropertySchema property, JsonNode? node, string path, WriteState state)
    {
        var current = property.Property.GetValue(target);
        if (node is null)
        {
            if (current is not null)
            {
                property.Property.SetValue(target, null);
                await DeleteOwnedAsync(state, current).ConfigureAwait(false);
            }

            return;
        }

        if (current is null)
        {
            current = CreateOwned(state, property.TargetClrType!);
            property.Property.SetValue(target, current);
        }

        await this.ApplyObjectAsync(current, property.TargetClrType!, ReadValues(node, path), path, state).ConfigureAwait(false);
    }

    private async Task ApplyEmbeddedListAsync(object target, PropertySchema property, JsonNode? node, string path, WriteState state)
    {
        if (state.PlainObjects)
        {
            await this.ApplyPlainListAsync(target, property, node, path, state).ConfigureAwait(false);
            return;
        }

        var collection = GetCollection(target, property);
        var existing = collection.Items.ToDictionary(item => item.GetId());
        var kept = new HashSet<Guid>();
        var added = new List<object>();
        var index = 0;
        foreach (var item in node?.AsArray() ?? [])
        {
            var itemPath = $"{path}[{index++}]";
            var values = ReadValues(item, itemPath);
            if (ReadId(item) is { } id && existing.TryGetValue(id, out var existingItem) && kept.Add(id))
            {
                await this.ApplyObjectAsync(existingItem, property.TargetClrType!, values, itemPath, state).ConfigureAwait(false);
            }
            else
            {
                var newItem = CreateOwned(state, property.TargetClrType!);
                await this.ApplyObjectAsync(newItem, property.TargetClrType!, values, itemPath, state).ConfigureAwait(false);
                added.Add(newItem);
            }
        }

        foreach (var removed in existing.Where(e => !kept.Contains(e.Key)).Select(e => e.Value))
        {
            collection.Remove(removed);
            await DeleteOwnedAsync(state, removed).ConfigureAwait(false);
        }

        added.ForEach(collection.Add);
    }

    /// <summary>
    /// Applies a list of plain objects, which have no ids: the n-th sent object changes the n-th
    /// object of the list, so its values which aren't sent stay.
    /// </summary>
    private async Task ApplyPlainListAsync(object target, PropertySchema property, JsonNode? node, string path, WriteState state)
    {
        var collection = GetCollection(target, property);
        var existing = collection.Items.ToList();
        var result = new List<object>();
        var index = 0;
        foreach (var item in node?.AsArray() ?? [])
        {
            var itemPath = $"{path}[{index}]";
            var obj = index < existing.Count ? existing[index] : CreateOwned(state, property.TargetClrType!);
            await this.ApplyObjectAsync(obj, property.TargetClrType!, ReadValues(item, itemPath), itemPath, state).ConfigureAwait(false);
            result.Add(obj);
            index++;
        }

        collection.Clear();
        result.ForEach(collection.Add);
    }

    /// <summary>
    /// The state of applying values to an object and its owned objects.
    /// </summary>
    private sealed record WriteState(IContext Context, CancellationToken CancellationToken, bool PlainObjects)
    {
        public List<FieldError> Errors { get; } = [];
    }

    /// <summary>
    /// Accesses an <see cref="ICollection{T}"/> without knowing its type at compile time.
    /// </summary>
    private sealed class CollectionAccessor
    {
        private readonly object _collection;
        private readonly MethodInfo _add;
        private readonly MethodInfo _remove;
        private readonly MethodInfo _clear;

        public CollectionAccessor(object collection, Type elementType)
        {
            var collectionType = typeof(ICollection<>).MakeGenericType(elementType);
            this._collection = collection;
            this._add = collectionType.GetMethod(nameof(ICollection<object>.Add))!;
            this._remove = collectionType.GetMethod(nameof(ICollection<object>.Remove))!;
            this._clear = collectionType.GetMethod(nameof(ICollection<object>.Clear))!;
        }

        public IEnumerable<object> Items => ((IEnumerable)this._collection).OfType<object>();

        public void Add(object item) => this._add.Invoke(this._collection, [item]);

        public void Remove(object item) => this._remove.Invoke(this._collection, [item]);

        public void Clear() => this._clear.Invoke(this._collection, null);
    }

    /// <summary>
    /// A value which can't be applied; it becomes a <see cref="FieldError"/>.
    /// </summary>
    private sealed class InvalidValueException : Exception
    {
        public InvalidValueException(string message, string? path = null)
            : base(message)
        {
            this.Path = path;
        }

        public string? Path { get; }
    }
}
