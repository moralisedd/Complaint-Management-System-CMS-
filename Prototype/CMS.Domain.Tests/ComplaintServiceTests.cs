using CMS.Application.Complaints;
using CMS.Domain.Entities;
using CMS.Domain.Interfaces;
using CMS.Domain.Strategies;
using CMS.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace CMS.Domain.Tests;

// I'm testing ComplaintService in isolation here — all its dependencies are mocked
// so I can focus purely on the orchestration logic without needing a database or Keycloak.
public sealed class ComplaintServiceTests
{
    // -----------------------------------------------------------------------
    // Shared setup helpers
    // -----------------------------------------------------------------------

    // Build a ComplaintService with the standard mocks pre-wired.
    // Individual tests can override specific mock behaviours as needed.
    private static (
        ComplaintService service,
        Mock<IComplaintRepository> complaintRepo,
        Mock<ITenantIndustryLookup> industryLookup)
    BuildService(string industryKey = "Banking")
    {
        var complaintRepo  = new Mock<IComplaintRepository>();
        var industryLookup = new Mock<ITenantIndustryLookup>();

        industryLookup
            .Setup(x => x.GetIndustryKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(industryKey);

        // Default: AddAsync and UpdateAsync succeed silently.
        complaintRepo
            .Setup(x => x.AddAsync(It.IsAny<Complaint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        complaintRepo
            .Setup(x => x.UpdateAsync(It.IsAny<Complaint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var strategies  = new IResolutionStrategy[] { new BankResolutionStrategy(), new TelecomResolutionStrategy() };
        var factory     = new ResolutionStrategyFactory(strategies);
        var service     = new ComplaintService(complaintRepo.Object, factory, industryLookup.Object);

        return (service, complaintRepo, industryLookup);
    }

    private static LogComplaintCommand BuildCommand(
        string tenantId   = "natwest",
        string subject    = "Card declined at point of sale",
        string desc       = "I tried to pay at Tesco and my card was declined despite having funds.",
        string userId     = "user-consumer-001",
        ContactChannel ch = ContactChannel.Mobile)
        => new(tenantId, subject, desc, ch, userId);

    // -----------------------------------------------------------------------
    // LogComplaintAsync — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LogComplaint_WithValidCommand_ReturnsOkResult()
    {
        var (service, _, _) = BuildService();
        var cmd = BuildCommand();

        var result = await service.LogComplaintAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ComplaintId.Should().NotBe(Guid.Empty);
        result.Value.ReferenceNumber.Should().StartWith("CMS-");
    }

    [Fact]
    public async Task LogComplaint_WithValidCommand_PersistsComplaintExactlyOnce()
    {
        var (service, repo, _) = BuildService();
        var cmd = BuildCommand();

        await service.LogComplaintAsync(cmd);

        // I want to make sure AddAsync was called exactly once with a Complaint
        // that has the right tenant and subject.
        repo.Verify(
            x => x.AddAsync(
                It.Is<Complaint>(c => c.TenantId == "natwest" && c.Subject == cmd.Subject),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogComplaint_ForBankingTenant_UsesCorrectIndustryKey()
    {
        var (service, _, lookup) = BuildService(industryKey: "Banking");
        var cmd = BuildCommand(tenantId: "natwest");

        await service.LogComplaintAsync(cmd);

        // The service must ask the lookup what industry this tenant is in
        // before it can pick the right resolution strategy.
        lookup.Verify(
            x => x.GetIndustryKeyAsync("natwest", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogComplaint_ForTelecomTenant_UsesCorrectIndustryKey()
    {
        var (service, _, lookup) = BuildService(industryKey: "Telecom");
        var cmd = BuildCommand(tenantId: "o2");

        await service.LogComplaintAsync(cmd);

        lookup.Verify(
            x => x.GetIndustryKeyAsync("o2", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null!)]
    public async Task LogComplaint_WithEmptyTenantId_ReturnsFailResult(string? tenantId)
    {
        var (service, _, _) = BuildService();
        // The domain Complaint.Create() throws ArgumentException for blank tenantId —
        // ComplaintService should catch it and return a Fail result.
        var cmd = BuildCommand(tenantId: tenantId!);

        var result = await service.LogComplaintAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Validation error");
    }

    [Fact]
    public async Task LogComplaint_WithSubjectOver120Chars_ReturnsFailResult()
    {
        var (service, _, _) = BuildService();
        var cmd = BuildCommand(subject: new string('x', 121));

        var result = await service.LogComplaintAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Validation error");
    }

    [Fact]
    public async Task LogComplaint_WithDescriptionOver2000Chars_ReturnsFailResult()
    {
        var (service, _, _) = BuildService();
        var cmd = BuildCommand(desc: new string('y', 2001));

        var result = await service.LogComplaintAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Validation error");
    }

    // -----------------------------------------------------------------------
    // GetComplaintAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetComplaint_WhenExists_ReturnsSummaryWithCorrectId()
    {
        var (service, repo, _) = BuildService();
        var complaint = MakeComplaint("natwest");

        repo.Setup(x => x.GetByIdAsync(complaint.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(complaint);

        var result = await service.GetComplaintAsync(complaint.Id, "natwest");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(complaint.Id);
        result.Value.LoggedByUserId.Should().Be("user-natwest-001");
    }

    [Fact]
    public async Task GetComplaint_WhenNotFound_ReturnsFailResult()
    {
        var (service, repo, _) = BuildService();
        var unknownId = Guid.NewGuid();

        repo.Setup(x => x.GetByIdAsync(unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Complaint?)null);

        var result = await service.GetComplaintAsync(unknownId, "natwest");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetComplaint_WhenTenantMismatch_ReturnsFailResult()
    {
        // A NatWest complaint should never be visible to an O2 user —
        // the application-layer double-check enforces this even if the EF filter slips.
        var (service, repo, _) = BuildService();
        var complaint = MakeComplaint("natwest");

        repo.Setup(x => x.GetByIdAsync(complaint.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(complaint);

        var result = await service.GetComplaintAsync(complaint.Id, tenantId: "o2");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    // -----------------------------------------------------------------------
    // GetComplaintsByTenantAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetComplaintsByTenant_WhenThreeExist_ReturnsThreeSummaries()
    {
        var (service, repo, _) = BuildService();
        var complaints = new List<Complaint>
        {
            MakeComplaint("natwest"),
            MakeComplaint("natwest"),
            MakeComplaint("natwest")
        };

        repo.Setup(x => x.GetByTenantAsync("natwest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(complaints);

        var result = await service.GetComplaintsByTenantAsync("natwest");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetComplaintsByTenant_WhenNoneExist_ReturnsEmptyList()
    {
        var (service, repo, _) = BuildService();

        repo.Setup(x => x.GetByTenantAsync("natwest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Complaint>());

        var result = await service.GetComplaintsByTenantAsync("natwest");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Complaint MakeComplaint(string tenantId, string subject = "Test subject")
        => Complaint.Create(tenantId, subject,
            "Detailed description of the issue with enough context.",
            ContactChannel.Web, $"user-{tenantId}-001");
}
