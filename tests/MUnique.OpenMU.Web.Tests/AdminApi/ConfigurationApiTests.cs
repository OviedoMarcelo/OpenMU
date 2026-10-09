// <copyright file="ConfigurationApiTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AdminApi;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Web.AdminPanel.AdminApi.Configuration;
using BasicModel = MUnique.OpenMU.Persistence.BasicModel;

/// <summary>
/// Tests the description and serialization of the configuration by the admin API.
/// </summary>
[TestFixture]
public class ConfigurationApiTests
{
    private ConfigurationTypeRegistry _registry = null!;
    private ConfigurationValueSerializer _serializer = null!;

    /// <summary>
    /// Sets up the registry and the serializer.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._registry = new ConfigurationTypeRegistry();
        this._serializer = new ConfigurationValueSerializer(this._registry, NullLogger<ConfigurationValueSerializer>.Instance);
    }

    /// <summary>
    /// Tests that the kinds of the properties follow the rules of the admin panel forms:
    /// a <see cref="DataModel.Composition.MemberOfAggregateAttribute"/> marks owned objects, everything else is a reference.
    /// </summary>
    [Test]
    public void PropertyKindsFollowTheAggregateRules()
    {
        var schema = this._registry.GetSchema(typeof(MonsterDefinition));
        PropertySchema Property(string name) => schema.Properties.Single(p => p.Name == name);

        Assert.That(Property(nameof(MonsterDefinition.Number)).Kind, Is.EqualTo(PropertyKind.Integer));
        Assert.That(Property(nameof(MonsterDefinition.Designation)).Kind, Is.EqualTo(PropertyKind.LocalizedText));
        Assert.That(Property(nameof(MonsterDefinition.RespawnDelay)).Kind, Is.EqualTo(PropertyKind.TimeSpan));
        Assert.That(Property(nameof(MonsterDefinition.ObjectKind)).Kind, Is.EqualTo(PropertyKind.Enum));
        Assert.That(Property(nameof(MonsterDefinition.ObjectKind)).EnumValues, Is.Not.Empty);

        var attackSkill = Property(nameof(MonsterDefinition.AttackSkill));
        Assert.That(attackSkill.Kind, Is.EqualTo(PropertyKind.Reference));
        Assert.That(attackSkill.TargetType, Is.EqualTo(nameof(Skill)));
        Assert.That(attackSkill.TargetIsBrowsable, Is.True);

        Assert.That(Property(nameof(MonsterDefinition.DropItemGroups)).Kind, Is.EqualTo(PropertyKind.ReferenceList));
        Assert.That(Property(nameof(MonsterDefinition.Attributes)).Kind, Is.EqualTo(PropertyKind.EmbeddedList));
        Assert.That(Property(nameof(MonsterDefinition.MerchantStore)).Kind, Is.EqualTo(PropertyKind.Embedded));

        Assert.That(schema.NameProperty, Is.EqualTo(nameof(MonsterDefinition.Designation)));
        Assert.That(schema.Properties.Select(p => p.Name), Has.None.StartsWith("Raw").And.None.StartsWith("Joined"));
    }

    /// <summary>
    /// Tests that the collections of the game configuration have their own lists, except the plugin
    /// configurations, which have their own page.
    /// </summary>
    [Test]
    public void CollectionsOfTheGameConfigurationAreBrowsable()
    {
        Assert.That(this._registry.IsBrowsable(typeof(ItemDefinition)), Is.True);
        Assert.That(this._registry.IsBrowsable(typeof(MonsterDefinition)), Is.True);
        Assert.That(this._registry.IsBrowsable(typeof(GameClientDefinition)), Is.True, "It's in the menu, although it's not part of the game configuration.");
        Assert.That(this._registry.IsBrowsable(typeof(PlugInConfiguration)), Is.False);
        Assert.That(
            this._registry.GetSchema(typeof(GameConfiguration)).Properties.Select(p => p.Name),
            Has.No.Member(nameof(GameConfiguration.PlugInConfigurations)));

        Assert.That(this._registry.GetType(nameof(MonsterAttribute)), Is.EqualTo(typeof(MonsterAttribute)), "Embedded types are known, too.");
        Assert.That(this._registry.GetType("monsterdefinition"), Is.EqualTo(typeof(MonsterDefinition)));
        Assert.That(this._registry.GetType("NoSuchType"), Is.Null);
    }

    /// <summary>
    /// Tests that the configured list columns exist and are used for the rows.
    /// </summary>
    [Test]
    public void RowsContainTheListColumns()
    {
        var monster = new BasicModel.MonsterDefinition { Number = 17, Designation = "Cyclops", RespawnDelay = TimeSpan.FromSeconds(10) };

        var row = this._serializer.SerializeRow(monster, typeof(MonsterDefinition));

        Assert.That(row["name"]!.GetValue<string>(), Is.EqualTo("Cyclops"));
        var values = row["values"]!.AsObject();
        Assert.That(values.Select(v => v.Key), Is.EquivalentTo(this._registry.GetSchema(typeof(MonsterDefinition)).ListColumns));
        Assert.That(values["Number"]!.GetValue<short>(), Is.EqualTo(17));
        Assert.That(values["RespawnDelay"]!.GetValue<string>(), Is.EqualTo("00:00:10"));
    }

    /// <summary>
    /// Tests that references are serialized as such, and owned objects completely.
    /// </summary>
    [Test]
    public void ReferencesAndOwnedObjectsAreSerialized()
    {
        var skill = new BasicModel.Skill { Number = 1, Name = "Poison" };
        var attribute = new BasicModel.AttributeDefinition(Guid.NewGuid(), "Level", string.Empty);
        var monster = new BasicModel.MonsterDefinition { Number = 17, Designation = "Cyclops", AttackSkill = skill };
        monster.Attributes.Add(new BasicModel.MonsterAttribute { AttributeDefinition = attribute, Value = 28 });

        var values = this._serializer.SerializeObject(monster, typeof(MonsterDefinition))["values"]!.AsObject();

        var reference = values["AttackSkill"]!.AsObject();
        Assert.That(reference["name"]!.GetValue<string>(), Is.EqualTo("Poison"));
        Assert.That(reference["type"]!.GetValue<string>(), Is.EqualTo(nameof(Skill)));
        Assert.That(reference.ContainsKey("values"), Is.False);

        var ownedAttribute = values["Attributes"]!.AsArray().Single()!.AsObject();
        Assert.That(ownedAttribute["values"]!["Value"]!.GetValue<float>(), Is.EqualTo(28));
        Assert.That(ownedAttribute["values"]!["AttributeDefinition"]!["name"]!.GetValue<string>(), Is.EqualTo("Level"));

        Assert.That(values["DropItemGroups"], Is.InstanceOf<JsonArray>());
        Assert.That(values["MerchantStore"], Is.Null);
    }

    /// <summary>
    /// Tests that the collections of the game configuration are only counted, because they have their own lists.
    /// </summary>
    [Test]
    public void CollectionsWithOwnListsAreOnlyCounted()
    {
        var configuration = new BasicModel.GameConfiguration { MaximumLevel = 400 };
        configuration.Items.Add(new BasicModel.ItemDefinition { Name = "Kris" });
        configuration.Items.Add(new BasicModel.ItemDefinition { Name = "Short Sword" });

        var values = this._serializer.SerializeObject(configuration, typeof(GameConfiguration))["values"]!.AsObject();

        Assert.That(values["MaximumLevel"]!.GetValue<short>(), Is.EqualTo(400));
        Assert.That(values["Items"]!["count"]!.GetValue<int>(), Is.EqualTo(2));
    }
}
