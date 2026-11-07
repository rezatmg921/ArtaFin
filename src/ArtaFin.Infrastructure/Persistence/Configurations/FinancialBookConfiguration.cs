using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class FinancialBookConfiguration : IEntityTypeConfiguration<FinancialBook>
{
    public void Configure(EntityTypeBuilder<FinancialBook> builder)
    {
        builder.ToTable("FinancialBooks");

        builder.HasKey(book => book.Id);

        builder.Property(book => book.OwnerId)
            .IsRequired();

        builder.Property(book => book.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(book => book.BaseCurrency)
            .HasColumnType("char(3)")
            .HasMaxLength(3)
            .IsUnicode(false)
            .IsFixedLength()
            .IsRequired();

        builder.Property(book => book.CreatedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.HasIndex(book => book.OwnerId)
            .HasDatabaseName("IX_FinancialBooks_OwnerId");
    }
}
