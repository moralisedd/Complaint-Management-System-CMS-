using CMS.Domain.Entities;

namespace CMS.Domain.Strategies;

// Banking strategy — applied to NatWest tenants.
// I modelled the FCA DISP rules here: complaints need an acknowledgement
// within 3 business days and a final response within 8 weeks (56 days).
public sealed class BankResolutionStrategy : IResolutionStrategy
{
    public string StrategyKey => "Banking";

    // FCA DISP 1.6 deadline
    public int ResolutionDeadlineDays => 56;

    public void ValidateComplaint(Complaint complaint)
    {
        // FCA rules require a meaningful subject — can't log a blank complaint.
        if (string.IsNullOrWhiteSpace(complaint.Subject))
            throw new InvalidOperationException("[Banking] Complaint subject is required under FCA DISP rules.");
    }

    public string GetAcknowledgementMessage(Complaint complaint) =>
        $"Thank you for your complaint (Ref: {complaint.Id}). " +
        $"We are required by the Financial Conduct Authority to acknowledge within 3 business days " +
        $"and provide a final response within 8 weeks.";
}
