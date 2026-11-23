using ArtaFin.Domain.Contacts;
using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("Contacts", table =>
        {
            table.HasCheckConstraint("CK_Contacts_Type_Valid", "[Type] IN (1, 2)");
        });

        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.FinancialBookId).IsRequired();
        builder.Property(contact => contact.Type).HasConversion<byte>()
            .HasColumnType("tinyint").IsRequired();
        builder.Property(contact => contact.DisplayName)
            .HasMaxLength(Contact.MaxDisplayNameLength).IsRequired();
        builder.Property(contact => contact.Email)
            .HasMaxLength(Contact.MaxEmailLength);
        builder.Property(contact => contact.Phone)
            .HasMaxLength(Contact.MaxPhoneLength).IsUnicode(false);
        builder.Property(contact => contact.Notes)
            .HasMaxLength(Contact.MaxNotesLength);
        builder.Property(contact => contact.CreatedAt)
            .HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(contact => contact.UpdatedAt)
            .HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(contact => contact.ArchivedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.HasIndex(contact => new { contact.FinancialBookId, contact.ArchivedAt })
            .HasDatabaseName("IX_Contacts_FinancialBookId_ArchivedAt");
        builder.HasIndex(contact => new { contact.FinancialBookId, contact.Email })
            .HasDatabaseName("IX_Contacts_FinancialBookId_Email");

        builder.HasOne<FinancialBook>().WithMany()
            .HasForeignKey(contact => contact.FinancialBookId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
