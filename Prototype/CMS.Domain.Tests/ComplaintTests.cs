using CMS.Domain.Entities;
using CMS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CMS.Domain.Tests;

// Tests for the Complaint domain entity.
// I'm testing the invariants in Complaint.Create() and Complaint.AssignTo() in
// isolation — no database or DI container needed, just plain C# objects.
public sealed class ComplaintTests
{
    // -----------------------------------------------------------------------
    // Complaint.Create() — UC01
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_WithValidInputs_SetsStatusToOpen()
    {
        // Arrange / Act
        var complaint = Complaint.Create(
            tenantId: "natwest",
            subject: "My card was declined",
            description: "I tried to use my card at Tesco and it was declined despite funds being available.",
            channel: ContactChannel.Mobile,
            loggedByUserId: "user-abc-123");

        // Assert
        complaint.Status.Should().Be(ComplaintStatus.Open);
    }

    [Fact]
    public void Create_WithValidInputs_AssignsNewGuid()
    {
        var c1 = BuildComplaint();
        var c2 = BuildComplaint();

        c1.Id.Should().NotBe(Guid.Empty);
        c1.Id.Should().NotBe(c2.Id, because: "each complaint must have a unique ID");
    }

    [Fact]
    public void Create_WithValidInputs_SetsLoggedAtToUtcNow()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var complaint = BuildComplaint();
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        complaint.LoggedAt.Should().BeAfter(before).And.BeBefore(after);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null!)]
    public void Create_WithEmptyTenantId_ThrowsArgumentException(string? tenantId)
    {
        var act = () => Complaint.Create(tenantId!, "Subject", "Description", ContactChannel.Web, "user-1");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithSubjectOver120Chars_ThrowsArgumentException()
    {
        var longSubject = new string('x', 121);
        var act = () => Complaint.Create("natwest", longSubject, "Description", ContactChannel.Web, "user-1");
        act.Should().Throw<ArgumentException>().WithMessage("*120*");
    }

    [Fact]
    public void Create_WithDescriptionOver2000Chars_ThrowsArgumentException()
    {
        var longDescription = new string('y', 2001);
        var act = () => Complaint.Create("natwest", "Subject", longDescription, ContactChannel.Web, "user-1");
        act.Should().Throw<ArgumentException>().WithMessage("*2000*");
    }

    [Fact]
    public void Create_AssignedToId_IsNull()
    {
        var complaint = BuildComplaint();
        complaint.AssignedToId.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Complaint.AssignTo() — UC02
    // -----------------------------------------------------------------------

    [Fact]
    public void AssignTo_WhenOpen_TransitionsToInProgress()
    {
        var complaint = BuildComplaint();

        complaint.AssignTo("support-person-456");

        complaint.Status.Should().Be(ComplaintStatus.InProgress);
        complaint.AssignedToId.Should().Be("support-person-456");
        complaint.AssignedAt.Should().NotBeNull();
    }

    [Fact]
    public void AssignTo_WhenAlreadyInProgress_ThrowsInvalidOperationException()
    {
        var complaint = BuildComplaint();
        complaint.AssignTo("sp-1");

        var act = () => complaint.AssignTo("sp-2");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Open*");
    }

    [Fact]
    public void AssignTo_WhenResolved_ThrowsInvalidOperationException()
    {
        var complaint = BuildComplaint();
        complaint.AssignTo("sp-1");
        complaint.Resolve("Issue resolved.");

        var act = () => complaint.AssignTo("sp-2");

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null!)]
    public void AssignTo_WithEmptySupportPersonId_ThrowsArgumentException(string? id)
    {
        var complaint = BuildComplaint();
        var act = () => complaint.AssignTo(id!);
        act.Should().Throw<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // Complaint.Resolve() — FR3 (out of PoC scope — tested for completeness)
    // -----------------------------------------------------------------------

    [Fact]
    public void Resolve_WhenInProgress_TransitionsToResolved()
    {
        var complaint = BuildComplaint();
        complaint.AssignTo("sp-1");

        complaint.Resolve("Refund processed.");

        complaint.Status.Should().Be(ComplaintStatus.Resolved);
        complaint.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_WhenOpen_ThrowsInvalidOperationException()
    {
        var complaint = BuildComplaint();
        var act = () => complaint.Resolve("Too early.");
        act.Should().Throw<InvalidOperationException>();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Complaint BuildComplaint(
        string tenantId = "natwest",
        string subject = "Test complaint subject",
        string description = "Test complaint description with sufficient detail.",
        ContactChannel channel = ContactChannel.Web,
        string userId = "test-user-001")
        => Complaint.Create(tenantId, subject, description, channel, userId);
}
