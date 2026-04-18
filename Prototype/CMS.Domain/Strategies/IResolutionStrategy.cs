using CMS.Domain.Entities;

namespace CMS.Domain.Strategies;

/// <summary>
/// Strategy interface for industry-specific complaint resolution rules (ADR-03).
/// The Strategy design pattern enables tenant-specific resolution behaviour
/// without modifying the core ComplaintService.
/// Implementations: BankResolutionStrategy, TelecomResolutionStrategy.
/// </summary>
public interface IResolutionStrategy
{
    /// <summary>Unique identifier for this strategy — matches tenant industry type.</summary>
    string StrategyKey { get; }

    /// <summary>
    /// Returns the maximum allowed resolution days for this industry (e.g. FCA: 8 weeks = 56 days).
    /// Used to set the SLA deadline on a newly-logged complaint.
    /// </summary>
    int ResolutionDeadlineDays { get; }

    /// <summary>
    /// Applies industry-specific validation rules before the complaint is persisted.
    /// Throws <see cref="InvalidOperationException"/> if the complaint violates industry rules.
    /// </summary>
    void ValidateComplaint(Complaint complaint);

    /// <summary>
    /// Returns the acknowledgement message to include in the consumer notification email/SMS.
    /// </summary>
    string GetAcknowledgementMessage(Complaint complaint);
}
