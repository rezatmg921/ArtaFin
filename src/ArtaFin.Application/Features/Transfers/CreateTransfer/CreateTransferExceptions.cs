namespace ArtaFin.Application.Features.Transfers.CreateTransfer;

public sealed class TransferAccountsMustBeDifferentException()
    : Exception("Source and destination accounts must be different.");

public sealed class TransferAccountTypeInvalidException()
    : Exception("Both source and destination accounts must be asset accounts.");
