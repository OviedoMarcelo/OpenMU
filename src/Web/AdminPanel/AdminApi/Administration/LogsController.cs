// <copyright file="LogsController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AdminApi.Administration;

using System.IO;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Shows the log files of the server, like the log files page of the admin panel.
/// </summary>
[ApiController]
[Route(AdminApiDefaults.RoutePrefix + "/logs")]
[Authorize(AuthenticationSchemes = AdminApiDefaults.AuthenticationScheme, Policy = AdminPolicies.Administrator)]
public class LogsController : ControllerBase
{
    private const int MaximumLines = 2000;

    /// <summary>
    /// The end of a file which is read for its last lines; older lines are cut off.
    /// </summary>
    private const int MaximumBytes = 1024 * 1024;

    private static string LogsPath => Path.Combine(Directory.GetCurrentDirectory(), "logs");

    /// <summary>
    /// Gets the log files, the newest first.
    /// </summary>
    /// <returns>The files.</returns>
    [HttpGet]
    public IEnumerable<LogFile> GetFiles()
    {
        if (!Directory.Exists(LogsPath))
        {
            return [];
        }

        return new DirectoryInfo(LogsPath).GetFiles()
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new LogFile(f.Name, f.Length, f.LastWriteTimeUtc))
            .ToList();
    }

    /// <summary>
    /// Gets the last lines of a log file.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="lines">The maximum number of lines.</param>
    /// <returns>The lines, the oldest first.</returns>
    [HttpGet("{name}")]
    public async Task<ActionResult<LogContent>> GetLinesAsync(string name, [FromQuery] int lines = 500)
    {
        if (FindFile(name) is not { } file)
        {
            return this.NotFound();
        }

        // The logger keeps the file open, so it's read with sharing.
        await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var start = Math.Max(0, stream.Length - MaximumBytes);
        stream.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = await reader.ReadToEndAsync(this.HttpContext.RequestAborted).ConfigureAwait(false);
        var allLines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (start > 0 && allLines.Count > 0)
        {
            // The first line was cut in the middle.
            allLines.RemoveAt(0);
        }

        if (allLines.Count > 0 && allLines[^1].Length == 0)
        {
            allLines.RemoveAt(allLines.Count - 1);
        }

        return new LogContent(file.Name, file.Length, file.LastWriteTimeUtc, allLines.TakeLast(Math.Clamp(lines, 1, MaximumLines)).ToList());
    }

    /// <summary>
    /// Downloads a log file.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <returns>The file.</returns>
    [HttpGet("{name}/download")]
    public IActionResult Download(string name)
    {
        if (FindFile(name) is not { } file)
        {
            return this.NotFound();
        }

        var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return this.File(stream, "text/plain", file.Name);
    }

    /// <summary>
    /// Finds a file of the logs folder; names with paths are rejected, so no other file can be read.
    /// </summary>
    private static FileInfo? FindFile(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name)
        {
            return null;
        }

        var file = new FileInfo(Path.Combine(LogsPath, name));
        return file.Exists && file.DirectoryName == new DirectoryInfo(LogsPath).FullName ? file : null;
    }

    /// <summary>
    /// A log file.
    /// </summary>
    /// <param name="Name">The name.</param>
    /// <param name="Size">The size in bytes.</param>
    /// <param name="LastWrite">When it was written the last time.</param>
    public record LogFile(string Name, long Size, DateTime LastWrite);

    /// <summary>
    /// The last lines of a log file.
    /// </summary>
    /// <param name="Name">The name.</param>
    /// <param name="Size">The size in bytes.</param>
    /// <param name="LastWrite">When it was written the last time.</param>
    /// <param name="Lines">The lines, the oldest first.</param>
    public record LogContent(string Name, long Size, DateTime LastWrite, IReadOnlyList<string> Lines);
}
