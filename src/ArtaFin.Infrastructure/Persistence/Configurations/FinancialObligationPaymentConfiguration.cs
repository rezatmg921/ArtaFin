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
            table.HasCheckConstraint("CK_FinancialObligationPayments_ReversalMetadata_Consistent", "([ReversedAt] IS NULL AND [ReversedBy] IS NULL AND [ReversalJournalEntryId] IS NULL AND [ReversalReason] IS NULL AND [ReversalIdempotencyKey] IS NULL AND [ReversalRequestHash] IS NULL) OR ([ReversedAt] IS NOT NULL AND [ReversedBy] IS NOT NULL AND [ReversalJournalEntryId] IS NOT NULL AND [ReversalReason] IS NOT NULL AND [ReversalIdempotencyKey] IS NOT NULL AND [ReversalRequestHash] IS NOT NULL)");
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
        builder.Property(x => x.ReversedAt).HasColumnType("datetimeoffset(7)");
        builder.Property(x => x.ReversedBy);
        builder.Property(x => x.ReversalJournalEntryId);
        builder.Property(x => x.ReversalReason).HasMaxLength(FinancialObligationPayment.MaxDescriptionLength);
        builder.Property(x => x.ReversalIdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.ReversalRequestHash).HasColumnType("char(64)").HasMaxLength(64).IsUnicode(false).IsFixedLength();

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
        builder.HasOne<JournalEntry>().WithMany()
            .HasForeignKey(x => x.ReversalJournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.FinancialBookId, x.CreatedBy, x.IdempotencyKey })
            .IsUnique().HasDatabaseName("UX_FinancialObligationPayments_Idempotency");
        builder.HasIndex(x => new { x.FinancialBookId, x.CashAccountId })
            .HasDatabaseName("IX_FinancialObligationPayments_FinancialBookId_CashAccountId");
        builder.HasIndex(x => x.JournalEntryId)
            .HasDatabaseName("IX_FinancialObligationPayments_JournalEntryId");
        builder.HasIndex(x => x.ReversalJournalEntryId)
            .IsUnique().HasFilter("[ReversalJournalEntryId] IS NOT NULL")
            .HasDatabaseName("UX_FinancialObligationPayments_ReversalJournalEntryId");
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
        builder.HasIndex(x => new { x.PaymentId, x.FinancialObligationId })
            .HasDatabaseName("IX_FinancialObligationPaymentAllocations_PaymentId_FinancialObligationId");
        builder.HasIndex(x => new { x.FinancialObligationId, x.InstallmentId })
            .HasDatabaseName("IX_FinancialObligationPaymentAllocations_Obligation_Installment");
    }
}