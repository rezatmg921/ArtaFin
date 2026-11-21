using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// A read snapshot of an account and the SQL Server version used for HTTP ETag.
/// </summary>
public sealed record AccountReadSnapshot(Account Account, byte[] RowVersion);
