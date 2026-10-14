// <copyright file="KeyOfAttribute.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// Marks a text property (or a list of texts) which holds the key of an object of a collection of the root object, e.g. the id of a chapter
/// of a plugin configuration. The admin panel shows a selection of the objects of the collection instead of a text field.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class KeyOfAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyOfAttribute"/> class.
    /// </summary>
    /// <param name="collection">The name of the collection property of the root object.</param>
    public KeyOfAttribute(string collection)
    {
        this.Collection = collection;
    }

    /// <summary>
    /// Gets the name of the collection property of the root object.
    /// </summary>
    public string Collection { get; }

    /// <summary>
    /// Gets or sets the name of the property of the objects of the collection which holds the key.
    /// </summary>
    public string KeyProperty { get; set; } = "Id";

    /// <summary>
    /// Gets or sets the name of the property of the objects of the collection which is shown to the user.
    /// </summary>
    public string CaptionProperty { get; set; } = "Name";
}
