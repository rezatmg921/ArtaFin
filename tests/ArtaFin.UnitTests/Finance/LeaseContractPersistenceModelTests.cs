using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ArtaFin.UnitTests.Finance;

public sealed class LeaseContractPersistenceModelTests
{
    [Fact]
    public void Model_MapsContractAndThreeSameBookAccountRelationships()
    {
        var options = new DbContextOptionsBuilder<ArtaFinDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ArtaFinModelOnly;Trusted_Connection=True;")
            .Options;

        using var context = new ArtaFinDbContext(options);
        var entity = context.Model.FindEntityType(typeof(LeaseContract));

        Assert.NotNull(entity);
        Assert.Equal("LeaseContracts", entity!.GetTableName());

        var table = StoreObjectIdentifier.Table("LeaseContracts", null);
        Assert.Equal("Currency", entity.FindProperty(nameof(LeaseContract.InitialLiabilityCurrency))!
            .GetColumnName(table));
        Assert.Equal("InitialLiabilityAmount", entity.FindProperty(nameof(LeaseContract.InitialLiabilityAmount))!
            .GetColumnName(table));

        var accountForeignKeys = entity.GetForeignKeys()
            .Where(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Account))
            .ToArray();

        Assert.Equal(3, accountForeignKeys.Length);
        Assert.All(accountForeignKeys, foreignKey =>
        {
            Assert.Equal(
                new[] { nameof(LeaseContract.FinancialBookId), foreignKey.Properties[1].Name },
                foreignKey.Properties.Select(property => property.Name));
            Assert.Equal(
                new[] { nameof(Account.FinancialBookId), nameof(Account.Id) },
                foreignKey.PrincipalKey.Properties.Select(property => property.Name));
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        });
    }
}
