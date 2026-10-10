// <copyright file="AdminAuditLog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Audit;

using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// Records the changes which admins make through the admin API, so they can be reviewed later.
/// </summary>
/// <remarks>
/// The entries are appended to a file with one JSON object per line, which survives restarts when
/// it's on a volume (see the docker compose file). The latest entries are also kept in memory.
/// </remarks>
public sealed class AdminAuditLog : IDisposable
{
    /// <summary>
    /// The configuration key of the path of the file.
    /// </summary>
    public const string PathConfigurationKey = "AdminApi:AuditLogPath";

    private const int MaximumEntriesInMemory = 1000;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string _path;
    private readonly ILogger<AdminAuditLog> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private LinkedList<AuditEntry>? _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminAuditLog"/> class.
    /// </summary>
    /// <param name="path">The path of the file; it's created when the first entry is added.</param>
    /// <param name="logger">The logger.</param>
    public AdminAuditLog(string path, ILogger<AdminAuditLog> logger)
    {
        this._path = path;
        this._logger = logger;
    }

    /// <summary>
    /// Gets the default path of the file, next to the application.
    /// </summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "admin-audit", "audit.jsonl");

    /// <summary>
    /// Adds an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The task.</returns>
    public async Task AddAsync(AuditEntry entry)
    {
        await this._lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var entries = await this.GetEntriesAsync().ConfigureAwait(false);
            entries.AddFirst(entry);
            if (entries.Count > MaximumEntriesInMemory)
            {
                entries.RemoveLast();
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(this._path)!);
                await File.AppendAllTextAsync(this._path, JsonSerializer.Serialize(entry, SerializerOptions) + "\n").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The change itself was saved, so a failure here must not fail the request.
                this._logger.LogError(ex, "The audit entry couldn't be written to {Path}: {Entry}", this._path, entry);
            }
        }
        finally
        {
            this._lock.Release();
        }
    }

    /// <summary>
    /// Gets the latest entries, newest first.
    /// </summary>
    /// <param name="limit">The maximum number of entries.</param>
    /// <param name="objectId">The id of an object, to only get its entries.</param>
    /// <returns>The entries.</returns>
    public async Task<IReadOnlyList<AuditEntry>> GetLatestAsync(int limit, Guid? objectId = null)
    {
        await this._lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var entries = await this.GetEntriesAsync().ConfigureAwait(false);
            return entries.Where(e => objectId is null || e.ObjectId == objectId).Take(limit).ToList();
        }
        finally
        {
            this._lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this._lock.Dispose();
    }

    private async Task<LinkedList<AuditEntry>> GetEntriesAsync()
    {
        if (this._entries is { } entries)
        {
            return entries;
        }

        entries = new LinkedList<AuditEntry>();
        if (File.Exists(this._path))
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(this._path).ConfigureAwait(false);
                foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)).TakeLast(MaximumEntriesInMemory))
                {
                    if (JsonSerializer.Deserialize<AuditEntry>(line, SerializerOptions) is { } entry)
                    {
                        entries.AddFirst(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "The audit log {Path} couldn't be read.", this._path);
            }
        }

        this._entries = entries;
        return entries;
    }
}
