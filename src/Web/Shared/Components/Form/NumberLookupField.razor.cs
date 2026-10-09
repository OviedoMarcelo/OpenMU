// <copyright file="NumberLookupField.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.Components.Form;

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// A field for a number which identifies an object of the configuration, like the number of a monster or a map.
/// It offers the objects by their name, and stores their number.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
public partial class NumberLookupField<TObject>
    where TObject : class
{
    private IReadOnlyList<TObject> _objects = [];

    /// <summary>
    /// Gets or sets the label which should be displayed. If it's not explicitly provided, the component shows the
    /// name of the <see cref="System.ComponentModel.DataAnnotations.DisplayAttribute"/> of the property.
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the function which gets the number of an object.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Func<TObject, short> NumberSelector { get; set; } = null!;

    /// <summary>
    /// Gets or sets the persistence context.
    /// </summary>
    [CascadingParameter]
    public IContext PersistenceContext { get; set; } = null!;

    /// <summary>
    /// Gets or sets the lookup controller.
    /// </summary>
    [Inject]
    public ILookupController LookupController { get; set; } = null!;

    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    private TObject? SelectedObject
    {
        get => this._objects.FirstOrDefault(o => this.NumberSelector(o) == this.CurrentValue);
        set => this.CurrentValue = value is null ? default : this.NumberSelector(value);
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync().ConfigureAwait(true);
        await this.GameConfigurationSource.GetOwnerAsync().ConfigureAwait(true);
        this._objects = this.GameConfigurationSource.GetAll<TObject>().ToList();
    }

    /// <inheritdoc />
    protected override bool TryParseValueFromString(string? value, [MaybeNullWhen(false)] out short result, [NotNullWhen(false)] out string? validationErrorMessage)
    {
        throw new NotImplementedException();
    }

    private string GetCaption(TObject? item)
    {
        return item is null ? string.Empty : $"{item.GetName()} ({this.NumberSelector(item)})";
    }
}
