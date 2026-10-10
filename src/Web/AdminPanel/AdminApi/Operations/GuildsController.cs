// <copyright file="GuildsController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Operations;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using MUnique.OpenMU.Web.Shared.Models;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// Shows the guilds with their members and alliances, like the guild pages of the admin panel.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/guilds")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Viewer)]
public class GuildsController : ControllerBase
{
    private const int MaximumCount = 200;

    private readonly IGuildService _guildService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildsController"/> class.
    /// </summary>
    /// <param name="guildService">The guild service.</param>
    public GuildsController(IGuildService guildService)
    {
        this._guildService = guildService;
    }

    /// <summary>
    /// Gets a page of the guilds, ordered by their name.
    /// </summary>
    /// <param name="q">A text which the name contains.</param>
    /// <param name="offset">The number of guilds to skip.</param>
    /// <param name="count">The maximum number of guilds.</param>
    /// <returns>The guilds.</returns>
    [HttpGet]
    public async Task<GuildPage> GetGuildsAsync([FromQuery] string? q = null, [FromQuery] int offset = 0, [FromQuery] int count = 50)
    {
        count = Math.Clamp(count, 1, MaximumCount);
        this._guildService.SearchFilter = q ?? string.Empty;
        var guilds = await this._guildService.GetAsync(Math.Max(0, offset), count + 1).ConfigureAwait(false);
        return new GuildPage(guilds.Take(count).Select(ToDto).ToList(), guilds.Count > count);
    }

    /// <summary>
    /// Gets a guild with its members and its alliance.
    /// </summary>
    /// <param name="id">The id of the guild.</param>
    /// <returns>The guild.</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GuildDetails>> GetGuildAsync(Guid id)
    {
        if (await this._guildService.GetGuildAsync(id).ConfigureAwait(false) is not { } guild)
        {
            return this.NotFound();
        }

        var members = await this._guildService.GetGuildMembersAsync(id).ConfigureAwait(false);
        var alliance = guild.AllianceGuildId is not null ? await this._guildService.GetAllianceAsync(id).ConfigureAwait(false) : null;
        return new GuildDetails(
            ToDto(guild),
            members.Select(m => new GuildMember(m.CharacterId, m.CharacterName, m.AccountId, m.AccountLoginName, m.CharacterClass, m.Level, m.MasterLevel, m.Position.ToString())).ToList(),
            alliance?.Guilds.Select(ToDto).ToList() ?? []);
    }

    private static GuildDto ToDto(GuildListItem guild) => new(
        guild.Id,
        guild.Name,
        guild.Score,
        guild.Notice,
        guild.MemberCount,
        guild.AllianceGuildId,
        guild.AllianceName,
        guild.Logo is { Length: > 0 } logo ? Convert.ToHexString(logo) : null);

    /// <summary>
    /// A page of guilds.
    /// </summary>
    /// <param name="Items">The guilds.</param>
    /// <param name="HasMore">If set to <c>true</c>, there are more guilds after this page.</param>
    public record GuildPage(IReadOnlyList<GuildDto> Items, bool HasMore);

    /// <summary>
    /// A guild.
    /// </summary>
    /// <param name="Id">The id.</param>
    /// <param name="Name">The name.</param>
    /// <param name="Score">The score.</param>
    /// <param name="Notice">The notice.</param>
    /// <param name="MemberCount">The number of members.</param>
    /// <param name="AllianceGuildId">The id of the master guild of its alliance, if any.</param>
    /// <param name="AllianceName">The name of the master guild of its alliance, if any.</param>
    /// <param name="Logo">The logo: 64 color indexes (8x8) of one nibble each, as hex.</param>
    public record GuildDto(Guid Id, string Name, int Score, string? Notice, int MemberCount, Guid? AllianceGuildId, string? AllianceName, string? Logo);

    /// <summary>
    /// A member of a guild.
    /// </summary>
    /// <param name="CharacterId">The id of the character.</param>
    /// <param name="CharacterName">The name of the character.</param>
    /// <param name="AccountId">The id of the account of the character.</param>
    /// <param name="AccountLoginName">The login name of the account.</param>
    /// <param name="CharacterClass">The class of the character.</param>
    /// <param name="Level">The level.</param>
    /// <param name="MasterLevel">The master level.</param>
    /// <param name="Position">The position in the guild, e.g. <c>GuildMaster</c>.</param>
    public record GuildMember(Guid CharacterId, string CharacterName, Guid? AccountId, string? AccountLoginName, string? CharacterClass, int Level, int MasterLevel, string Position);

    /// <summary>
    /// A guild with its members and alliance.
    /// </summary>
    /// <param name="Guild">The guild.</param>
    /// <param name="Members">The members, the guild master first.</param>
    /// <param name="Alliance">The guilds of its alliance, including itself; empty if it isn't in one.</param>
    public record GuildDetails(GuildDto Guild, IReadOnlyList<GuildMember> Members, IReadOnlyList<GuildDto> Alliance);
}
