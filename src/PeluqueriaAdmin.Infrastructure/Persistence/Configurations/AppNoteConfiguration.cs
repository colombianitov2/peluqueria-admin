using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaAdmin.Domain.Notes;

namespace PeluqueriaAdmin.Infrastructure.Persistence.Configurations;

internal sealed class AppNoteConfiguration : IEntityTypeConfiguration<AppNote>
{
    public void Configure(EntityTypeBuilder<AppNote> builder)
    {
        builder.ToTable("Notes", table => table.HasCheckConstraint("CK_Notes_Singleton", "Id = 1"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Content).IsRequired();
        builder.Property(item => item.UpdatedUtc)
            .HasConversion(value => value.Ticks, value => new DateTime(value, DateTimeKind.Utc))
            .IsRequired();
    }
}
