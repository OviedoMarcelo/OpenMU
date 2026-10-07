// <copyright file="SeasonPassState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// The stored state of the pass of a season for one account.
/// </summary>
/// <param name="Experience">The experience of the pass.</param>
/// <param name="Claims">The rewards which have been handed out.</param>
/// <param name="IsPremium">If set to <c>true</c>, the premium track is active.</param>
public sealed record SeasonPassState(long Experience, IReadOnlyCollection<SeasonClaim> Claims, bool IsPremium);
