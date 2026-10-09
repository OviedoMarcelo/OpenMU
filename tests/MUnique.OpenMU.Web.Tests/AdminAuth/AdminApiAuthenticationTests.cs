// <copyright file="AdminApiAuthenticationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AdminAuth;

using System.IO;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Web.AdminPanel.AdminApi;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Tests the login and the token authentication of the admin API.
/// </summary>
[TestFixture]
public class AdminApiAuthenticationTests
{
    private const string TestPassword = "a-very-long-test-password";

    private string _keyDirectoryPath = null!;
    private ServiceProvider _serviceProvider = null!;
    private InMemoryAdminUserRepository _repository = null!;
    private AdminPanelAuthOptions _authOptions = null!;

    /// <summary>
    /// Sets up the services of the admin panel authentication and of the admin API.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._keyDirectoryPath = Path.Combine(Path.GetTempPath(), "openmu-admin-api-tests-" + Guid.NewGuid());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminPanel:Auth:DataProtectionKeyPath"] = this._keyDirectoryPath,
            })
            .Build();

        this._repository = new InMemoryAdminUserRepository();
        this._authOptions = new AdminPanelAuthOptions();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IAdminUserRepository>(this._repository);
        services.AddAdminPanelAuth(configuration);
        services.AddSingleton<IOptions<AdminPanelAuthOptions>>(Options.Create(this._authOptions));
        services.AddAdminApi();
        this._serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Cleans up the services and the stored keys.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        this._serviceProvider.Dispose();
        if (Directory.Exists(this._keyDirectoryPath))
        {
            Directory.Delete(this._keyDirectoryPath, true);
        }
    }

    /// <summary>
    /// Tests that a login without a second factor issues a token which authenticates the user with its roles.
    /// </summary>
    [Test]
    public async Task LoginIssuesTokenWhichAuthenticatesAsync()
    {
        await this.CreateUserAsync("tester", AdminRoles.Operator).ConfigureAwait(false);

        var response = await this.LoginAsync(new("tester", TestPassword)).ConfigureAwait(false);

        Assert.That(response.Status, Is.EqualTo(AdminApiLoginStatus.Succeeded));
        Assert.That(response.Token, Is.Not.Null.And.Not.Empty);
        Assert.That(response.User!.Role, Is.EqualTo(AdminRoles.Operator));

        var result = await this.AuthenticateAsync(response.Token).ConfigureAwait(false);
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Principal!.FindFirstValue(ClaimTypes.Name), Is.EqualTo("tester"));
        Assert.That(result.Principal!.IsInRole(AdminRoles.Viewer), Is.True);
        Assert.That(result.Principal!.IsInRole(AdminRoles.Operator), Is.True);
        Assert.That(result.Principal!.IsInRole(AdminRoles.Administrator), Is.False);
    }

    /// <summary>
    /// Tests that a wrong password doesn't issue a token.
    /// </summary>
    [Test]
    public async Task LoginWithWrongPasswordFailsAsync()
    {
        await this.CreateUserAsync("tester", AdminRoles.Administrator).ConfigureAwait(false);

        var response = await this.LoginAsync(new("tester", "wrong-password-here")).ConfigureAwait(false);

        Assert.That(response.Status, Is.EqualTo(AdminApiLoginStatus.Failed));
        Assert.That(response.Token, Is.Null);
    }

    /// <summary>
    /// Tests that a disabled user can't log in.
    /// </summary>
    [Test]
    public async Task LoginOfDisabledUserFailsAsync()
    {
        var user = await this.CreateUserAsync("tester", AdminRoles.Administrator).ConfigureAwait(false);
        user.IsDisabled = true;
        await this._repository.UpdateAsync(user).ConfigureAwait(false);

        var response = await this.LoginAsync(new("tester", TestPassword)).ConfigureAwait(false);

        Assert.That(response.Status, Is.EqualTo(AdminApiLoginStatus.Failed));
    }

    /// <summary>
    /// Tests that the code of the second factor is asked for, and that a valid code completes the login.
    /// </summary>
    [Test]
    public async Task LoginWithSecondFactorAsksForTheCodeAsync()
    {
        var key = await this.CreateUserWithAuthenticatorAsync("tester").ConfigureAwait(false);

        var withoutCode = await this.LoginAsync(new("tester", TestPassword)).ConfigureAwait(false);
        Assert.That(withoutCode.Status, Is.EqualTo(AdminApiLoginStatus.TwoFactorRequired));
        Assert.That(withoutCode.Token, Is.Null);

        var withCode = await this.LoginAsync(new("tester", TestPassword, TestTotpGenerator.Generate(key))).ConfigureAwait(false);
        Assert.That(withCode.Status, Is.EqualTo(AdminApiLoginStatus.Succeeded));
        Assert.That(withCode.User!.UsedSecondFactor, Is.True);
    }

    /// <summary>
    /// Tests that a wrong code of the second factor doesn't issue a token.
    /// </summary>
    [Test]
    public async Task LoginWithWrongSecondFactorFailsAsync()
    {
        await this.CreateUserWithAuthenticatorAsync("tester").ConfigureAwait(false);

        var response = await this.LoginAsync(new("tester", TestPassword, "000000")).ConfigureAwait(false);

        Assert.That(response.Status, Is.EqualTo(AdminApiLoginStatus.Failed));
        Assert.That(response.Token, Is.Null);
    }

    /// <summary>
    /// Tests that a user without a second factor can't log in when the configuration requires one.
    /// </summary>
    [Test]
    public async Task RequiredSecondFactorWithoutSetupIsRejectedAsync()
    {
        this._authOptions.RequireTwoFactor = true;
        await this.CreateUserAsync("tester", AdminRoles.Administrator).ConfigureAwait(false);

        var response = await this.LoginAsync(new("tester", TestPassword)).ConfigureAwait(false);

        Assert.That(response.Status, Is.EqualTo(AdminApiLoginStatus.TwoFactorSetupRequired));
        Assert.That(response.Token, Is.Null);
    }

    /// <summary>
    /// Tests that a token is rejected after the user was disabled.
    /// </summary>
    [Test]
    public async Task TokenOfDisabledUserIsRejectedAsync()
    {
        var user = await this.CreateUserAsync("tester", AdminRoles.Administrator).ConfigureAwait(false);
        var response = await this.LoginAsync(new("tester", TestPassword)).ConfigureAwait(false);

        user.IsDisabled = true;
        await this._repository.UpdateAsync(user).ConfigureAwait(false);

        var result = await this.AuthenticateAsync(response.Token).ConfigureAwait(false);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.Not.Null);
    }

    /// <summary>
    /// Tests that a token is rejected after a security relevant change of the user, e.g. a new password.
    /// </summary>
    [Test]
    public async Task TokenIsRejectedAfterSecurityStampChangedAsync()
    {
        var user = await this.CreateUserAsync("tester", AdminRoles.Administrator).ConfigureAwait(false);
        var response = await this.LoginAsync(new("tester", TestPassword)).ConfigureAwait(false);

        using (var scope = this._serviceProvider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
            await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        }

        var result = await this.AuthenticateAsync(response.Token).ConfigureAwait(false);
        Assert.That(result.Succeeded, Is.False);
    }

    /// <summary>
    /// Tests that an altered token is rejected and that a request without token has no result.
    /// </summary>
    [Test]
    public async Task InvalidOrMissingTokenIsNotAuthenticatedAsync()
    {
        await this.CreateUserAsync("tester", AdminRoles.Administrator).ConfigureAwait(false);
        var response = await this.LoginAsync(new("tester", TestPassword)).ConfigureAwait(false);

        var altered = await this.AuthenticateAsync(response.Token![..^4] + "AAAA").ConfigureAwait(false);
        Assert.That(altered.Succeeded, Is.False);
        Assert.That(altered.Failure, Is.Not.Null);

        var missing = await this.AuthenticateAsync(null).ConfigureAwait(false);
        Assert.That(missing.None, Is.True);
    }

    /// <summary>
    /// Tests that a viewer passes the viewer policy, but not the operator policy which is required to start and stop servers.
    /// </summary>
    [Test]
    public async Task ViewerIsNotAnOperatorAsync()
    {
        await this.CreateUserAsync("viewer", AdminRoles.Viewer).ConfigureAwait(false);
        var response = await this.LoginAsync(new("viewer", TestPassword)).ConfigureAwait(false);
        var principal = (await this.AuthenticateAsync(response.Token).ConfigureAwait(false)).Principal!;

        var authorizationService = this._serviceProvider.GetRequiredService<IAuthorizationService>();
        var viewer = await authorizationService.AuthorizeAsync(principal, AdminPolicies.Viewer).ConfigureAwait(false);
        var @operator = await authorizationService.AuthorizeAsync(principal, AdminPolicies.Operator).ConfigureAwait(false);

        Assert.That(viewer.Succeeded, Is.True);
        Assert.That(@operator.Succeeded, Is.False);
    }

    /// <summary>
    /// Tests that without any admin user, no login is required and an anonymous request gets all permissions,
    /// like in the initial setup mode of the admin panel - and that this ends as soon as a user exists.
    /// </summary>
    [Test]
    public async Task WithoutUsersNoLoginIsRequiredAsync()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        using (var scope = this._serviceProvider.CreateScope())
        {
            var status = await this.CreateController(scope).GetStatusAsync(
                scope.ServiceProvider.GetRequiredService<AdminUserAvailabilityService>()).ConfigureAwait(false);
            Assert.That(status.LoginRequired, Is.False);

            var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            var result = await authorizationService.AuthorizeAsync(anonymous, AdminPolicies.Operator).ConfigureAwait(false);
            Assert.That(result.Succeeded, Is.True);
        }

        await this.CreateUserAsync("tester", AdminRoles.Administrator).ConfigureAwait(false);

        // The admin panel does this when it creates a user, because the answer is cached.
        this._serviceProvider.GetRequiredService<AdminUserAvailabilityService>().Invalidate();

        using (var scope = this._serviceProvider.CreateScope())
        {
            var status = await this.CreateController(scope).GetStatusAsync(
                scope.ServiceProvider.GetRequiredService<AdminUserAvailabilityService>()).ConfigureAwait(false);
            Assert.That(status.LoginRequired, Is.True);

            var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            var result = await authorizationService.AuthorizeAsync(anonymous, AdminPolicies.Viewer).ConfigureAwait(false);
            Assert.That(result.Succeeded, Is.False);
        }
    }

    private AuthController CreateController(IServiceScope scope)
    {
        return new AuthController(
            scope.ServiceProvider.GetRequiredService<AdminLoginService>(),
            scope.ServiceProvider.GetRequiredService<AdminTokenService>(),
            scope.ServiceProvider.GetRequiredService<IOptions<AdminPanelAuthOptions>>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private async Task<AuthController.LoginResponse> LoginAsync(AuthController.LoginRequest request)
    {
        using var scope = this._serviceProvider.CreateScope();
        var result = await this.CreateController(scope).LoginAsync(request).ConfigureAwait(false);
        return (AuthController.LoginResponse)((ObjectResult)result.Result!).Value!;
    }

    private async Task<AuthenticateResult> AuthenticateAsync(string? token)
    {
        using var scope = this._serviceProvider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        if (token is not null)
        {
            context.Request.Headers[AdminApiDefaults.TokenHeaderName] = token;
        }

        var handler = new AdminTokenAuthenticationHandler(
            scope.ServiceProvider.GetRequiredService<IOptionsMonitor<AuthenticationSchemeOptions>>(),
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>(),
            UrlEncoder.Default,
            scope.ServiceProvider.GetRequiredService<AdminTokenService>(),
            scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>());
        var scheme = new AuthenticationScheme(AdminApiDefaults.AuthenticationScheme, null, typeof(AdminTokenAuthenticationHandler));
        await handler.InitializeAsync(scheme, context).ConfigureAwait(false);
        return await handler.AuthenticateAsync().ConfigureAwait(false);
    }

    private async Task<AdminUser> CreateUserAsync(string loginName, string roles)
    {
        using var scope = this._serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
        var user = new AdminUser
        {
            LoginName = loginName,
            Roles = roles,
        };

        var result = await userManager.CreateAsync(user, TestPassword).ConfigureAwait(false);
        Assert.That(result.Succeeded, Is.True, string.Join(' ', result.Errors.Select(e => e.Description)));
        return user;
    }

    private async Task<string> CreateUserWithAuthenticatorAsync(string loginName)
    {
        var user = await this.CreateUserAsync(loginName, AdminRoles.Administrator).ConfigureAwait(false);
        using var scope = this._serviceProvider.CreateScope();
        var setupService = scope.ServiceProvider.GetRequiredService<AuthenticatorSetupService>();
        var setup = await setupService.BeginSetupAsync(user).ConfigureAwait(false);
        var key = setup.SharedKey.Replace(" ", string.Empty);
        var recoveryCodes = await setupService.ConfirmSetupAsync(user, TestTotpGenerator.Generate(key)).ConfigureAwait(false);
        Assert.That(recoveryCodes, Is.Not.Null, "The generated code should have been accepted.");
        return key;
    }
}
