using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", table =>
        {
            table.HasCheckConstraint("CK_Accounts_Type_Valid", "[Type] IN (1, 2, 3, 4, 5)");
            table.HasCheckConstraint("CK_Accounts_NormalBalance_Valid", "[NormalBalance] IN (1, 2)");
            table.HasCheckConstraint("CK_Accounts_Status_Valid", "[Status] IN (1, 2)");
        });

        builder.HasKey(account => account.Id);
        builder.HasAlternateKey(account => new { account.FinancialBookId, account.Id })
            .HasName("AK_Accounts_FinancialBookId_Id");

        builder.Property(account => account.FinancialBookId).IsRequired();
        builder.Property(account => account.Code).HasMaxLength(Account.MaxCodeLength).IsUnicode(false).IsRequired();
        builder.Property(account => account.Name).HasMaxLength(Account.MaxNameLength).IsRequired();
        builder.Property(account => account.Type).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(account => account.NormalBalance).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(account => account.Status).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(account => account.CreatedAt).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(account => account.DeactivatedAt).HasColumnType("datetimeoffset(7)");

        // SQL Server updates this value whenever the account row changes.
        // EF Core uses it as an optimistic concurrency token.
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .HasColumnName("RowVersion");

        builder.HasOne<FinancialBook>()
            .WithMany()
            .HasForeignKey(account => account.FinancialBookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(account => new { account.FinancialBookId, account.Code })
            .IsUnique()
            .HasDatabaseName("UX_Accounts_FinancialBookId_Code");
    }
}
