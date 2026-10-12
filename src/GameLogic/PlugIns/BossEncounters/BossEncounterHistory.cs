// <copyright file="BossEncounterHistory.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.BossEncounters;

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

/// <summary>
/// The history of the finished boss encounters. Each result is a line of JSON in a file, so it survives
/// restarts without a database. The latest results are kept in memory, to read them fast.
/// </summary>
public sealed class BossEncounterHistory
{
    /// <summary>
    /// The maximum number of results which are kept in memory.
    /// </summary>
    public const int MaximumRecentResults = 200;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<BossEncounterResult>? _recentResults;

    /// <summary>
    /// Initializes a new instance of the <see cref="BossEncounterHistory"/> class.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    public BossEncounterHistory(string path)
    {
        this._path = path;
    }

    /// <summary>
    /// Gets the history of the server, in the folder of the server.
    /// </summary>
    public static BossEncounterHistory Shared { get; } = new(Path.Combine(AppContext.BaseDirectory, "boss-encounters", "history.jsonl"));

    /// <summary>
    /// Adds the result of a finished encounter.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="logger">The logger for errors.</param>
    public async ValueTask AddAsync(BossEncounterResult result, ILogger logger)
    {
        await this._lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var results = await this.LoadAsync(logger).ConfigureAwait(false);
            results.Add(result);
            if (results.Count > MaximumRecentResults)
            {
                results.RemoveRange(0, results.Count - MaximumRecentResults);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(this._path)!);
            await File.AppendAllTextAsync(this._path, JsonSerializer.Serialize(result, SerializerOptions) + "\n").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The result of the boss encounter {Name} couldn't be written to {Path}.", result.Name, this._path);
        }
        finally
        {
            this._lock.Release();
        }
    }

    /// <summary>
    /// Gets the latest results, the newest first.
    /// </summary>
    /// <param name="take">The maximum number of results.</param>
    /// <param name="logger">The logger for errors.</param>
    /// <returns>The results.</returns>
    public async ValueTask<IReadOnlyList<BossEncounterResult>> GetRecentAsync(int take, ILogger logger)
    {
        await this._lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var results = await this.LoadAsync(logger).ConfigureAwait(false);
            return results.AsEnumerable().Reverse().Take(Math.Max(0, take)).ToList();
        }
        finally
        {
            this._lock.Release();
        }
    }

    private static BossEncounterResult? TryDeserialize(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<BossEncounterResult>(line, SerializerOptions);
        }
        catch (JsonException)
        {
            // A broken line, e.g. after a crash while writing, is skipped.
            return null;
        }
    }

    /// <summary>
    /// Loads the latest results of the file, once.
    /// </summary>
    private async ValueTask<List<BossEncounterResult>> LoadAsync(ILogger logger)
    {
        if (this._recentResults is { } loaded)
        {
            return loaded;
        }

        var results = new List<BossEncounterResult>();
        this._recentResults = results;
        if (!File.Exists(this._path))
        {
            return results;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(this._path).ConfigureAwait(false);
            foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)).TakeLast(MaximumRecentResults))
            {
                if (TryDeserialize(line) is { } result)
                {
                    results.Add(result);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The history of the boss encounters {Path} couldn't be read.", this._path);
        }

        return results;
    }
}
