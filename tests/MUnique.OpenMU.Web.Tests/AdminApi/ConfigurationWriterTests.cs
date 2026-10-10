// <copyright file="ConfigurationWriterTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AdminApi;

using System.Text.Json.Nodes;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;

/// <summary>
/// Tests that the admin API applies the sent values to the configuration objects.
/// </summary>
[TestFixture]
public class ConfigurationWriterTests
{
    private ConfigurationTypeRegistry _registry = null!;
    private ConfigurationValueWriter _writer = null!;
    private IContext _context = null!;

    /// <summary>
    /// Sets up the writer and an in-memory context.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._registry = new ConfigurationTypeRegistry();
        this._writer = new ConfigurationValueWriter(this._registry);
        this._context = new InMemoryPersistenceContextProvider().CreateNewContext();
    }

    /// <summary>
    /// Disposes the context.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
    }

    /// <summary>
    /// Tests that simple values are converted, and that only the sent values are changed.
    /// </summary>
    [Test]
    public async Task SimpleValuesAreAppliedAsync()
    {
        var monster = this._context.CreateNew<MonsterDefinition>();
        monster.Number = 17;
        monster.Designation = new LocalizedString("Cyclops||es=Cíclope");
        monster.NpcWindow = NpcWindow.Merchant;

        await this.ApplyAsync(monster, new JsonObject
        {
            ["Designation"] = "Big Cyclops",
            ["RespawnDelay"] = "00:00:30",
            ["ObjectKind"] = nameof(NpcObjectKind.Guard),
            ["NumberOfMaximumItemDrops"] = 3,
        }).ConfigureAwait(false);

        Assert.That(monster.Designation.Value, Is.EqualTo("Big Cyclops||es=Cíclope"), "The translations are kept.");
        Assert.That(monster.RespawnDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
        Assert.That(monster.ObjectKind, Is.EqualTo(NpcObjectKind.Guard));
        Assert.That(monster.NumberOfMaximumItemDrops, Is.EqualTo(3));
        Assert.That(monster.Number, Is.EqualTo(17), "Values which weren't sent stay.");
        Assert.That(monster.NpcWindow, Is.EqualTo(NpcWindow.Merchant));
    }

    /// <summary>
    /// Tests that all invalid values are reported at once, with their paths.
    /// </summary>
    [Test]
    public void InvalidValuesAreReported()
    {
        var monster = this._context.CreateNew<MonsterDefinition>();

        var exception = Assert.ThrowsAsync<ConfigurationValidationException>(() => this.ApplyAsync(monster, new JsonObject
        {
            ["Number"] = 70000,
            ["ObjectKind"] = "Dragon",
            ["RespawnDelay"] = "soon",
            ["NoSuchProperty"] = 1,
            ["Id"] = Guid.NewGuid().ToString(),
        }));

        Assert.That(exception!.Errors.Select(e => e.Path), Is.EquivalentTo(new[] { "Number", "ObjectKind", "RespawnDelay", "NoSuchProperty", "Id" }));
        Assert.That(exception.Errors.Single(e => e.Path == "Number").Message, Does.Contain("-32768").And.Contain("32767"));
    }

    /// <summary>
    /// Tests that references and lists of references are resolved by their ids.
    /// </summary>
    [Test]
    public async Task ReferencesAreResolvedAsync()
    {
        var poison = this._context.CreateNew<Skill>();
        var firstGroup = this._context.CreateNew<DropItemGroup>();
        var secondGroup = this._context.CreateNew<DropItemGroup>();
        var monster = this._context.CreateNew<MonsterDefinition>();
        monster.DropItemGroups.Add(firstGroup);

        await this.ApplyAsync(monster, new JsonObject
        {
            ["AttackSkill"] = new JsonObject { ["id"] = poison.GetId().ToString() },
            ["DropItemGroups"] = new JsonArray(new JsonObject { ["id"] = secondGroup.GetId().ToString() }),
        }).ConfigureAwait(false);

        Assert.That(monster.AttackSkill, Is.SameAs(poison));
        Assert.That(monster.DropItemGroups, Is.EquivalentTo(new[] { secondGroup }));

        var exception = Assert.ThrowsAsync<ConfigurationValidationException>(() => this.ApplyAsync(monster, new JsonObject
        {
            ["AttackSkill"] = new JsonObject { ["id"] = Guid.NewGuid().ToString() },
        }));
        Assert.That(exception!.Errors.Single().Path, Is.EqualTo("AttackSkill"));
    }

    /// <summary>
    /// Tests that owned objects are matched by their ids: existing ones are changed, new ones
    /// created, and left out ones removed.
    /// </summary>
    [Test]
    public async Task OwnedObjectsAreMatchedByIdAsync()
    {
        var level = this._context.CreateNew<AttributeDefinition>();
        var monster = this._context.CreateNew<MonsterDefinition>();
        var kept = this._context.CreateNew<MonsterAttribute>();
        kept.AttributeDefinition = level;
        kept.Value = 10;
        var removed = this._context.CreateNew<MonsterAttribute>();
        monster.Attributes.Add(kept);
        monster.Attributes.Add(removed);

        await this.ApplyAsync(monster, new JsonObject
        {
            ["Attributes"] = new JsonArray(
                new JsonObject { ["id"] = kept.GetId().ToString(), ["values"] = new JsonObject { ["Value"] = 28 } },
                new JsonObject { ["values"] = new JsonObject { ["AttributeDefinition"] = new JsonObject { ["id"] = level.GetId().ToString() }, ["Value"] = 5 } }),
        }).ConfigureAwait(false);

        Assert.That(monster.Attributes, Has.Count.EqualTo(2));
        Assert.That(monster.Attributes, Does.Contain(kept).And.Not.Contain(removed));
        Assert.That(kept.Value, Is.EqualTo(28));
        Assert.That(kept.AttributeDefinition, Is.SameAs(level), "Values which weren't sent stay.");
        var added = monster.Attributes.Single(a => a != kept);
        Assert.That(added.Value, Is.EqualTo(5));
        Assert.That(added.AttributeDefinition, Is.SameAs(level));
    }

    /// <summary>
    /// Tests that the schema marks the values which can't be changed.
    /// </summary>
    [Test]
    public void SchemaMarksReadOnlyValues()
    {
        var gameConfiguration = this._registry.GetSchema(typeof(GameConfiguration));
        Assert.That(gameConfiguration.Properties.Single(p => p.Name == nameof(GameConfiguration.Items)).IsReadOnly, Is.True, "The items have their own list.");
        Assert.That(gameConfiguration.Properties.Single(p => p.Name == nameof(GameConfiguration.MaximumLevel)).IsReadOnly, Is.False);
        Assert.That(gameConfiguration.CanCreate, Is.False);
        Assert.That(this._registry.GetSchema(typeof(MonsterDefinition)).CanCreate, Is.True);
        Assert.That(this._registry.GetSchema(typeof(MonsterAttribute)).CanCreate, Is.False);
    }

    private Task ApplyAsync(MonsterDefinition monster, JsonObject values) =>
        this._writer.ApplyAsync(monster, typeof(MonsterDefinition), values, this._context);
}
