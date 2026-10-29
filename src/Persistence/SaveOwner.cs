// <copyright file="SaveOwner.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

using System.Threading;

/// <summary>
/// Describes, for the current asynchronous flow, on whose behalf a context is being saved
/// (e.g. the account and character of a player), so that the persistence layer can name it
/// in its log entries. A context doesn't know whom its entities belong to, and a save
/// conflict which doesn't say which player is affected can't be investigated.
/// </summary>
public static class SaveOwner
{
    private static readonly AsyncLocal<string?> CurrentOwner = new();

    /// <summary>
    /// Gets the description of the owner of the current save, if any.
    /// </summary>
    public static string? Current => CurrentOwner.Value;

    /// <summary>
    /// Sets the owner of the saves of the current asynchronous flow, until the returned object is disposed.
    /// </summary>
    /// <param name="owner">The description of the owner.</param>
    /// <returns>The disposable which restores the previous owner.</returns>
    public static IDisposable Begin(string owner)
    {
        var previous = CurrentOwner.Value;
        CurrentOwner.Value = owner;
        return new Restorer(previous);
    }

    private sealed class Restorer : IDisposable
    {
        private readonly string? _previous;

        public Restorer(string? previous)
        {
            this._previous = previous;
        }

        public void Dispose()
        {
            CurrentOwner.Value = this._previous;
        }
    }
}
