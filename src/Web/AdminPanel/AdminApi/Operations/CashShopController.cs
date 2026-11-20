// <copyright file="CashShopController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Operations;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Persistence.CashShop;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Operations on the points of the cash shop (the Cyber Coins).
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/cash-shop")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
public class CashShopController : ControllerBase
{
    private const long MaximumGift = 1_000_000;

    private readonly AdminAuditLog _auditLog;
    private readonly ILogger<CashShopController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CashShopController"/> class.
    /// </summary>
    /// <param name="auditLog">The audit log.</param>
    /// <param name="logger">The logger.</param>
    public CashShopController(AdminAuditLog auditLog, ILogger<CashShopController> logger)
    {
        this._auditLog = auditLog;
        this._logger = logger;
    }

    /// <summary>
    /// Gives points to every account, e.g. so that all players can test the shop. Each account gets the
    /// points of a gift only once, so giving the same gift again only reaches the accounts created since then.
    /// </summary>
    /// <param name="request">The gift.</param>
    /// <returns>The count of accounts which got the points now.</returns>
    [HttpPost("gift-all")]
    public async Task<ActionResult<GiftResult>> GiftToAllAsync([FromBody] GiftRequest request)
    {
        var name = request.Name?.Trim();
        if (request.Amount is < 1 or > MaximumGift || string.IsNullOrEmpty(name) || name.Length > 64)
        {
            return this.BadRequest(new { message = $"La cantidad tiene que ser de 1 a {MaximumGift:N0} y el nombre del regalo de 1 a 64 caracteres." });
        }

        if (CashShopRepositoryRegistry.Current is not { } repository)
        {
            return this.StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "El almacenamiento de la tienda no está disponible." });
        }

        var accounts = await repository.AddPointsToAllAccountsAsync(request.Amount, "gift", $"gift:{name}", name).ConfigureAwait(false);
        this._logger.LogInformation("Admin '{Admin}' gave {Amount} cash shop points to {Accounts} accounts (gift '{Gift}').", this.User.Identity?.Name, request.Amount, accounts, name);
        await this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            AuditAction.Updated,
            "CashShopGift",
            "Regalo de Cyber Coins",
            Guid.Empty,
            name,
            [new AuditChange("Amount", "Cyber Coins por cuenta", null, request.Amount.ToString()), new AuditChange("Accounts", "Cuentas", null, accounts.ToString())])).ConfigureAwait(false);
        return new GiftResult(accounts);
    }

    /// <summary>
    /// A gift of points for every account.
    /// </summary>
    /// <param name="Amount">The points which every account gets.</param>
    /// <param name="Name">The name of the gift, e.g. "beta-diciembre"; every account gets a gift only once.</param>
    public record GiftRequest(long Amount, string? Name);

    /// <summary>
    /// The result of a gift.
    /// </summary>
    /// <param name="Accounts">The count of accounts which got the points now.</param>
    public record GiftResult(int Accounts);
}
