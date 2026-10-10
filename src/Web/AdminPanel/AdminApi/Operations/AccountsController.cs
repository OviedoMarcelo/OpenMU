// <copyright file="AccountsController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Operations;

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// Lists, creates and changes the accounts with their characters.
/// </summary>
/// <remarks>
/// An account is loaded and saved with its own player context, like the game server does. While the
/// account is logged in, the game server holds its own copy and would overwrite the changes when the
/// player leaves, so it can't be changed then - it has to be disconnected first.
/// </remarks>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/accounts")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class AccountsController : ControllerBase
{
    private const int MaximumCount = 200;
    private const string LoggedInMessage = "La cuenta está conectada. Desconectala antes de modificarla: el servidor de juego pisaría los cambios al salir.";

    private readonly IDataSource<GameConfiguration> _configurationSource;
    private readonly IDataSource<Account> _accountSource;
    private readonly IPersistenceContextProvider _contextProvider;
    private readonly ILoginServer _loginServer;
    private readonly LoggedInAccountService _loggedInAccounts;
    private readonly ConfigurationTypeRegistry _registry;
    private readonly ConfigurationValueSerializer _serializer;
    private readonly ConfigurationValueWriter _writer;
    private readonly AdminAuditLog _auditLog;
    private readonly ILogger<AccountsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountsController"/> class.
    /// </summary>
    /// <param name="configurationSource">The game configuration, which the player contexts need.</param>
    /// <param name="accountSource">The account data source of the admin panel, whose cached account is discarded after a change.</param>
    /// <param name="contextProvider">The persistence context provider.</param>
    /// <param name="loginServer">The login server, which knows the logged in accounts.</param>
    /// <param name="loggedInAccounts">The service which disconnects accounts.</param>
    /// <param name="registry">The type registry.</param>
    /// <param name="serializer">The serializer.</param>
    /// <param name="writer">The writer.</param>
    /// <param name="auditLog">The audit log.</param>
    /// <param name="logger">The logger.</param>
    public AccountsController(
        IDataSource<GameConfiguration> configurationSource,
        IDataSource<Account> accountSource,
        IPersistenceContextProvider contextProvider,
        ILoginServer loginServer,
        LoggedInAccountService loggedInAccounts,
        ConfigurationTypeRegistry registry,
        ConfigurationValueSerializer serializer,
        ConfigurationValueWriter writer,
        AdminAuditLog auditLog,
        ILogger<AccountsController> logger)
    {
        this._configurationSource = configurationSource;
        this._accountSource = accountSource;
        this._contextProvider = contextProvider;
        this._loginServer = loginServer;
        this._loggedInAccounts = loggedInAccounts;
        this._registry = registry;
        this._serializer = serializer;
        this._writer = writer;
        this._auditLog = auditLog;
        this._logger = logger;
    }

    /// <summary>
    /// Gets a page of the accounts, ordered by their login name.
    /// </summary>
    /// <param name="q">A text which the login name or the name of a character contains.</param>
    /// <param name="offset">The number of accounts to skip.</param>
    /// <param name="count">The maximum number of accounts.</param>
    /// <returns>The accounts.</returns>
    [HttpGet]
    public async Task<AccountPage> GetAccountsAsync([FromQuery] string? q = null, [FromQuery] int offset = 0, [FromQuery] int count = 50)
    {
        count = Math.Clamp(count, 1, MaximumCount);
        offset = Math.Max(0, offset);
        using var context = await this.CreateContextAsync().ConfigureAwait(false);

        // One more than requested tells whether there's another page.
        var accounts = string.IsNullOrWhiteSpace(q)
            ? await context.GetAccountsOrderedByLoginNameAsync(offset, count + 1, this.HttpContext.RequestAborted).ConfigureAwait(false)
            : await context.SearchAccountsAsync(q.Trim(), offset, count + 1, this.HttpContext.RequestAborted).ConfigureAwait(false);
        var online = await this._loginServer.GetSnapshotAsync().ConfigureAwait(false);
        var rows = accounts
            .Select(account => new AccountRow(
                account.GetId(),
                account.LoginName,
                account.EMail,
                account.State.ToString(),
                account.RegistrationDate,
                online.TryGetValue(account.LoginName, out var server) ? server : null))
            .ToList();
        return new AccountPage(rows.Take(count).ToList(), rows.Count > count);
    }

    /// <summary>
    /// Gets an account with its characters.
    /// </summary>
    /// <param name="id">The id of the account.</param>
    /// <returns>The account.</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDetails>> GetAccountAsync(Guid id)
    {
        using var context = await this.CreateContextAsync().ConfigureAwait(false);
        if (await context.GetByIdAsync<Account>(id, this.HttpContext.RequestAborted).ConfigureAwait(false) is not { } account)
        {
            return this.NotFound();
        }

        return new AccountDetails(this._serializer.SerializeObject(account, typeof(Account)), await this.GetOnlineServerAsync(account.LoginName).ConfigureAwait(false));
    }

    /// <summary>
    /// Creates an account.
    /// </summary>
    /// <param name="request">The data of the account.</param>
    /// <returns>The account.</returns>
    [HttpPost]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<AccountDetails>> CreateAccountAsync([FromBody] CreateAccountRequest request)
    {
        var loginName = request.LoginName?.Trim() ?? string.Empty;
        var errors = new List<FieldError>();
        CheckLength(errors, "LoginName", loginName, 3, 10);
        CheckLength(errors, "Password", request.Password, 3, 20);
        CheckLength(errors, "SecurityCode", request.SecurityCode, 3, 10);
        if (!Enum.TryParse<AccountState>(request.State ?? nameof(AccountState.Normal), out var state))
        {
            errors.Add(new FieldError("State", "Estado inválido."));
        }

        using var context = await this.CreateContextAsync().ConfigureAwait(false);
        if (errors.Count == 0 && await context.GetAccountByLoginNameAsync(loginName, this.HttpContext.RequestAborted).ConfigureAwait(false) is not null)
        {
            errors.Add(new FieldError("LoginName", "Ya existe una cuenta con ese nombre."));
        }

        if (errors.Count > 0)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("Hay valores inválidos.", errors));
        }

        // Like the account creation of the admin panel (AccountService).
        var account = context.CreateNew<Account>();
        account.LoginName = loginName;
        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        account.SecurityCode = request.SecurityCode!;
        account.EMail = request.EMail?.Trim() ?? string.Empty;
        account.State = state;
        account.RegistrationDate = DateTime.UtcNow;
        await context.SaveChangesAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);

        await this.AuditAsync(AuditAction.Created, account, [new AuditChange("LoginName", "Usuario", null, account.LoginName)]).ConfigureAwait(false);
        return this.StatusCode(StatusCodes.Status201Created, new AccountDetails(this._serializer.SerializeObject(account, typeof(Account)), null));
    }

    /// <summary>
    /// Changes an account and its characters. Only the sent values are changed.
    /// </summary>
    /// <param name="id">The id of the account.</param>
    /// <param name="request">The values, in the format of the configuration.</param>
    /// <returns>The changed account.</returns>
    [HttpPut("{id:guid}")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<AccountDetails>> UpdateAccountAsync(Guid id, [FromBody] ConfigurationEditController.ValuesRequest request)
    {
        using var context = await this.CreateContextAsync().ConfigureAwait(false);
        if (await context.GetByIdAsync<Account>(id, this.HttpContext.RequestAborted).ConfigureAwait(false) is not { } account)
        {
            return this.NotFound();
        }

        if (await this.GetOnlineServerAsync(account.LoginName).ConfigureAwait(false) is not null)
        {
            return this.Conflict(new ConfigurationEditController.ErrorResponse(LoggedInMessage));
        }

        var before = this._serializer.SerializeObject(account, typeof(Account));
        try
        {
            await this._writer.ApplyAsync(account, typeof(Account), request.Values ?? [], context, this.HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (ConfigurationValidationException ex)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("Hay valores inválidos.", ex.Errors));
        }

        if (await this.SaveAsync(context, account).ConfigureAwait(false) is { } error)
        {
            return error;
        }

        var after = this._serializer.SerializeObject(account, typeof(Account));
        var changes = AuditChanges.Between(this._registry.GetSchema(typeof(Account)), before, after);
        if (changes.Count > 0)
        {
            await this.AuditAsync(AuditAction.Updated, account, changes).ConfigureAwait(false);
        }

        return new AccountDetails(after, null);
    }

    /// <summary>
    /// Changes the state of an account, e.g. bans it. A banned account is disconnected.
    /// </summary>
    /// <param name="id">The id of the account.</param>
    /// <param name="request">The new state.</param>
    /// <returns>The changed account.</returns>
    [HttpPost("{id:guid}/state")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<ActionResult<AccountDetails>> ChangeStateAsync(Guid id, [FromBody] ChangeStateRequest request)
    {
        if (!Enum.TryParse<AccountState>(request.State, out var state))
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("Estado inválido."));
        }

        using var context = await this.CreateContextAsync().ConfigureAwait(false);
        if (await context.GetByIdAsync<Account>(id, this.HttpContext.RequestAborted).ConfigureAwait(false) is not { } account)
        {
            return this.NotFound();
        }

        // The state is checked at the login, so it can be changed while the account is logged in;
        // a banned account is disconnected, so the ban takes effect right away.
        var server = await this.GetOnlineServerAsync(account.LoginName).ConfigureAwait(false);
        if (state == AccountState.Banned && server is { } serverId)
        {
            await this._loggedInAccounts.SetAccountOfflineAsync(new LoggedInAccount(account.LoginName, serverId)).ConfigureAwait(false);
            server = null;
        }

        var previous = account.State;
        account.State = state;
        if (await this.SaveAsync(context, account).ConfigureAwait(false) is { } error)
        {
            return error;
        }

        await this.AuditAsync(AuditAction.Updated, account, [new AuditChange("State", "Estado", previous.ToString(), state.ToString())]).ConfigureAwait(false);
        return new AccountDetails(this._serializer.SerializeObject(account, typeof(Account)), server);
    }

    /// <summary>
    /// Replaces the secrets of an account: the password, the security code or the vault password.
    /// </summary>
    /// <param name="id">The id of the account.</param>
    /// <param name="request">The secrets to replace; the others stay.</param>
    /// <returns>The result.</returns>
    [HttpPost("{id:guid}/credentials")]
    [Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Operator)]
    public async Task<IActionResult> ChangeCredentialsAsync(Guid id, [FromBody] CredentialsRequest request)
    {
        var errors = new List<FieldError>();
        if (request.Password is not null)
        {
            CheckLength(errors, "Password", request.Password, 3, 20);
        }

        if (request.SecurityCode is not null)
        {
            CheckLength(errors, "SecurityCode", request.SecurityCode, 3, 10);
        }

        if (request.VaultPassword is { Length: > 0 } && request.VaultPassword.Length > 10)
        {
            errors.Add(new FieldError("VaultPassword", "Tiene que tener como máximo 10 caracteres."));
        }

        if (errors.Count > 0)
        {
            return this.BadRequest(new ConfigurationEditController.ErrorResponse("Hay valores inválidos.", errors));
        }

        using var context = await this.CreateContextAsync().ConfigureAwait(false);
        if (await context.GetByIdAsync<Account>(id, this.HttpContext.RequestAborted).ConfigureAwait(false) is not { } account)
        {
            return this.NotFound();
        }

        if (await this.GetOnlineServerAsync(account.LoginName).ConfigureAwait(false) is not null)
        {
            return this.Conflict(new ConfigurationEditController.ErrorResponse(LoggedInMessage));
        }

        var changes = new List<AuditChange>();
        if (request.Password is not null)
        {
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            changes.Add(new AuditChange("Password", "Contraseña", null, null));
        }

        if (request.SecurityCode is not null)
        {
            account.SecurityCode = request.SecurityCode;
            changes.Add(new AuditChange("SecurityCode", "Código de seguridad", null, null));
        }

        if (request.VaultPassword is not null)
        {
            account.VaultPassword = request.VaultPassword;
            changes.Add(new AuditChange("VaultPassword", "Contraseña del baúl", null, null));
        }

        if (changes.Count == 0)
        {
            return this.NoContent();
        }

        if (await this.SaveAsync(context, account).ConfigureAwait(false) is { } error)
        {
            return error;
        }

        await this.AuditAsync(AuditAction.Updated, account, changes).ConfigureAwait(false);
        return this.NoContent();
    }

    private static void CheckLength(List<FieldError> errors, string path, string? value, int minimum, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < minimum || value.Length > maximum)
        {
            errors.Add(new FieldError(path, $"Tiene que tener entre {minimum} y {maximum} caracteres."));
        }
    }

    private async Task<IPlayerContext> CreateContextAsync()
    {
        var gameConfiguration = await this._configurationSource.GetOwnerAsync(default, this.HttpContext.RequestAborted).ConfigureAwait(false);
        return this._contextProvider.CreateNewPlayerContext(gameConfiguration);
    }

    private async Task<byte?> GetOnlineServerAsync(string loginName)
    {
        var online = await this._loginServer.GetSnapshotAsync().ConfigureAwait(false);
        return online.TryGetValue(loginName, out var server) ? server : null;
    }

    private async Task<ActionResult?> SaveAsync(IContext context, Account account)
    {
        try
        {
            await context.SaveChangesAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "The account {LoginName} couldn't be saved.", account.LoginName);
            return this.Conflict(new ConfigurationEditController.ErrorResponse($"No se pudo guardar: {(ex.InnerException ?? ex).Message}"));
        }

        // The admin panel may have the account loaded; it has to load it again.
        await this._accountSource.ForceDiscardChangesAsync().ConfigureAwait(false);
        return null;
    }

    private Task AuditAsync(AuditAction action, Account account, IReadOnlyList<AuditChange> changes)
    {
        return this._auditLog.AddAsync(new AuditEntry(
            DateTimeOffset.UtcNow,
            this.User.Identity?.Name,
            action,
            nameof(Account),
            "Cuentas",
            account.GetId(),
            account.LoginName,
            changes));
    }

    /// <summary>
    /// A page of accounts.
    /// </summary>
    /// <param name="Items">The accounts.</param>
    /// <param name="HasMore">If set to <c>true</c>, there are more accounts after this page.</param>
    public record AccountPage(IReadOnlyList<AccountRow> Items, bool HasMore);

    /// <summary>
    /// An account in a list.
    /// </summary>
    /// <param name="Id">The id.</param>
    /// <param name="LoginName">The login name.</param>
    /// <param name="EMail">The e-mail address.</param>
    /// <param name="State">The state, e.g. <c>Banned</c>.</param>
    /// <param name="RegistrationDate">The registration date.</param>
    /// <param name="OnlineServer">The id of the game server, if the account is logged in.</param>
    public record AccountRow(Guid Id, string LoginName, string EMail, string State, DateTime RegistrationDate, byte? OnlineServer);

    /// <summary>
    /// An account with all of its values.
    /// </summary>
    /// <param name="Account">The account, in the format of the configuration objects.</param>
    /// <param name="OnlineServer">The id of the game server, if the account is logged in.</param>
    public record AccountDetails(JsonObject Account, byte? OnlineServer);

    /// <summary>
    /// The data of a new account.
    /// </summary>
    /// <param name="LoginName">The login name.</param>
    /// <param name="Password">The password.</param>
    /// <param name="SecurityCode">The security code.</param>
    /// <param name="EMail">The e-mail address.</param>
    /// <param name="State">The state; <c>Normal</c> if not set.</param>
    public record CreateAccountRequest(string? LoginName, string? Password, string? SecurityCode, string? EMail, string? State);

    /// <summary>
    /// A new state of an account.
    /// </summary>
    /// <param name="State">The state, e.g. <c>Banned</c>.</param>
    public record ChangeStateRequest(string State);

    /// <summary>
    /// New secrets of an account; the ones which aren't set stay.
    /// </summary>
    /// <param name="Password">The new password.</param>
    /// <param name="SecurityCode">The new security code.</param>
    /// <param name="VaultPassword">The new vault password; empty removes it.</param>
    public record CredentialsRequest(string? Password, string? SecurityCode, string? VaultPassword);
}
