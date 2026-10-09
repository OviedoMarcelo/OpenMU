// <copyright file="NumberLookupFieldBuilder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.ComponentBuilders;

using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MUnique.OpenMU.Web.Shared.Components.Form;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// A <see cref="IComponentBuilder"/> for numbers which identify an object of the configuration, like the number
/// of a monster or a map. Such properties are marked with <typeparamref name="TAttribute"/>.
/// </summary>
/// <typeparam name="TObject">The type of the object which is identified by the number.</typeparam>
/// <typeparam name="TAttribute">The type of the attribute which marks the property.</typeparam>
public class NumberLookupFieldBuilder<TObject, TAttribute> : BaseComponentBuilder, IComponentBuilder
    where TObject : class
    where TAttribute : Attribute
{
    private readonly Func<TObject, short> _numberSelector;

    /// <summary>
    /// Initializes a new instance of the <see cref="NumberLookupFieldBuilder{TObject, TAttribute}"/> class.
    /// </summary>
    /// <param name="numberSelector">The function which gets the number of an object.</param>
    public NumberLookupFieldBuilder(Func<TObject, short> numberSelector)
    {
        this._numberSelector = numberSelector;
    }

    /// <inheritdoc/>
    public int BuildComponent(object model, PropertyInfo propertyInfo, RenderTreeBuilder builder, int currentIndex, IChangeNotificationService notificationService)
    {
        builder.OpenComponent<NumberLookupField<TObject>>(currentIndex++);
        builder.AddAttribute(currentIndex++, nameof(NumberLookupField<TObject>.NumberSelector), this._numberSelector);
        builder.AddAttribute(currentIndex++, nameof(NumberLookupField<TObject>.ValueExpression), model.CreatePropertyExpression<short>(propertyInfo));
        builder.AddAttribute(currentIndex++, nameof(NumberLookupField<TObject>.Value), propertyInfo.GetValue(model));
        builder.AddAttribute(currentIndex++, nameof(NumberLookupField<TObject>.ValueChanged), EventCallback.Factory.Create<short>(
            this,
            value =>
            {
                if (propertyInfo.SetMethod is { })
                {
                    propertyInfo.SetValue(model, value);
                    notificationService.NotifyChange(model, propertyInfo.Name);
                }
            }));
        builder.CloseComponent();
        return currentIndex;
    }

    /// <inheritdoc/>
    public bool CanBuildComponent(PropertyInfo propertyInfo) =>
        propertyInfo.PropertyType == typeof(short)
        && propertyInfo.GetCustomAttribute<TAttribute>() is not null;
}
