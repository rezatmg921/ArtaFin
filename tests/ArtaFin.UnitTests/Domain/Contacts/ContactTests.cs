using ArtaFin.Domain.Common;
using ArtaFin.Domain.Contacts;
using Xunit;

namespace ArtaFin.UnitTests.Domain.Contacts;

public sealed class ContactTests
{
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    private static Contact Create(
        string name = "  Acme Ltd  ",
        ContactType type = ContactType.Organization,
        string? email = " SALES@ACME.COM ",
        string? phone = " +98 (21) 1234-5678 ",
        string? notes = "  Supplier  ") =>
        Contact.Create(Guid.NewGuid(), BookId, type, name, email, phone, notes, Now);

    [Fact]
    public void Create_NormalizesOptionalFieldsAndDisplayName()
    {
        var contact = Create();

        Assert.Equal(BookId, contact.FinancialBookId);
        Assert.Equal(ContactType.Organization, contact.Type);
        Assert.Equal("Acme Ltd", contact.DisplayName);
        Assert.Equal("sales@acme.com", contact.Email);
        Assert.Equal("+98 (21) 1234-5678", contact.Phone);
        Assert.Equal("Supplier", contact.Notes);
        Assert.Equal(Now, contact.CreatedAt);
        Assert.Equal(Now, contact.UpdatedAt);
        Assert.False(contact.IsArchived);
    }

    [Fact]
    public void Create_AllowsOmittedOptionalFields()
    {
        var contact = Create(email: null, phone: null, notes: null);

        Assert.Null(contact.Email);
        Assert.Null(contact.Phone);
        Assert.Null(contact.Notes);
    }

    [Fact]
    public void Create_RejectsEmptyFinancialBookId() =>
        Assert.Throws<ArgumentException>(() => Contact.Create(
            Guid.NewGuid(), Guid.Empty, ContactType.Person, "Name", null, null, null, Now));

    [Fact]
    public void Create_RejectsUndefinedContactType() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Contact.Create(
            Guid.NewGuid(), BookId, (ContactType)99, "Name", null, null, null, Now));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsMissingDisplayName(string name) =>
        Assert.Throws<ArgumentException>(() => Contact.Create(
            Guid.NewGuid(), BookId, ContactType.Person, name, null, null, null, Now));

    [Fact]
    public void Create_RejectsDisplayNameLongerThanLimit() =>
        Assert.Throws<ArgumentException>(() => Contact.Create(
            Guid.NewGuid(), BookId, ContactType.Person,
            new string('x', Contact.MaxDisplayNameLength + 1), null, null, null, Now));

    [Fact]
    public void Create_RejectsControlCharactersInDisplayName() =>
        Assert.Throws<ArgumentException>(() => Contact.Create(
            Guid.NewGuid(), BookId, ContactType.Person, "Bad\nName", null, null, null, Now));

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@@example.com")]
    public void Create_RejectsInvalidEmail(string email) =>
        Assert.Throws<ArgumentException>(() => Contact.Create(
            Guid.NewGuid(), BookId, ContactType.Person, "Name", email, null, null, Now));

    [Fact]
    public void Create_RejectsUnsupportedPhoneCharacters() =>
        Assert.Throws<ArgumentException>(() => Contact.Create(
            Guid.NewGuid(), BookId, ContactType.Person, "Name", null, "123 ext. 4", null, Now));

    [Fact]
    public void UpdateDetails_ChangesContactAndTimestamp()
    {
        var contact = Create();
        var updatedAt = Now.AddDays(1);

        contact.UpdateDetails(ContactType.Person, "Jane Doe", "JANE@EXAMPLE.COM",
            "+1 555 0100", "Customer", updatedAt);

        Assert.Equal(ContactType.Person, contact.Type);
        Assert.Equal("Jane Doe", contact.DisplayName);
        Assert.Equal("jane@example.com", contact.Email);
        Assert.Equal("+1 555 0100", contact.Phone);
        Assert.Equal("Customer", contact.Notes);
        Assert.Equal(updatedAt, contact.UpdatedAt);
    }

    [Fact]
    public void Archive_SetsArchiveTimestampAndPreventsFurtherChanges()
    {
        var contact = Create();
        var archivedAt = Now.AddDays(1);

        contact.Archive(archivedAt);

        Assert.True(contact.IsArchived);
        Assert.Equal(archivedAt, contact.ArchivedAt);
        Assert.Throws<DomainException>(() => contact.UpdateDetails(
            ContactType.Person, "Changed", null, null, null, archivedAt.AddDays(1)));
        Assert.Throws<DomainException>(() => contact.Archive(archivedAt.AddDays(1)));
    }

    [Fact]
    public void Archive_RejectsTimestampBeforeCreation()
    {
        var contact = Create();

        Assert.Throws<DomainException>(() => contact.Archive(Now.AddTicks(-1)));
        Assert.False(contact.IsArchived);
    }
}
