using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Obligations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class FinancialObligationPaymentConfiguration : IEntityTypeConfiguration<FinancialObligationPayment>
{
    public void Configure(EntityTypeBuilder<FinancialObligationPayment> builder)
    {
        builder.ToTable("FinancialObligationPayments", table =>
        {
            table.HasCheckConstraint("CK_FinancialObligationPayments_Amount_Positive", "[Amount] > 0");
            table.HasCheckConstraint("CK_FinancialObligationPayments_IdempotencyMetadata_Valid", "LEN([IdempotencyKey]) BETWEEN 1 AND 128 AND LEN([RequestHash]) = 64");
        });

        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.FinancialObligationId })
            .HasName("AK_FinancialObligationPayments_Id_ObligationId");

        builder.Property(x => x.FinancialBookId).IsRequired();
        builder.Property(x => x.FinancialObligationId).IsRequired();
        builder.Property(x => x.CashAccountId).IsRequired();
        builder.Property(x => x.JournalEntryId).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(19, 4).IsRequired();
        builder.Property(x => x.Currency).HasColumnType("char(3)").HasMaxLength(3).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(x => x.PaymentDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.Description).HasMaxLength(FinancialObligationPayment.MaxDescriptionLength).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(FinancialObligationPayment.MaxReferenceLength);
        builder.Property(x => x.CreatedBy).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestHash).HasColumnType("char(64)").HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();

        builder.HasMany(x => x.Allocations).WithOne()
            .HasForeignKey(x => new { x.PaymentId, x.FinancialObligationId })
            .HasPrincipalKey(x => new { x.Id, x.FinancialObligationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<FinancialObligation>().WithMany()
            .HasForeignKey(x => new { x.FinancialBookId, x.FinancialObligationId })
            .HasPrincipalKey(x => new { x.FinancialBookId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany()
            .HasForeignKey(x => new { x.FinancialBookId, x.CashAccountId })
            .HasPrincipalKey(x => new { x.FinancialBookId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany()
            .HasForeignKey(x => x.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.FinancialBookId, x.CreatedBy, x.IdempotencyKey })
            .IsUnique().HasDatabaseName("UX_FinancialObligationPayments_Idempotency");
        builder.HasIndex(x => new { x.FinancialBookId, x.FinancialObligationId, x.PaymentDate, x.Id })
            .HasDatabaseName("IX_FinancialObligationPayments_Obligation_Date");
        builder.HasIndex(x => new { x.FinancialBookId, x.Id })
            .IsUnique().HasDatabaseName("UX_FinancialObligationPayments_Book_Id");
    }
}

public sealed class FinancialObligationPaymentAllocationConfiguration
    : IEntityTypeConfiguration<FinancialObligationPaymentAllocation>
{
    public void Configure(EntityTypeBuilder<FinancialObligationPaymentAllocation> builder)
    {
        builder.ToTable("FinancialObligationPaymentAllocations", table =>
            table.HasCheckConstraint("CK_FinancialObligationPaymentAllocations_Amount_Positive", "[Amount] > 0"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.PaymentId).IsRequired();
        builder.Property(x => x.FinancialObligationId).IsRequired();
        builder.Property(x => x.InstallmentId).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(19, 4).IsRequired();

        builder.HasOne<FinancialObligationInstallment>().WithMany()
            .HasForeignKey(x => new { x.FinancialObligationId, x.InstallmentId })
            .HasPrincipalKey(x => new { x.FinancialObligationId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PaymentId, x.InstallmentId })
            .IsUnique().HasDatabaseName("UX_FinancialObligationPaymentAllocations_Payment_Installment");
        builder.HasIndex(x => new { x.FinancialObligationId, x.InstallmentId })
            .HasDatabaseName("IX_FinancialObligationPaymentAllocations_Obligation_Installment");
    }
}