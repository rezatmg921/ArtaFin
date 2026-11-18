using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class LeasePaymentScheduleConfiguration : IEntityTypeConfiguration<LeasePaymentSchedule>
{
    public void Configure(EntityTypeBuilder<LeasePaymentSchedule> builder)
    {
        builder.ToTable("LeasePaymentSchedules", table =>
        {
            table.HasCheckConstraint("CK_LeasePaymentSchedules_Version_Positive", "[Version] > 0");
            table.HasCheckConstraint("CK_LeasePaymentSchedules_Status_Valid", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_LeasePaymentSchedules_Version_Supersedes",
                "([Version] = 1 AND [SupersedesScheduleId] IS NULL) OR ([Version] > 1 AND [SupersedesScheduleId] IS NOT NULL)");
            table.HasCheckConstraint("CK_LeasePaymentSchedules_Finalization_Consistent",
                "([Status] = 1 AND [FinalizedBy] IS NULL AND [FinalizedAt] IS NULL) OR ([Status] IN (2, 3) AND [FinalizedBy] IS NOT NULL AND [FinalizedAt] IS NOT NULL)");
        });

        builder.HasKey(schedule => schedule.Id);
        builder.Property(schedule => schedule.FinancialBookId).IsRequired();
        builder.Property(schedule => schedule.LeaseContractId).IsRequired();
        builder.Property(schedule => schedule.Version).IsRequired();
        builder.Property(schedule => schedule.SupersedesScheduleId);
        builder.Property(schedule => schedule.Status).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(schedule => schedule.CreatedBy).IsRequired();
        builder.Property(schedule => schedule.CreatedAt).IsRequired();
        builder.Property(schedule => schedule.FinalizedBy);
        builder.Property(schedule => schedule.FinalizedAt);

        builder.HasOne<FinancialBook>().WithMany()
            .HasForeignKey(schedule => schedule.FinancialBookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LeaseContract>().WithMany()
            .HasForeignKey(schedule => new { schedule.FinancialBookId, schedule.LeaseContractId })
            .HasPrincipalKey(contract => new { contract.FinancialBookId, contract.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LeasePaymentSchedule>().WithMany()
            .HasForeignKey(schedule => schedule.SupersedesScheduleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(schedule => schedule.Payments)
            .WithOne().HasForeignKey(payment => payment.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(schedule => schedule.Payments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(schedule => new { schedule.LeaseContractId, schedule.Version })
            .IsUnique().HasDatabaseName("UX_LeasePaymentSchedules_Contract_Version");
        builder.HasIndex(schedule => new { schedule.FinancialBookId, schedule.LeaseContractId, schedule.Status })
            .HasDatabaseName("IX_LeasePaymentSchedules_Book_Contract_Status");
    }
}

public sealed class LeasePaymentScheduleLineConfiguration : IEntityTypeConfiguration<LeasePaymentScheduleLine>
{
    public void Configure(EntityTypeBuilder<LeasePaymentScheduleLine> builder)
    {
        builder.ToTable("LeasePaymentScheduleLines", table =>
        {
            table.HasCheckConstraint("CK_LeasePaymentScheduleLines_Period_Positive", "[PeriodNumber] > 0");
            table.HasCheckConstraint("CK_LeasePaymentScheduleLines_Amount_NonNegative", "[PaymentAmount] >= 0");
            table.HasCheckConstraint("CK_LeasePaymentScheduleLines_Rate_Valid",
                "[EffectivePeriodicRate] BETWEEN 0 AND 999999999.9999999999");
        });

        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.ScheduleId).IsRequired();
        builder.Property(payment => payment.PeriodNumber).IsRequired();
        builder.Property(payment => payment.PaymentDate).HasColumnType("date").IsRequired();
        builder.Property(payment => payment.PaymentAmount).HasPrecision(19, 4).IsRequired();
        builder.Property(payment => payment.EffectivePeriodicRate).HasPrecision(19, 10).IsRequired();

        builder.HasIndex(payment => new { payment.ScheduleId, payment.PeriodNumber })
            .IsUnique().HasDatabaseName("UX_LeasePaymentScheduleLines_Schedule_Period");
        builder.HasIndex(payment => new { payment.ScheduleId, payment.PaymentDate })
            .IsUnique().HasDatabaseName("UX_LeasePaymentScheduleLines_Schedule_Date");
    }
}
