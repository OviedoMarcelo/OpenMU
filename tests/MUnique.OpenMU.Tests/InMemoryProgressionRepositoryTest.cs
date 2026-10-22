// <copyright file="InMemoryProgressionRepositoryTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// Tests for the <see cref="InMemoryProgressionRepository"/>.
/// </summary>
[TestFixture]
public class InMemoryProgressionRepositoryTest
{
    private const byte HelmSlot = 2;
    private const byte ArmorSlot = 3;

    /// <summary>
    /// Tests that removing cosmetic pieces from all characters removes the unlocked and worn pieces with these
    /// numbers, and keeps the others.
    /// </summary>
    [Test]
    public async Task RemovesCosmeticPiecesOfAllCharactersAsync()
    {
        var repository = new InMemoryProgressionRepository();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        foreach (var (character, piece) in new[] { (first, (short)541), (first, (short)11), (second, (short)542) })
        {
            await repository.AddUnlockedCosmeticPieceAsync(new UnlockedCosmeticPiece { CharacterId = character, PieceNumber = piece, UnlockedAt = DateTime.UtcNow }).ConfigureAwait(false);
        }

        await repository.SetWornCosmeticPieceAsync(first, HelmSlot, 541).ConfigureAwait(false);
        await repository.SetWornCosmeticPieceAsync(first, ArmorSlot, 11).ConfigureAwait(false);

        var removed = await repository.RemoveCosmeticPiecesOfAllCharactersAsync([541, 542]).ConfigureAwait(false);

        Assert.That(removed, Is.EqualTo(2));
        Assert.That((await repository.LoadUnlockedCosmeticPiecesAsync(first).ConfigureAwait(false)).Select(p => p.PieceNumber), Is.EqualTo(new short[] { 11 }));
        Assert.That(await repository.LoadUnlockedCosmeticPiecesAsync(second).ConfigureAwait(false), Is.Empty);
        Assert.That((await repository.LoadWornCosmeticPiecesAsync(first).ConfigureAwait(false)).Select(p => p.PieceNumber), Is.EqualTo(new short[] { 11 }));
    }
}
