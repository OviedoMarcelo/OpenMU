// <copyright file="AddCashShopExampleCatalogPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.CashShop;
using MUnique.OpenMU.GameLogic.PlugIns.Cosmetics;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Gives the cash shop of existing databases the example catalog of <see cref="CashShopExampleCatalog"/>,
/// when it has no products yet. New databases get it from the initialization.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("5D3A9C1E-7B42-4E8F-A6D0-2C9B8E1F4A73")]
public class AddCashShopExampleCatalogPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add the example catalog of the cash shop";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Fills the cash shop (if it has no products yet) with an example: per class the best weapon and armor set without sockets, packs of jewels, boxes and skins. The plugin stays inactive until it's activated.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Determines whether a serialized configuration has products. The products reference items, so the
    /// configuration isn't deserialized here.
    /// </summary>
    /// <param name="json">The serialized <see cref="CashShopConfiguration"/>.</param>
    /// <returns><c>true</c>, if it has at least one product.</returns>
    public static bool HasProducts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || JsonNode.Parse(json) is not JsonObject configuration)
        {
            return false;
        }

        // With preserved references, a list is an object with its elements in "$values".
        var products = configuration[nameof(CashShopConfiguration.Products)];
        var list = products as JsonArray ?? (products as JsonObject)?["$values"] as JsonArray;
        return list is { Count: > 0 };
    }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var plugInId = typeof(CashShopPlugIn).GUID;
        var plugInConfiguration = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == plugInId);
        if (plugInConfiguration is null)
        {
            plugInConfiguration = context.CreateNew<PlugInConfiguration>();
            plugInConfiguration.SetGuid(plugInId);
            plugInConfiguration.TypeId = plugInId;
            plugInConfiguration.IsActive = false;
            gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
        }

        if (HasProducts(plugInConfiguration.CustomConfiguration))
        {
            // The products were configured already, and we don't overwrite them.
            return ValueTask.CompletedTask;
        }

        // The cosmetic pieces only have numbers, so they're read without resolving references.
        var cosmetics = gameConfiguration.PlugInConfigurations
            .FirstOrDefault(c => c.TypeId == typeof(CosmeticsPlugIn).GUID)
            ?.GetConfiguration<CosmeticsConfiguration>(null);
        plugInConfiguration.SetConfiguration(CashShopExampleCatalog.Create(gameConfiguration, cosmetics), new ExistingObjectsReferenceHandler());
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Writes the references to the objects of the game configuration (e.g. the items) by their id, like the
    /// reference handler of the plugin manager reads them.
    /// </summary>
    private sealed class ExistingObjectsReferenceHandler : ReferenceHandler
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
}
