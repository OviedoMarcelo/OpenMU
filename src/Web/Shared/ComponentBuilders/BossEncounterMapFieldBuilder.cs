// <copyright file="BossEncounterMapFieldBuilder.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.ComponentBuilders;

using System.Reflection;
using Microsoft.AspNetCore.Components.Rendering;
using MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;
using MUnique.OpenMU.Web.Shared.Components.Form;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// A <see cref="IComponentBuilder"/> for the places of appearance of a boss encounter, which shows the
/// <see cref="BossEncounterMap"/> above the usual list of the places.
/// </summary>
public class BossEncounterMapFieldBuilder : BaseComponentBuilder, IComponentBuilder
{
    private readonly ObjectCollectionFieldBuilder _listBuilder = new();

    /// <inheritdoc/>
    public int BuildComponent(object model, PropertyInfo propertyInfo, RenderTreeBuilder builder, int currentIndex, IChangeNotificationService notificationService)
    {
        builder.OpenComponent<BossEncounterMap>(currentIndex++);
        builder.AddAttribute(currentIndex++, nameof(BossEncounterMap.Encounter), model);
        builder.CloseComponent();
        return this._listBuilder.BuildComponent(model, propertyInfo, builder, currentIndex, notificationService);
    }

    /// <inheritdoc/>
    public bool CanBuildComponent(PropertyInfo propertyInfo) =>
        propertyInfo.DeclaringType == typeof(BossEncounterDefinition)
        && propertyInfo.Name == nameof(BossEncounterDefinition.SpawnPoints);
}
