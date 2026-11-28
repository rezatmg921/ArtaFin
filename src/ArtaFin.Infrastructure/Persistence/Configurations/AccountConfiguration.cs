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
            table.HasCheckConstraint("CK_Accounts_BalanceSheetClassification_Valid", "[BalanceSheetClassification] IN (0, 1, 2)");
            table.HasCheckConstraint("CK_Accounts_BalanceSheetClassification_EligibleType", "[BalanceSheetClassification] = 0 OR [Type] IN (1, 2)");
            table.HasCheckConstraint("CK_Accounts_NormalBalance_Valid", "[NormalBalance] IN (1, 2)");
            table.HasCheckConstraint("CK_Accounts_Status_Valid", "[Status] IN (1, 2)");
            table.HasCheckConstraint("CK_Accounts_BalancePolicy_Valid", "[BalancePolicy] IN (1, 2)");
            table.HasCheckConstraint("CK_Accounts_CashFlowClassification_Valid",
                "([CashFlowAccountType] = 0 AND [CashEquivalentMaturityDays] IS NULL AND [CashEquivalentRiskInsignificant] IS NULL) OR ([CashFlowAccountType] = 1 AND [CashEquivalentMaturityDays] IS NULL AND [CashEquivalentRiskInsignificant] IS NULL) OR ([CashFlowAccountType] = 2 AND [CashEquivalentMaturityDays] BETWEEN 0 AND 90 AND [CashEquivalentRiskInsignificant] = 1)");
            table.HasCheckConstraint("CK_Accounts_CashFlowClassification_AssetOnly",
                "[CashFlowAccountType] = 0 OR [Type] = 1");
            table.HasCheckConstraint("CK_Accounts_FinancingLiabilityType_Valid",
                "[FinancingLiabilityType] IN (0, 1, 2, 3)");
            table.HasCheckConstraint("CK_Accounts_FinancingLiabilityType_LiabilityOnly",
                "[FinancingLiabilityType] = 0 OR [Type] = 2");
        });

        builder.HasKey(account => account.Id);
        builder.HasAlternateKey(account => new { account.FinancialBookId, account.Id })
            .HasName("AK_Accounts_FinancialBookId_Id");

        builder.Property(account => account.FinancialBookId).IsRequired();
        builder.Property(account => account.Code).HasMaxLength(Account.MaxCodeLength).IsUnicode(false).IsRequired();
        builder.Property(account => account.Name).HasMaxLength(Account.MaxNameLength).IsRequired();
        builder.Property(account => account.Type).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(account => account.NormalBalance).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(account => account.BalancePolicy).HasConversion<byte>().HasColumnType("tinyint").HasDefaultValue(AccountBalancePolicy.PreventNegative).HasSentinel((AccountBalancePolicy)0).IsRequired();
        builder.Property(account => account.Status).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(account => account.CashFlowAccountType).HasConversion<byte>().HasColumnType("tinyint").HasDefaultValue(CashFlowAccountType.None).IsRequired();
        builder.Property(account => account.CashEquivalentMaturityDays).HasColumnType("int");
        builder.Property(account => account.CashEquivalentRiskInsignificant).HasColumnType("bit");
        builder.Property(account => account.BalanceSheetClassification).HasConversion<byte>()
            .HasColumnType("tinyint").HasDefaultValue(BalanceSheetClassification.Unclassified).IsRequired();
        builder.Property(account => account.FinancingLiabilityType).HasConversion<byte>()
            .HasColumnType("tinyint").HasDefaultValue(FinancingLiabilityType.None).IsRequired();
        builder.Property(account => account.CreatedAt).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(account => account.DeactivatedAt).HasColumnType("datetimeoffset(7)");

        builder.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .IsRequired()
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
