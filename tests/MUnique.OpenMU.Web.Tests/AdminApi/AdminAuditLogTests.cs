// <copyright file="AdminAuditLogTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AdminApi;

using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;

/// <summary>
/// Tests the <see cref="AdminAuditLog"/>.
/// </summary>
[TestFixture]
public class AdminAuditLogTests
{
    /// <summary>
    /// Tests that the entries are kept in the file, so they're still there after a restart.
    /// </summary>
    [Test]
    public async Task EntriesSurviveARestartAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid()}", "audit.jsonl");
        var objectId = Guid.NewGuid();
        try
        {
            using (var log = new AdminAuditLog(path, NullLogger<AdminAuditLog>.Instance))
            {
                await log.AddAsync(CreateEntry(objectId, AuditAction.Created)).ConfigureAwait(false);
                await log.AddAsync(CreateEntry(Guid.NewGuid(), AuditAction.Updated)).ConfigureAwait(false);
                await log.AddAsync(CreateEntry(objectId, AuditAction.Updated)).ConfigureAwait(false);
            }

            using var restarted = new AdminAuditLog(path, NullLogger<AdminAuditLog>.Instance);
            var latest = await restarted.GetLatestAsync(10).ConfigureAwait(false);
            Assert.That(latest, Has.Count.EqualTo(3));
            Assert.That(latest[0].ObjectId, Is.EqualTo(objectId), "The newest entry comes first.");
            Assert.That(latest[0].Changes.Single().After, Is.EqualTo("28"));

            var ofObject = await restarted.GetLatestAsync(10, objectId).ConfigureAwait(false);
            Assert.That(ofObject.Select(e => e.Action), Is.EqualTo(new[] { AuditAction.Updated, AuditAction.Created }));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    private static AuditEntry CreateEntry(Guid objectId, AuditAction action) => new(
        DateTimeOffset.UtcNow,
        "admin",
        action,
        "MonsterDefinition",
        "Monstruos",
        objectId,
        "Cyclops",
        [new AuditChange("Level", "Nivel", "10", "28")]);
}
