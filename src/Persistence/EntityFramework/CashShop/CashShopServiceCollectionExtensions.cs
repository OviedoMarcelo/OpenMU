// <copyright file="CashShopServiceCollectionExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.CashShop;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MUnique.OpenMU.Persistence.CashShop;

/// <summary>
/// Extensions to register the database backed <see cref="ICashShopRepository"/>.
/// </summary>
public static class CashShopServiceCollectionExtensions
{
    /// <summary>
    /// Adds the database backed <see cref="ICashShopRepository"/> to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same instance, to allow chaining of further calls.</returns>
    public static IServiceCollection AddCashShopRepository(this IServiceCollection services)
    {
        services.TryAddSingleton<ICashShopRepository, CashShopRepository>();
        return services;
    }
}
