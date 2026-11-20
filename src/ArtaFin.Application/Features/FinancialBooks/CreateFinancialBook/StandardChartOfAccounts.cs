using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.FinancialBooks.CreateFinancialBook;

/// <summary>
/// Creates the initial posting accounts for a newly created financial book.
/// Existing account codes are deliberately left untouched so this operation is safe to rerun.
/// </summary>
internal static class StandardChartOfAccounts
{
    private static readonly IReadOnlyList<AccountTemplate> Templates =
    [
        new("1000", "صندوق", AccountType.Asset, CashFlowAccountType.Cash),
        new("1001", "بانک", AccountType.Asset, CashFlowAccountType.Cash),
        new("1002", "تنخواه‌گردان", AccountType.Asset, CashFlowAccountType.Cash),
        new("1100", "حساب‌های دریافتنی", AccountType.Asset),
        new("1101", "اسناد دریافتنی", AccountType.Asset),
        new("1102", "مطالبات از مشتریان", AccountType.Asset),
        new("1200", "موجودی مواد و کالا", AccountType.Asset),
        new("1201", "ملزومات اداری", AccountType.Asset),
        new("1300", "پیش‌پرداخت‌ها", AccountType.Asset),
        new("1301", "پیش‌پرداخت اجاره", AccountType.Asset),
        new("1302", "پیش‌پرداخت بیمه", AccountType.Asset),
        new("1500", "دارایی‌های ثابت مشهود", AccountType.Asset),
        new("1501", "تجهیزات اداری", AccountType.Asset),
        new("1502", "وسایل نقلیه", AccountType.Asset),
        new("1503", "ساختمان", AccountType.Asset),
        new("1504", "زمین", AccountType.Asset),
        new("1600", "دارایی حق استفاده", AccountType.Asset),
        new("1601", "حق استفاده از ساختمان", AccountType.Asset),
        new("1602", "حق استفاده از تجهیزات", AccountType.Asset),

        new("2000", "حساب‌های پرداختنی", AccountType.Liability),
        new("2001", "اسناد پرداختنی", AccountType.Liability),
        new("2002", "بدهی به تأمین‌کنندگان", AccountType.Liability),
        new("2100", "حقوق پرداختنی", AccountType.Liability),
        new("2101", "مالیات پرداختنی", AccountType.Liability),
        new("2102", "بیمه پرداختنی", AccountType.Liability),
        new("2200", "تسهیلات مالی کوتاه‌مدت", AccountType.Liability),
        new("2300", "بدهی‌های بلندمدت", AccountType.Liability),
        new("2301", "بدهی اجاره تأمین مالی", AccountType.Liability,
            FinancingLiabilityType: FinancingLiabilityType.LeaseLiabilities),
        new("2302", "تسهیلات مالی بلندمدت", AccountType.Liability),

        new("3000", "سرمایه", AccountType.Equity),
        new("3001", "سرمایه اولیه", AccountType.Equity),
        new("3100", "اندوخته قانونی", AccountType.Equity),
        new("3200", "سود و زیان انباشته", AccountType.Equity),
        new("3300", "سود و زیان دوره", AccountType.Equity),

        new("4000", "درآمد عملیاتی", AccountType.Income),
        new("4001", "درآمد خدمات", AccountType.Income),
        new("4002", "درآمد فروش کالا", AccountType.Income),
        new("4003", "درآمد اجاره", AccountType.Income),
        new("4100", "درآمدهای غیرعملیاتی", AccountType.Income),
        new("4101", "درآمد سود سپرده", AccountType.Income),
        new("4102", "سود فروش دارایی", AccountType.Income),

        new("5000", "بهای تمام‌شده خدمات", AccountType.Expense),
        new("5001", "بهای تمام‌شده خدمات ارائه‌شده", AccountType.Expense),
        new("5002", "بهای تمام‌شده کالای فروش‌رفته", AccountType.Expense),
        new("5100", "هزینه مستقیم پروژه‌ها", AccountType.Expense),

        new("6000", "هزینه‌های اداری و عمومی", AccountType.Expense),
        new("6001", "هزینه ملزومات اداری", AccountType.Expense),
        new("6002", "هزینه آب و برق", AccountType.Expense),
        new("6003", "هزینه تلفن و اینترنت", AccountType.Expense),
        new("6004", "هزینه تعمیر و نگهداری", AccountType.Expense),
        new("6100", "هزینه‌های مالی", AccountType.Expense),
        new("6101", "هزینه بهره تأمین مالی", AccountType.Expense),
        new("6102", "کارمزد بانکی", AccountType.Expense),
        new("6103", "هزینه بهره اجاره", AccountType.Expense),
        new("6200", "هزینه حقوق و دستمزد", AccountType.Expense),
        new("6201", "حقوق کارکنان", AccountType.Expense),
        new("6202", "بیمه سهم کارفرما", AccountType.Expense),
        new("6300", "هزینه استهلاک", AccountType.Expense),
        new("6301", "استهلاک تجهیزات", AccountType.Expense),
        new("6302", "استهلاک ساختمان", AccountType.Expense),
        new("6400", "هزینه‌های عملیاتی", AccountType.Expense),
        new("6401", "هزینه اجاره", AccountType.Expense),
        new("6402", "هزینه حمل‌ونقل", AccountType.Expense),
        new("6403", "هزینه تبلیغات", AccountType.Expense)
    ];

    public static async Task SeedAsync(
        Guid financialBookId,
        IAccountRepository accountRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var createdAt = timeProvider.GetUtcNow();

        foreach (var template in Templates)
        {
            if (await accountRepository.ExistsByCodeAsync(
                    financialBookId, template.Code, cancellationToken))
            {
                continue;
            }

            var account = Account.Create(
                Guid.NewGuid(),
                financialBookId,
                template.Code,
                template.Name,
                template.Type,
                createdAt);

            if (template.CashFlowAccountType != CashFlowAccountType.None)
            {
                account.ConfigureCashFlowClassification(template.CashFlowAccountType);
            }

            if (template.FinancingLiabilityType != FinancingLiabilityType.None)
            {
                account.ConfigureFinancingLiabilityClassification(template.FinancingLiabilityType);
            }

            await accountRepository.AddAsync(account, cancellationToken);
        }
    }

    private sealed record AccountTemplate(
        string Code,
        string Name,
        AccountType Type,
        CashFlowAccountType CashFlowAccountType = CashFlowAccountType.None,
        FinancingLiabilityType FinancingLiabilityType = FinancingLiabilityType.None);
}
