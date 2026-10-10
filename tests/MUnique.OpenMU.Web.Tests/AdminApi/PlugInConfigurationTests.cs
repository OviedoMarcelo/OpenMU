// <copyright file="PlugInConfigurationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AdminApi;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.GameLogic.PlugIns.Achievements;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

/// <summary>
/// Tests that the custom configurations of plugins, which are plain objects, are described and
/// changed like the configuration objects.
/// </summary>
[TestFixture]
public class PlugInConfigurationTests
{
    private ConfigurationTypeRegistry _registry = null!;

    /// <summary>
    /// Sets up the registry.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        // The plugin types are found in the loaded assemblies.
        _ = typeof(AchievementsConfiguration).Assembly;
        this._registry = new ConfigurationTypeRegistry();
    }

    /// <summary>
    /// Tests that the objects of a plugin configuration are owned by it, and that a text id can be changed.
    /// </summary>
    [Test]
    public void PlainObjectsAreOwned()
    {
        var schema = this._registry.GetSchema(typeof(AchievementsConfiguration));
        var titles = schema.Properties.Single(p => p.Name == nameof(AchievementsConfiguration.Titles));
        Assert.That(titles.Kind, Is.EqualTo(PropertyKind.EmbeddedList));
        Assert.That(titles.IsReadOnly, Is.False);

        var titleId = this._registry.GetSchema(typeof(TitleDefinition)).Properties.Single(p => p.Name == nameof(TitleDefinition.Id));
        Assert.That(titleId.IsReadOnly, Is.False, "Only ids of entities can't be changed.");
    }

    /// <summary>
    /// Tests that the objects of the lists of a plain object are matched by their position, since they have no ids.
    /// </summary>
    [Test]
    public async Task ListsOfPlainObjectsAreMatchedByPositionAsync()
    {
        var configuration = new AchievementsConfiguration { MaximumExperienceBonusPercent = 10 };
        configuration.Titles.Clear();
        configuration.Titles.Add(new TitleDefinition { Id = "hero", Text = "Hero", Color = "#FF0000" });
        configuration.Titles.Add(new TitleDefinition { Id = "legend", Text = "Legend" });
        var writer = new ConfigurationValueWriter(this._registry);
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();

        await writer.ApplyAsync(
            configuration,
            typeof(AchievementsConfiguration),
            new JsonObject
            {
                ["MaximumExperienceBonusPercent"] = 15,
                ["Titles"] = new JsonArray(
                    new JsonObject { ["values"] = new JsonObject { ["Text"] = "Héroe" } },
                    new JsonObject { ["values"] = new JsonObject { ["Id"] = "new", ["Text"] = "Nuevo" } },
                    new JsonObject { ["values"] = new JsonObject { ["Id"] = "third", ["Text"] = "Tercero" } }),
            },
            context,
            plainObjects: true).ConfigureAwait(false);

        Assert.That(configuration.MaximumExperienceBonusPercent, Is.EqualTo(15));
        var titles = configuration.Titles.ToList();
        Assert.That(titles.Select(t => t.Id), Is.EqualTo(new[] { "hero", "new", "third" }));
        Assert.That(titles[0].Text, Is.EqualTo("Héroe"));
        Assert.That(titles[0].Color, Is.EqualTo("#FF0000"), "Values which weren't sent stay.");

        var serializer = new ConfigurationValueSerializer(this._registry, NullLogger<ConfigurationValueSerializer>.Instance);
        var values = serializer.SerializeObject(configuration, typeof(AchievementsConfiguration))["values"]!.AsObject();
        Assert.That(values["Titles"]!.AsArray(), Has.Count.EqualTo(3));
    }
}
