using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Infrastructure.Persistence.Configurations;

internal sealed class GeneralSettingsConfiguration : IEntityTypeConfiguration<GeneralSettings>
{
    public void Configure(EntityTypeBuilder<GeneralSettings> builder)
    {
        builder.ToTable(
            "Settings",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("CK_Settings_Singleton", "Id = 1");
                tableBuilder.HasCheckConstraint(
                    "CK_Settings_WeeklyUsageFeeMinorUnits",
                    "WeeklyUsageFeeMinorUnits >= 0");
                tableBuilder.HasCheckConstraint(
                    "CK_Settings_CollaboratorProfitBasisPoints",
                    "CollaboratorProfitBasisPoints >= 0 AND CollaboratorProfitBasisPoints <= 10000");
                tableBuilder.HasCheckConstraint(
                    "CK_Settings_OptionalSuppliesMonthlyBudgetMinorUnits",
                    "OptionalSuppliesMonthlyBudgetMinorUnits >= 0");
                tableBuilder.HasCheckConstraint("CK_Settings_TotalChairs", "TotalChairs >= 0");
                tableBuilder.HasCheckConstraint(
                    "CK_Settings_CurrencyCode",
                    "length(CurrencyCode) = 3 AND CurrencyCode GLOB '[A-Z][A-Z][A-Z]'");
            });

        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();

        builder.Property(settings => settings.WeeklyUsageFee)
            .HasConversion(
                value => value.MinorUnits,
                value => Money.FromMinorUnits(value))
            .HasColumnName("WeeklyUsageFeeMinorUnits")
            .IsRequired();

        builder.Property(settings => settings.CollaboratorProfit)
            .HasConversion(
                value => value.BasisPoints,
                value => Percentage.FromBasisPoints(value))
            .HasColumnName("CollaboratorProfitBasisPoints")
            .IsRequired();

        builder.Property(settings => settings.OptionalSuppliesMonthlyBudget)
            .HasConversion(
                value => value.MinorUnits,
                value => Money.FromMinorUnits(value))
            .HasColumnName("OptionalSuppliesMonthlyBudgetMinorUnits")
            .IsRequired();

        builder.Property(settings => settings.TotalChairs).IsRequired();

        builder.Property(settings => settings.CurrencyCode)
            .HasConversion(
                value => value.Value,
                value => CurrencyCode.From(value))
            .HasColumnName("CurrencyCode")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(settings => settings.ExportDirectory)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(settings => settings.CreatedUtc)
            .HasConversion(
                value => value.Ticks,
                value => new DateTime(value, DateTimeKind.Utc))
            .IsRequired();

        builder.Property(settings => settings.UpdatedUtc)
            .HasConversion(
                value => value.Ticks,
                value => new DateTime(value, DateTimeKind.Utc))
            .IsRequired();
    }
}
