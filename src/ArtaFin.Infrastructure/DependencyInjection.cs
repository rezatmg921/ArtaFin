using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Infrastructure.Persistence;
using ArtaFin.Infrastructure.Persistence.Repositories;
using ArtaFin.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArtaFin.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Integration hosts may build a bare ServiceCollection; retain the
        // supplied configuration as a fallback without overriding host config.
        services.TryAddSingleton<IConfiguration>(configuration);

        services.AddDbContext<ArtaFinDbContext>((provider, options) =>
        {
            // Resolve configuration when the DbContext is created, not while
            // services are registered, so host-specific providers can override it.
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString("ArtaFin");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'ArtaFin' was not configured.");
            }

            options.UseSqlServer(connectionString);
        });

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<ArtaFinDbContext>());
        services.AddScoped<AccountRepository>();
        services.AddScoped<IAccountRepository>(provider =>
            provider.GetRequiredService<AccountRepository>());
        services.AddScoped<IAccountReadRepository>(provider =>
            provider.GetRequiredService<AccountRepository>());
        services.AddScoped<JournalEntryRepository>();
        services.AddScoped<IJournalEntryRepository>(provider =>
            provider.GetRequiredService<JournalEntryRepository>());
        services.AddScoped<IJournalEntryReadRepository>(provider =>
            provider.GetRequiredService<JournalEntryRepository>());
        services.AddScoped<IAccountLedgerReadRepository, AccountLedgerReadRepository>();
        services.AddScoped<IAccountBalanceReadRepository, AccountBalanceReadRepository>();
        services.AddScoped<ITrialBalanceReadRepository, TrialBalanceReadRepository>();
        services.AddScoped<IIncomeStatementReadRepository, IncomeStatementReadRepository>();
        services.AddScoped<IBalanceSheetReadRepository, BalanceSheetReadRepository>();
        services.AddScoped<ICashFlowStatementReadRepository, CashFlowStatementReadRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IFinancialBookRepository, FinancialBookRepository>();
        services.AddScoped<IFinancialBookWriter, FinancialBookRepository>();
        services.AddScoped<ICashFlowAllocationRepository, CashFlowAllocationRepository>();
        services.AddScoped<ICashFlowNonCashDisclosureRepository, CashFlowNonCashDisclosureRepository>();
        services.AddScoped<IFinancingLiabilitiesReconciliationReadRepository, FinancingLiabilitiesReconciliationReadRepository>();

        return services;
    }
}
