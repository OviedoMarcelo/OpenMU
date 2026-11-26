// <copyright file="ExistingObjectsReferenceHandler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Text.Json.Serialization;

/// <summary>
/// Writes the references to the objects of the game configuration (e.g. the items) by their id, like the
/// reference handler of the plugin manager reads them.
/// </summary>
internal sealed class ExistingObjectsReferenceHandler : ReferenceHandler
{
    public override ReferenceResolver CreateResolver() => new Resolver();

    private sealed class Resolver : ReferenceResolver
    {
        public override void AddReference(string referenceId, object value)
        {
        }

        public override string GetReference(object value, out bool alreadyExists)
        {
            if (value is IIdentifiable identifiable)
            {
                alreadyExists = true;
                return identifiable.Id.ToString();
            }

            alreadyExists = false;
            return string.Empty;
        }

        public override object ResolveReference(string referenceId) => throw new NotSupportedException();
    }
}
