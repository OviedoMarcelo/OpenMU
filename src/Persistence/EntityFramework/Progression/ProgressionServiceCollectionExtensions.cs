// <copyright file="ProgressionServiceCollectionExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Progression;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// Extensions to register the persistence of the long term progression.
/// </summary>
public static class ProgressionServiceCollectionExtensions
{
    /// <summary>
    /// Adds the database backed <see cref="IProgressionRepository"/> to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same instance, to allow chaining of further calls.</returns>
    public static IServiceCollection AddProgressionRepository(this IServiceCollection services)
    {
        services.TryAddSingleton<IProgressionRepository, ProgressionRepository>();
        return services;
    }
}
