// <copyright file="CashShopContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.CashShop;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using MUnique.OpenMU.Persistence.CashShop;

/// <summary>
/// The context which holds the points of the cash shop, their changes and the bought items.
/// </summary>
/// <remarks>
/// Like the <see cref="WeeklyQuests.WeeklyQuestContext"/>, it uses an own schema, an own migration history
/// and an own set of migrations. This keeps the generated game data model untouched, so the
/// feature doesn't conflict with changes of the upstream project.
/// </remarks>
public class CashShopContext : DbContext
{
    /// <summary>
    /// Gets or sets the points of the accounts.
    /// </summary>
    public DbSet<CashShopBalance> Balances { get; set; } = null!;

    /// <summary>
    /// Gets or sets the changes of the points.
    /// </summary>
    public DbSet<CashShopTransaction> Transactions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the bought items.
    /// </summary>
    public DbSet<CashShopStorageItem> StorageItems { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        this.Configure(optionsBuilder);

        optionsBuilder.UseNpgsql(
            ConnectionConfigurator.GetConnectionString<CashShopContext>(),
            options => options.MigrationsHistoryTable(HistoryRepository.DefaultTableName, SchemaNames.CashShop));
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(SchemaNames.CashShop);
        modelBuilder.Entity<CashShopBalance>(entity =>
        {
            entity.ToTable("Balance", SchemaNames.CashShop);
            entity.HasKey(b => b.AccountId);
            entity.ToTable(t => t.HasCheckConstraint("CK_Balance_Points", "\"Points\" >= 0"));
        });

        modelBuilder.Entity<CashShopTransaction>(entity =>
        {
            entity.ToTable("Transaction", SchemaNames.CashShop);
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Reason).IsRequired().HasMaxLength(32);
            entity.Property(t => t.Reference).HasMaxLength(128);
            entity.Property(t => t.Comment).HasMaxLength(256);
            entity.HasIndex(t => new { t.AccountId, t.CreatedAt });
            entity.HasIndex(t => new { t.AccountId, t.Reference }).IsUnique().HasFilter("\"Reference\" IS NOT NULL");
        });

        modelBuilder.Entity<CashShopStorageItem>(entity =>
        {
            entity.ToTable("StorageItem", SchemaNames.CashShop);
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(i => new { i.AccountId, i.ConsumedAt });
        });
    }
}
