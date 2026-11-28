using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtaFin.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ArtaFinDbContext))]
[Migration("20261122100000_EnsureLeasePaymentTrackingColumns")]
public sealed class EnsureLeasePaymentTrackingColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.LeasePaymentScheduleLines', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.LeasePaymentScheduleLines', N'IsPaid') IS NULL
                    ALTER TABLE [dbo].[LeasePaymentScheduleLines]
                    ADD [IsPaid] bit NOT NULL
                        CONSTRAINT [DF_LeasePaymentScheduleLines_IsPaid] DEFAULT (0);

                IF COL_LENGTH(N'dbo.LeasePaymentScheduleLines', N'JournalEntryId') IS NULL
                    ALTER TABLE [dbo].[LeasePaymentScheduleLines]
                    ADD [JournalEntryId] uniqueidentifier NULL;

                IF COL_LENGTH(N'dbo.LeasePaymentScheduleLines', N'PaidAt') IS NULL
                    ALTER TABLE [dbo].[LeasePaymentScheduleLines]
                    ADD [PaidAt] datetimeoffset(7) NULL;

                IF COL_LENGTH(N'dbo.LeasePaymentScheduleLines', N'PaidBy') IS NULL
                    ALTER TABLE [dbo].[LeasePaymentScheduleLines]
                    ADD [PaidBy] uniqueidentifier NULL;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE [name] = N'CK_LeasePaymentScheduleLines_Payment_Consistent'
                      AND [parent_object_id] = OBJECT_ID(N'dbo.LeasePaymentScheduleLines')
                )
                    ALTER TABLE [dbo].[LeasePaymentScheduleLines]
                    ADD CONSTRAINT [CK_LeasePaymentScheduleLines_Payment_Consistent]
                    CHECK
                    (
                        ([IsPaid] = 0 AND [JournalEntryId] IS NULL AND [PaidBy] IS NULL AND [PaidAt] IS NULL)
                        OR
                        ([IsPaid] = 1 AND [JournalEntryId] IS NOT NULL AND [PaidBy] IS NOT NULL AND [PaidAt] IS NOT NULL)
                    );

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'UX_LeasePaymentScheduleLines_JournalEntry'
                      AND [object_id] = OBJECT_ID(N'dbo.LeasePaymentScheduleLines')
                )
                    CREATE UNIQUE INDEX [UX_LeasePaymentScheduleLines_JournalEntry]
                    ON [dbo].[LeasePaymentScheduleLines] ([JournalEntryId])
                    WHERE [JournalEntryId] IS NOT NULL;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally retain these compatibility columns on rollback. They are
        // part of the lease-payment tracking contract and may have predated this
        // repair migration on an existing database.
    }
}
