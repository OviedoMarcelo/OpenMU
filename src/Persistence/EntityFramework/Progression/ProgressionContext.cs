// <copyright file="ProgressionContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Progression;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using MUnique.OpenMU.Persistence.Progression;

/// <summary>
/// The context which holds the long term progression of characters and accounts: achievements and titles.
/// </summary>
/// <remarks>
/// Like the <see cref="WeeklyQuests.WeeklyQuestContext"/>, it uses an own schema, an own migration history
/// and an own set of migrations. This keeps the generated game data model untouched, so the
/// feature doesn't conflict with changes of the upstream project.
/// </remarks>
public class ProgressionContext : DbContext
{
    /// <summary>
    /// Gets or sets the achievement progress entries.
    /// </summary>
    public DbSet<AchievementProgress> Achievements { get; set; } = null!;

    /// <summary>
    /// Gets or sets the unlocked titles.
    /// </summary>
    public DbSet<UnlockedTitle> UnlockedTitles { get; set; } = null!;

    /// <summary>
    /// Gets or sets the active titles.
    /// </summary>
    public DbSet<ActiveTitle> ActiveTitles { get; set; } = null!;

    /// <summary>
    /// Gets or sets the experience of the season passes.
    /// </summary>
    public DbSet<SeasonProgress> SeasonProgress { get; set; } = null!;

    /// <summary>
    /// Gets or sets the rewards of the season passes which have been handed out.
    /// </summary>
    public DbSet<SeasonClaim> SeasonClaims { get; set; } = null!;

    /// <summary>
    /// Gets or sets the activated premium tracks of the season passes.
    /// </summary>
    public DbSet<SeasonPremium> SeasonPremiums { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        this.Configure(optionsBuilder);

        optionsBuilder.UseNpgsql(
            ConnectionConfigurator.GetConnectionString<ProgressionContext>(),
            options => options.MigrationsHistoryTable(HistoryRepository.DefaultTableName, SchemaNames.Progression));
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(SchemaNames.Progression);
        modelBuilder.Entity<AchievementProgress>(entity =>
        {
            entity.ToTable(nameof(AchievementProgress), SchemaNames.Progression);
            entity.HasKey(p => new { p.OwnerId, p.AchievementId });
            entity.Property(p => p.AchievementId).IsRequired().HasMaxLength(64);
            entity.HasIndex(p => p.AccountId);
        });
        modelBuilder.Entity<UnlockedTitle>(entity =>
        {
            entity.ToTable(nameof(UnlockedTitle), SchemaNames.Progression);
            entity.HasKey(t => new { t.OwnerId, t.TitleId });
            entity.Property(t => t.TitleId).IsRequired().HasMaxLength(64);
            entity.Property(t => t.Source).HasMaxLength(64);
        });
        modelBuilder.Entity<ActiveTitle>(entity =>
        {
            entity.ToTable(nameof(ActiveTitle), SchemaNames.Progression);
            entity.HasKey(t => t.CharacterId);
            entity.Property(t => t.TitleId).IsRequired().HasMaxLength(64);
        });
        modelBuilder.Entity<SeasonProgress>(entity =>
        {
            entity.ToTable(nameof(SeasonProgress), SchemaNames.Progression);
            entity.HasKey(p => new { p.AccountId, p.SeasonId });
            entity.Property(p => p.SeasonId).IsRequired().HasMaxLength(64);
        });
        modelBuilder.Entity<SeasonClaim>(entity =>
        {
            entity.ToTable(nameof(SeasonClaim), SchemaNames.Progression);
            entity.HasKey(c => new { c.AccountId, c.SeasonId, c.Level, c.IsPremium });
            entity.Property(c => c.SeasonId).IsRequired().HasMaxLength(64);
        });
        modelBuilder.Entity<SeasonPremium>(entity =>
        {
            entity.ToTable(nameof(SeasonPremium), SchemaNames.Progression);
            entity.HasKey(p => new { p.AccountId, p.SeasonId });
            entity.Property(p => p.SeasonId).IsRequired().HasMaxLength(64);
            entity.Property(p => p.GrantedBy).HasMaxLength(64);
        });
    }
}
