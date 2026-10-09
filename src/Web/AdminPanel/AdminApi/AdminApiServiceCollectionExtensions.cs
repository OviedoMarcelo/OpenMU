// <copyright file="AdminApiServiceCollectionExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

/// <summary>
/// Extensions which add the admin API, which is used by the separate admin frontend.
/// </summary>
public static class AdminApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds the services of the admin API. Requires the authentication of the admin panel.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same instance, to allow chaining of further calls.</returns>
    public static IServiceCollection AddAdminApi(this IServiceCollection services)
    {
        services.AddSingleton<AdminTokenService>();
        services.AddSingleton<ServerMetricsSampler>();
        services.AddSingleton<ConfigurationTypeRegistry>();
        services.AddSingleton<ConfigurationValueSerializer>();
        services.AddHostedService(provider => provider.GetRequiredService<ServerMetricsSampler>());
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AdminTokenAuthenticationHandler>(
                AdminApiDefaults.AuthenticationScheme,
                configureOptions: null);
        return services;
    }
}
