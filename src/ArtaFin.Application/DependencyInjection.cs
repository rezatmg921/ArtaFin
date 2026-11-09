using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Authentication;
using ArtaFin.Application.Features.FinancialBooks.CreateFinancialBook;
using ArtaFin.Application.Features.FinancialBooks.InviteMember;
using ArtaFin.Application.Features.FinancialBooks.ListMembers;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using Microsoft.Extensions.DependencyInjection;

namespace ArtaFin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CreateAccountCommandHandler>();
        services.AddScoped<CreateJournalEntryCommandHandler>();
        services.AddScoped<CreateFinancialBookCommandHandler>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<InviteFinancialBookMemberCommandHandler>();
        services.AddScoped<ListFinancialBookMembersQueryHandler>();
        return services;
    }
}
