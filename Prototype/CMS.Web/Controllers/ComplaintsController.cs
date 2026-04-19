using CMS.Application.Complaints;
using CMS.Application.Support;
using CMS.Domain.Interfaces;
using CMS.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Web.Controllers;

/// <summary>
/// REST API endpoints for complaint operations (FR1, FR2).
/// Consumes JWT Bearer tokens issued by Keycloak (ADR-04).
/// Tenant isolation is enforced by TenantResolverMiddleware + EF global query filters (ADR-07).
/// </summary>
[ApiController]
[Route("api/v1/complaints")]
public sealed class ComplaintsController : ControllerBase
{
    private readonly IComplaintService _complaintService;
    private readonly ISupportAssignmentService _assignmentService;
    private readonly ITenantContext _tenantContext;

    public ComplaintsController(
        IComplaintService complaintService,
        ISupportAssignmentService assignmentService,
        ITenantContext tenantContext)
    {
        _complaintService = complaintService;
        _assignmentService = assignmentService;
        _tenantContext = tenantContext;
    }

    // -------------------------------------------------------------------------
    // FR1 / UC01 — POST /api/v1/complaints
    // -------------------------------------------------------------------------

    /// <summary>Logs a new complaint for the authenticated Consumer.</summary>
    /// <response code="201">Complaint created. Returns complaintId and referenceNumber.</response>
    /// <response code="400">Validation failure (subject/description/channel invalid).</response>
    [HttpPost]
    [Authorize(Policy = "CanLogComplaint")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LogComplaint(
        [FromBody] LogComplaintRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirst("sub")?.Value
                  ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? "unknown";

        var command = new LogComplaintCommand(
            TenantId: _tenantContext.TenantId,
            Subject: request.Subject,
            Description: request.Description,
            Channel: request.Channel,
            LoggedByUserId: userId);

        var result = await _complaintService.LogComplaintAsync(command, ct);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(
            nameof(LogComplaint),
            new
            {
                complaintId = result.Value!.ComplaintId,
                referenceNumber = result.Value.ReferenceNumber
            });
    }

    // -------------------------------------------------------------------------
    // FR2 / UC02 — PATCH /api/v1/complaints/{id}/assign
    // -------------------------------------------------------------------------

    /// <summary>Assigns a support person to an Open complaint (transitions → InProgress).</summary>
    /// <response code="204">Assignment successful.</response>
    /// <response code="400">Complaint is not in Open status or other business rule violation.</response>
    /// <response code="404">Complaint or support person not found in the current tenant.</response>
    [HttpPatch("{id:guid}/assign")]
    [Authorize(Policy = "CanAssignSupport")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignSupport(
        Guid id,
        [FromBody] AssignSupportRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var actorId = User.FindFirst("sub")?.Value
                   ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                   ?? "unknown";

        var command = new AssignSupportCommand(
            ComplaintId: id,
            SupportPersonId: request.SupportPersonId,
            TenantId: _tenantContext.TenantId,
            ActorUserId: actorId);

        var result = await _assignmentService.AssignAsync(command, ct);

        if (result.IsFailure)
        {
            // "not found" errors come through as failures with descriptive messages
            if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { error = result.Error });

            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }
}

// ---------------------------------------------------------------------------
// Request DTOs (internal to Web layer — not shared with Application)
// ---------------------------------------------------------------------------

/// <summary>Request body for POST /api/v1/complaints (FR1).</summary>
// Property-style record (not positional) so ASP.NET Core model binding can construct
// the instance via the parameterless constructor before populating properties.
public sealed record LogComplaintRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(120, MinimumLength = 1)]
    public string Subject { get; init; } = default!;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(2000, MinimumLength = 1)]
    public string Description { get; init; } = default!;

    public ContactChannel Channel { get; init; }
}

/// <summary>Request body for PATCH /api/v1/complaints/{id}/assign (FR2).</summary>
public sealed record AssignSupportRequest
{
    public Guid SupportPersonId { get; init; }
}
