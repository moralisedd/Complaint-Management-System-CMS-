using CMS.Application.Support;
using CMS.Domain.Entities;
using CMS.Domain.Interfaces;
using CMS.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace CMS.Domain.Tests;

// Tests for the Assign Support Person use case (UC02 / FR2).
// I'm testing the service layer here — all repositories are mocked so I'm
// only exercising the orchestration logic, not the database.
public sealed class SupportAssignmentServiceTests
{
    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------

    private static (
        SupportAssignmentService service,
        Mock<IComplaintRepository> complaintRepo,
        Mock<ISupportPersonRepository> supportPersonRepo)
    BuildService()
    {
        var complaintRepo     = new Mock<IComplaintRepository>();
        var supportPersonRepo = new Mock<ISupportPersonRepository>();

        complaintRepo
            .Setup(x => x.UpdateAsync(It.IsAny<Complaint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new SupportAssignmentService(
            complaintRepo.Object,
            supportPersonRepo.Object);

        return (service, complaintRepo, supportPersonRepo);
    }

    // A complaint that is Open and belongs to the given tenant.
    private static Complaint OpenComplaint(string tenantId = "natwest")
        => Complaint.Create(tenantId, "Test complaint subject",
            "Detailed description of the issue.", ContactChannel.Web,
            $"consumer-{tenantId}");

    // A support person who is active for the given tenant.
    private static SupportPerson ActiveSupportPerson(string tenantId = "natwest")
        => new()
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            DisplayName = "Sarah Lee",
            Email       = "sarah.lee@example.com",
            IsActive    = true
        };

    // -----------------------------------------------------------------------
    // AssignAsync — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Assign_WithValidIds_ReturnsOkResult()
    {
        var (service, complaintRepo, spRepo) = BuildService();
        var complaint = OpenComplaint("natwest");
        var sp        = ActiveSupportPerson("natwest");

        complaintRepo.Setup(x => x.GetByIdAsync(complaint.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(complaint);

        spRepo.Setup(x => x.GetByIdAsync(sp.Id, "natwest", It.IsAny<CancellationToken>()))
              .ReturnsAsync(sp);

        var cmd    = new AssignSupportCommand(complaint.Id, sp.Id, "natwest", "agent-001");
        var result = await service.AssignAsync(cmd);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Assign_WithValidIds_PersistsUpdateExactlyOnce()
    {
        var (service, complaintRepo, spRepo) = BuildService();
        var complaint = OpenComplaint("natwest");
        var sp        = ActiveSupportPerson("natwest");

        complaintRepo.Setup(x => x.GetByIdAsync(complaint.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(complaint);
        spRepo.Setup(x => x.GetByIdAsync(sp.Id, "natwest", It.IsAny<CancellationToken>()))
              .ReturnsAsync(sp);

        await service.AssignAsync(new AssignSupportCommand(complaint.Id, sp.Id, "natwest", "agent-001"));

        complaintRepo.Verify(
            x => x.UpdateAsync(
                It.Is<Complaint>(c => c.Id == complaint.Id && c.Status == ComplaintStatus.InProgress),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // AssignAsync — complaint not found
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Assign_WhenComplaintNotFound_ReturnsFailResult()
    {
        var (service, complaintRepo, _) = BuildService();
        var missingId = Guid.NewGuid();

        complaintRepo.Setup(x => x.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync((Complaint?)null);

        var cmd    = new AssignSupportCommand(missingId, Guid.NewGuid(), "natwest", "agent-001");
        var result = await service.AssignAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Assign_WhenComplaintNotFound_NeverCallsUpdateAsync()
    {
        var (service, complaintRepo, _) = BuildService();

        complaintRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((Complaint?)null);

        await service.AssignAsync(new AssignSupportCommand(Guid.NewGuid(), Guid.NewGuid(), "natwest", "agent-001"));

        complaintRepo.Verify(
            x => x.UpdateAsync(It.IsAny<Complaint>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -----------------------------------------------------------------------
    // AssignAsync — tenant mismatch (cross-tenant isolation check)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Assign_WhenTenantMismatch_ReturnsAccessDenied()
    {
        // An O2 agent should never be able to assign a NatWest complaint.
        // This is a defence-in-depth check on top of the EF query filter.
        var (service, complaintRepo, _) = BuildService();
        var natwestComplaint = OpenComplaint("natwest");

        complaintRepo.Setup(x => x.GetByIdAsync(natwestComplaint.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(natwestComplaint);

        // Command uses tenantId = "o2" but the complaint belongs to "natwest".
        var cmd    = new AssignSupportCommand(natwestComplaint.Id, Guid.NewGuid(), "o2", "agent-o2");
        var result = await service.AssignAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Access denied");
    }

    // -----------------------------------------------------------------------
    // AssignAsync — support person not found
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Assign_WhenSupportPersonNotFound_ReturnsFailResult()
    {
        var (service, complaintRepo, spRepo) = BuildService();
        var complaint    = OpenComplaint("natwest");
        var unknownSpId  = Guid.NewGuid();

        complaintRepo.Setup(x => x.GetByIdAsync(complaint.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(complaint);

        spRepo.Setup(x => x.GetByIdAsync(unknownSpId, "natwest", It.IsAny<CancellationToken>()))
              .ReturnsAsync((SupportPerson?)null);

        var cmd    = new AssignSupportCommand(complaint.Id, unknownSpId, "natwest", "agent-001");
        var result = await service.AssignAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    // -----------------------------------------------------------------------
    // AssignAsync — domain business rule: can only assign an Open complaint
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Assign_WhenComplaintAlreadyInProgress_ReturnsFailResult()
    {
        // The domain Complaint.AssignTo() throws InvalidOperationException
        // if the complaint is not Open — the service should return Fail, not propagate.
        var (service, complaintRepo, spRepo) = BuildService();
        var complaint = OpenComplaint("natwest");
        var sp        = ActiveSupportPerson("natwest");

        // Assign once to put it into InProgress state.
        complaint.AssignTo(sp.Id.ToString());

        complaintRepo.Setup(x => x.GetByIdAsync(complaint.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(complaint);

        // Trying to re-assign a different support person.
        var sp2 = ActiveSupportPerson("natwest");
        spRepo.Setup(x => x.GetByIdAsync(sp2.Id, "natwest", It.IsAny<CancellationToken>()))
              .ReturnsAsync(sp2);

        var cmd    = new AssignSupportCommand(complaint.Id, sp2.Id, "natwest", "agent-001");
        var result = await service.AssignAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Open");
    }

    // -----------------------------------------------------------------------
    // GetAvailableSupportPersonsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAvailableSupportPersons_ReturnsTenantScopedList()
    {
        var (service, _, spRepo) = BuildService();
        var people = new List<SupportPerson>
        {
            ActiveSupportPerson("natwest"),
            ActiveSupportPerson("natwest"),
            ActiveSupportPerson("natwest")
        };

        spRepo.Setup(x => x.GetActiveSupportPersonsAsync("natwest", It.IsAny<CancellationToken>()))
              .ReturnsAsync(people);

        var result = await service.GetAvailableSupportPersonsAsync("natwest");

        result.Should().HaveCount(3);
        result.Should().AllSatisfy(p => p.TenantId.Should().Be("natwest"));
    }

    [Fact]
    public async Task GetAvailableSupportPersons_DoesNotReturnOtherTenantPeople()
    {
        var (service, _, spRepo) = BuildService();

        // The O2 query returns its own people — NatWest agents should never appear.
        spRepo.Setup(x => x.GetActiveSupportPersonsAsync("o2", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<SupportPerson> { ActiveSupportPerson("o2") });

        spRepo.Setup(x => x.GetActiveSupportPersonsAsync("natwest", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<SupportPerson> { ActiveSupportPerson("natwest") });

        var o2Result = await service.GetAvailableSupportPersonsAsync("o2");

        o2Result.Should().HaveCount(1);
        o2Result.Single().TenantId.Should().Be("o2");
    }
}
