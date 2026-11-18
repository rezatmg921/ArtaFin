using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class LeaseContractConfiguration : IEntityTypeConfiguration<LeaseContract>
{
    public void Configure(EntityTypeBuilder<LeaseContract> builder)
    {
        builder.ToTable("LeaseContracts", table =>
        {
            table.HasCheckConstraint("CK_LeaseContracts_InitialLiability_Positive", "[InitialLiabilityAmount] > 0");
            table.HasCheckConstraint("CK_LeaseContracts_EffectivePeriodicRate_Valid", "[EffectivePeriodicRate] BETWEEN 0 AND 999999999.9999999999");
            table.HasCheckConstraint("CK_LeaseContracts_PaymentFrequency_Valid", "[PaymentFrequency] IN (1, 3, 6, 12)");
            table.HasCheckConstraint("CK_LeaseContracts_PaymentTiming_Valid", "[PaymentTiming] IN (1, 2)");
            table.HasCheckConstraint("CK_LeaseContracts_Status_Valid", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_LeaseContracts_PaymentDates_Valid",
                "([PaymentTiming] = 1 AND [FirstPaymentDate] >= [CommencementDate]) OR ([PaymentTiming] = 2 AND [FirstPaymentDate] > [CommencementDate])");
            table.HasCheckConstraint("CK_LeaseContracts_EndDate_Valid", "[ContractEndDate] >= [FirstPaymentDate]");
            table.HasCheckConstraint("CK_LeaseContracts_Accounts_Distinct",
                "[LeaseLiabilityAccountId] <> [InterestExpenseAccountId] AND [LeaseLiabilityAccountId] <> [PaymentAccountId] AND [InterestExpenseAccountId] <> [PaymentAccountId]");
        });

        builder.HasKey(contract => contract.Id);
        builder.Property(contract => contract.FinancialBookId).IsRequired();
        builder.Property(contract => contract.ContractNumber).HasMaxLength(LeaseContract.MaxContractNumberLength).IsRequired();
        builder.Property(contract => contract.Description).HasMaxLength(LeaseContract.MaxDescriptionLength).IsRequired();
        builder.Property(contract => contract.CommencementDate).HasColumnType("date").IsRequired();
        builder.Property(contract => contract.FirstPaymentDate).HasColumnType("date").IsRequired();
        builder.Property(contract => contract.ContractEndDate).HasColumnType("date").IsRequired();
        builder.Ignore(contract => contract.InitialLiability);
        builder.Property(contract => contract.InitialLiabilityAmount).HasPrecision(19, 4).IsRequired();
        builder.Property(contract => contract.InitialLiabilityCurrency).HasColumnName("Currency")
            .HasColumnType("char(3)").HasMaxLength(3).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(contract => contract.EffectivePeriodicRate).HasPrecision(19, 10).IsRequired();
        builder.Property(contract => contract.PaymentFrequency).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(contract => contract.PaymentTiming).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(contract => contract.Status).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(contract => contract.LeaseLiabilityAccountId).IsRequired();
        builder.Property(contract => contract.InterestExpenseAccountId).IsRequired();
        builder.Property(contract => contract.PaymentAccountId).IsRequired();

        builder.HasOne<FinancialBook>().WithMany()
            .HasForeignKey(contract => contract.FinancialBookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>().WithMany()
            .HasForeignKey(contract => new { contract.FinancialBookId, contract.LeaseLiabilityAccountId })
            .HasPrincipalKey(account => new { account.FinancialBookId, account.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>().WithMany()
            .HasForeignKey(contract => new { contract.FinancialBookId, contract.InterestExpenseAccountId })
            .HasPrincipalKey(account => new { account.FinancialBookId, account.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>().WithMany()
            .HasForeignKey(contract => new { contract.FinancialBookId, contract.PaymentAccountId })
            .HasPrincipalKey(account => new { account.FinancialBookId, account.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(contract => new { contract.FinancialBookId, contract.ContractNumber })
            .IsUnique().HasDatabaseName("UX_LeaseContracts_Book_ContractNumber");
        builder.HasIndex(contract => new { contract.FinancialBookId, contract.Status, contract.ContractEndDate })
            .HasDatabaseName("IX_LeaseContracts_Book_Status_EndDate");
    }
}
