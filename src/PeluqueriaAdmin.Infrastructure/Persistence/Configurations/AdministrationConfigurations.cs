using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Infrastructure.Persistence.Configurations;

internal static class AdministrationConfiguration
{
    public static void ConfigureAudit<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.CreatedUtc)
            .HasConversion(value => value.Ticks, value => new DateTime(value, DateTimeKind.Utc));
        builder.Property(entity => entity.UpdatedUtc)
            .HasConversion(value => value.Ticks, value => new DateTime(value, DateTimeKind.Utc));
        builder.Property(entity => entity.DeletedUtc)
            .HasConversion(
                value => value.HasValue ? value.Value.Ticks : (long?)null,
                value => value.HasValue ? new DateTime(value.Value, DateTimeKind.Utc) : null);
        builder.Ignore(entity => entity.IsDeleted);
        builder.HasQueryFilter(entity => entity.DeletedUtc == null);
    }

    public static PropertyBuilder<Money> ConfigureMoney(PropertyBuilder<Money> property) => property
        .HasConversion(value => value.MinorUnits, value => Money.FromMinorUnits(value));

    public static PropertyBuilder<Money?> ConfigureNullableMoney(PropertyBuilder<Money?> property) => property
        .HasConversion(
            value => value.HasValue ? value.Value.MinorUnits : (long?)null,
            value => value.HasValue ? Money.FromMinorUnits(value.Value) : null);

    public static PropertyBuilder<YearMonth> ConfigureMonth(PropertyBuilder<YearMonth> property) => property
        .HasConversion(
            value => value.Year * 100 + value.Month,
            value => new YearMonth(value / 100, value % 100));
}

internal sealed class LocalUsePersonConfiguration : IEntityTypeConfiguration<LocalUsePerson>
{
    public void Configure(EntityTypeBuilder<LocalUsePerson> builder)
    {
        builder.ToTable("LocalUsePeople");
        AdministrationConfiguration.ConfigureAudit(builder);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
    }
}

internal sealed class WeeklyRateConfiguration : IEntityTypeConfiguration<WeeklyRate>
{
    public void Configure(EntityTypeBuilder<WeeklyRate> builder)
    {
        builder.ToTable("WeeklyRates");
        AdministrationConfiguration.ConfigureAudit(builder);
        AdministrationConfiguration.ConfigureMoney(builder.Property(item => item.Amount))
            .HasColumnName("AmountMinorUnits");
        builder.HasIndex(item => item.EffectiveFrom);
    }
}

internal sealed class WeeklyChargeConfiguration : IEntityTypeConfiguration<WeeklyCharge>
{
    public void Configure(EntityTypeBuilder<WeeklyCharge> builder)
    {
        builder.ToTable("WeeklyCharges");
        AdministrationConfiguration.ConfigureAudit(builder);
        AdministrationConfiguration.ConfigureMoney(builder.Property(item => item.Amount))
            .HasColumnName("AmountMinorUnits");
        builder.HasIndex(item => new { item.PersonId, item.PeriodStart }).IsUnique();
        builder.HasOne<LocalUsePerson>().WithMany().HasForeignKey(item => item.PersonId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LocalUsePaymentConfiguration : IEntityTypeConfiguration<LocalUsePayment>
{
    public void Configure(EntityTypeBuilder<LocalUsePayment> builder)
    {
        builder.ToTable("LocalUsePayments");
        AdministrationConfiguration.ConfigureAudit(builder);
        AdministrationConfiguration.ConfigureMoney(builder.Property(item => item.Amount))
            .HasColumnName("AmountMinorUnits");
        builder.HasIndex(item => item.PaymentDate);
        builder.HasOne<LocalUsePerson>().WithMany().HasForeignKey(item => item.PersonId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        AdministrationConfiguration.ConfigureAudit(builder);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.UnitOfMeasure).HasMaxLength(50).IsRequired();
    }
}

internal sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements");
        AdministrationConfiguration.ConfigureAudit(builder);
        builder.Property(item => item.QuantityDelta).HasPrecision(18, 3);
        AdministrationConfiguration.ConfigureNullableMoney(builder.Property(item => item.CashAmount))
            .HasColumnName("CashAmountMinorUnits");
        AdministrationConfiguration.ConfigureNullableMoney(builder.Property(item => item.EstimatedCost))
            .HasColumnName("EstimatedCostMinorUnits");
        builder.HasIndex(item => new { item.ProductId, item.Date });
        builder.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MonthlyRestockPlanConfiguration : IEntityTypeConfiguration<MonthlyRestockPlan>
{
    public void Configure(EntityTypeBuilder<MonthlyRestockPlan> builder)
    {
        builder.ToTable("MonthlyRestockPlans");
        AdministrationConfiguration.ConfigureAudit(builder);
        AdministrationConfiguration.ConfigureMonth(builder.Property(item => item.Month));
        builder.Property(item => item.NeededQuantity)
            .HasConversion(value => value.Value, value => Quantity.NonNegative(value))
            .HasPrecision(18, 3);
        builder.HasIndex(item => new { item.ProductId, item.Month }).IsUnique();
        builder.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FinancialEntryConfiguration : IEntityTypeConfiguration<FinancialEntry>
{
    public void Configure(EntityTypeBuilder<FinancialEntry> builder)
    {
        builder.ToTable("FinancialEntries");
        AdministrationConfiguration.ConfigureAudit(builder);
        builder.Property(item => item.Concept).HasMaxLength(300).IsRequired();
        AdministrationConfiguration.ConfigureMoney(builder.Property(item => item.Amount))
            .HasColumnName("AmountMinorUnits");
        builder.HasIndex(item => item.Date);
    }
}

internal sealed class ObligationConfiguration : IEntityTypeConfiguration<Obligation>
{
    public void Configure(EntityTypeBuilder<Obligation> builder)
    {
        builder.ToTable("Obligations");
        AdministrationConfiguration.ConfigureAudit(builder);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        AdministrationConfiguration.ConfigureMoney(builder.Property(item => item.ExpectedAmount))
            .HasColumnName("ExpectedAmountMinorUnits");
        builder.HasIndex(item => new { item.SeriesId, item.DueDate }).IsUnique();
    }
}

internal sealed class ObligationPaymentConfiguration : IEntityTypeConfiguration<ObligationPayment>
{
    public void Configure(EntityTypeBuilder<ObligationPayment> builder)
    {
        builder.ToTable("ObligationPayments");
        AdministrationConfiguration.ConfigureAudit(builder);
        AdministrationConfiguration.ConfigureMoney(builder.Property(item => item.Amount))
            .HasColumnName("AmountMinorUnits");
        builder.HasIndex(item => item.Date);
        builder.HasOne<Obligation>().WithMany().HasForeignKey(item => item.ObligationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MaintenanceRecordConfiguration : IEntityTypeConfiguration<MaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecord> builder)
    {
        builder.ToTable("MaintenanceRecords");
        AdministrationConfiguration.ConfigureAudit(builder);
        builder.Property(item => item.Asset).HasMaxLength(200).IsRequired();
        builder.Property(item => item.MaintenanceType).HasMaxLength(200).IsRequired();
        AdministrationConfiguration.ConfigureNullableMoney(builder.Property(item => item.EstimatedCost))
            .HasColumnName("EstimatedCostMinorUnits");
        AdministrationConfiguration.ConfigureNullableMoney(builder.Property(item => item.ActualCost))
            .HasColumnName("ActualCostMinorUnits");
    }
}

internal sealed class CollaboratorConfiguration : IEntityTypeConfiguration<Collaborator>
{
    public void Configure(EntityTypeBuilder<Collaborator> builder)
    {
        builder.ToTable("Collaborators");
        AdministrationConfiguration.ConfigureAudit(builder);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
    }
}

internal sealed class MonthlyCloseConfiguration : IEntityTypeConfiguration<MonthlyClose>
{
    public void Configure(EntityTypeBuilder<MonthlyClose> builder)
    {
        builder.ToTable("MonthlyCloses");
        AdministrationConfiguration.ConfigureAudit(builder);
        AdministrationConfiguration.ConfigureMonth(builder.Property(item => item.Month));
        builder.Property(item => item.ClosedUtc)
            .HasConversion(value => value.Ticks, value => new DateTime(value, DateTimeKind.Utc));
        builder.Property(item => item.ReopenedUtc)
            .HasConversion(
                value => value.HasValue ? value.Value.Ticks : (long?)null,
                value => value.HasValue ? new DateTime(value.Value, DateTimeKind.Utc) : null);
        builder.Ignore(item => item.IsConfirmed);
        builder.HasIndex(item => item.Month);
    }
}

internal sealed class MonthlyCloseParticipantConfiguration : IEntityTypeConfiguration<MonthlyCloseParticipant>
{
    public void Configure(EntityTypeBuilder<MonthlyCloseParticipant> builder)
    {
        builder.ToTable("MonthlyCloseParticipants");
        AdministrationConfiguration.ConfigureAudit(builder);
        AdministrationConfiguration.ConfigureMoney(builder.Property(item => item.Amount))
            .HasColumnName("AmountMinorUnits");
        builder.HasIndex(item => new { item.CloseId, item.CollaboratorId }).IsUnique();
        builder.HasOne<MonthlyClose>().WithMany().HasForeignKey(item => item.CloseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Collaborator>().WithMany().HasForeignKey(item => item.CollaboratorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DistributionPaymentConfiguration : IEntityTypeConfiguration<DistributionPayment>
{
    public void Configure(EntityTypeBuilder<DistributionPayment> builder)
    {
        builder.ToTable("DistributionPayments");
        AdministrationConfiguration.ConfigureAudit(builder);
        AdministrationConfiguration.ConfigureMoney(builder.Property(item => item.Amount))
            .HasColumnName("AmountMinorUnits");
        builder.HasIndex(item => item.Date);
        builder.HasOne<MonthlyCloseParticipant>().WithMany().HasForeignKey(item => item.ParticipantId).OnDelete(DeleteBehavior.Restrict);
    }
}
