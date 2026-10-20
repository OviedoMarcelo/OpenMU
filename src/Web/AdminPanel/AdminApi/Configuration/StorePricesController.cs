// <copyright file="StorePricesController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using static MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration.ConfigurationEditController;

/// <summary>
/// The items which the merchant NPCs sell and their prices, to see and change the prices of all
/// stores at once instead of store by store.
/// </summary>
/// <remarks>
/// The prices are changed on the context of the data source, like the changes of a merchant through
/// <see cref="ConfigurationEditController"/>, so saving publishes them to the running game servers.
/// Every changed merchant is recorded in the <see cref="AdminAuditLog"/>.
/// </remarks>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/store-prices")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class StorePricesController : ControllerBase
{
    private readonly IDataSource<GameConfiguration> _dataSource;
    private readonly ConfigurationTypeRegistry _registry;
    private readonly AdminAuditLog _auditLog;
    private readonly ILogger<StorePricesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorePricesController"/> class.
    /// </summary>
    /// <param name="dataSource">The data source of the game configuration.</param>
    /// <param name="registry">The type registry.</param>
    /// <param name="auditLog">The audit log.</param>
    /// <param name="logger">The logger.</param>
    public StorePricesController(IDataSource<GameConfiguration> dataSource, ConfigurationTypeRegistry registry, AdminAuditLog auditLog, ILogger<StorePricesController> logger)
    {
        this._dataSource = dataSource;
        this._registry = registry;
        this._auditLog = auditLog;
        this._logger = logger;
    }

    /// <summary>
    /// Gets the items which are sold by any merchant, with the price at each one.
    /// </summary>
    /// <returns>The items on sale.</returns>
    [HttpGet]
    public async Task<IReadOnlyList<StorePrices.ItemOnSale>> GetAsync()
    {
        var configuration = await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        return StorePrices.GetItemsOnSale(configuration);
    }

    /// <summary>
    /// Changes the own prices of store items, all or none.
    /// </summary>
    /// <param name="request">The changes.</param>
    /// <returns>The items on sale after the change.</returns>
    [HttpPut]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<IReadOnlyList<StorePrices.ItemOnSale>>> UpdateAsync([FromBody] UpdateRequest request)
    {
        await DataSourceLock.WaitAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        try
        {
            var configuration = await this._dataSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
            var context = await this._dataSource.GetContextAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
            IReadOnlyList<StorePrices.MerchantPriceChanges> changed;
            try
            {
                changed = StorePrices.Apply(configuration, request.Changes ?? []);
            }
            catch (ConfigurationValidationException ex)
            {
                return this.BadRequest(new ErrorResponse("Hay precios inválidos.", ex.Errors));
            }

            if (changed.Count > 0)
            {
                try
                {
                    await context.SaveChangesAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "The store prices couldn't be saved.");
                    await this._dataSource.ForceDiscardChangesAsync().ConfigureAwait(false);
                    return this.Conflict(new ErrorResponse($"No se pudo guardar: {(ex.InnerException ?? ex).Message}"));
                }

                foreach (var (merchant, changes) in changed)
                {
                    await this._auditLog.AddAsync(new AuditEntry(
                        DateTimeOffset.UtcNow,
                        this.User.Identity?.Name,
                        AuditAction.Updated,
                        this._registry.GetName(typeof(MonsterDefinition)),
                        this._registry.GetCaption(typeof(MonsterDefinition)),
                        merchant.GetId(),
                        merchant.GetName(),
                        changes)).ConfigureAwait(false);
                }
            }

            return this.Ok(StorePrices.GetItemsOnSale(configuration));
        }
        finally
        {
            DataSourceLock.Release();
        }
    }

    /// <summary>
    /// The changes of the own prices.
    /// </summary>
    /// <param name="Changes">The changes.</param>
    public record UpdateRequest(IReadOnlyList<StorePrices.PriceChange>? Changes);
}
