using CMS.Domain.Entities;

namespace CMS.Domain.Strategies;

// Telecom strategy — applied to O2 tenants.
// I modelled the Ofcom ADR rules: if we can't resolve within 8 weeks,
// the customer can refer the complaint to an Ofcom-approved ADR scheme.
public sealed class TelecomResolutionStrategy : IResolutionStrategy
{
    public string StrategyKey => "Telecom";

    // Ofcom 8-week deadlock period before ADR referral
    public int ResolutionDeadlineDays => 56;

    public void ValidateComplaint(Complaint complaint)
    {
        // Ofcom requires a description so we can identify the service type.
        if (string.IsNullOrWhiteSpace(complaint.Description))
            throw new InvalidOperationException("[Telecom] A description is required under Ofcom ADR rules.");
    }

    public string GetAcknowledgementMessage(Complaint complaint) =>
        $"Thank you for contacting us (Ref: {complaint.Id}). " +
        $"If we can't resolve your complaint within 8 weeks, you can refer it to " +
        $"an Alternative Dispute Resolution scheme approved by Ofcom.";
}
