// <copyright file="AdminTokenService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi;

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

/// <summary>
/// Issues and reads the session tokens of the admin API.
/// </summary>
/// <remarks>
/// A token is a payload protected by the data protection key ring of the admin panel, so it can't be
/// forged or altered. It contains the security stamp of the user: a password change, a changed role or
/// a disabled user invalidates all issued tokens, because the stamp is compared on every request.
/// </remarks>
public class AdminTokenService
{
    /// <summary>
    /// The lifetime of an issued token.
    /// </summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

    private readonly ITimeLimitedDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminTokenService"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    public AdminTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        this._protector = dataProtectionProvider
            .CreateProtector("MUnique.OpenMU.AdminApi.Token")
            .ToTimeLimitedDataProtector();
    }

    /// <summary>
    /// Issues a new token.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <returns>The token.</returns>
    public string Issue(AdminTokenPayload payload)
    {
        return this._protector.Protect(JsonSerializer.Serialize(payload), TokenLifetime);
    }

    /// <summary>
    /// Tries to read the payload of the specified token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="payload">The payload, if the token is valid and not expired.</param>
    /// <returns><c>true</c>, if the token is valid and not expired.</returns>
    public bool TryRead(string token, out AdminTokenPayload? payload)
    {
        payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<AdminTokenPayload>(this._protector.Unprotect(token));
            return payload is not null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return false;
        }
    }
}
