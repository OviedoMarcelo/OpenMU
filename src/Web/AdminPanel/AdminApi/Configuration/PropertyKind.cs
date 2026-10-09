// <copyright file="PropertyKind.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using System.Text.Json.Serialization;

/// <summary>
/// The kind of a property, which decides how the admin frontend shows and edits it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PropertyKind>))]
public enum PropertyKind
{
    /// <summary>
    /// A kind which isn't supported by the admin frontend.
    /// </summary>
    Unknown,

    /// <summary>
    /// A text.
    /// </summary>
    Text,

    /// <summary>
    /// A text with translations (<see cref="Interfaces.LocalizedString"/>).
    /// </summary>
    LocalizedText,

    /// <summary>
    /// A whole number.
    /// </summary>
    Integer,

    /// <summary>
    /// A number with decimals.
    /// </summary>
    Decimal,

    /// <summary>
    /// A boolean.
    /// </summary>
    Boolean,

    /// <summary>
    /// One value of an enumeration.
    /// </summary>
    Enum,

    /// <summary>
    /// A combination of values of a flags enumeration.
    /// </summary>
    Flags,

    /// <summary>
    /// A point in time.
    /// </summary>
    DateTime,

    /// <summary>
    /// A date.
    /// </summary>
    Date,

    /// <summary>
    /// A time of the day.
    /// </summary>
    Time,

    /// <summary>
    /// A duration.
    /// </summary>
    TimeSpan,

    /// <summary>
    /// Binary data.
    /// </summary>
    Bytes,

    /// <summary>
    /// A reference to another object, which is selected from the existing ones.
    /// </summary>
    Reference,

    /// <summary>
    /// An object which is owned by the declaring object (<see cref="DataModel.Composition.MemberOfAggregateAttribute"/>).
    /// </summary>
    Embedded,

    /// <summary>
    /// A list of references to other objects.
    /// </summary>
    ReferenceList,

    /// <summary>
    /// A list of objects which are owned by the declaring object.
    /// </summary>
    EmbeddedList,

    /// <summary>
    /// A list of simple values, e.g. numbers.
    /// </summary>
    ValueList,
}
