using CMS.Domain.Entities;
using CMS.Domain.Strategies;
using CMS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CMS.Domain.Tests;

/// <summary>
/// Unit tests for the Strategy design pattern (ADR-03).
/// Tests BankResolutionStrategy, TelecomResolutionStrategy, and ResolutionStrategyFactory.
/// </summary>
public sealed class ResolutionStrategyTests
{
    // -----------------------------------------------------------------------
    // BankResolutionStrategy
    // -----------------------------------------------------------------------

    [Fact]
    public void BankStrategy_StrategyKey_IsBanking()
    {
        new BankResolutionStrategy().StrategyKey.Should().Be("Banking");
    }

    [Fact]
    public void BankStrategy_ResolutionDeadlineDays_Is56()
    {
        new BankResolutionStrategy().ResolutionDeadlineDays.Should().Be(56);
    }

    [Fact]
    public void BankStrategy_GetAcknowledgementMessage_ContainsFCAReference()
    {
        var strategy = new BankResolutionStrategy();
        var complaint = BuildComplaint("natwest");

        var msg = strategy.GetAcknowledgementMessage(complaint);

        msg.Should().Contain("Financial Conduct Authority");
        msg.Should().Contain("8 weeks");
        msg.Should().Contain(complaint.Id.ToString());
    }

    [Fact]
    public void BankStrategy_ValidateComplaint_WithValidComplaint_DoesNotThrow()
    {
        var strategy = new BankResolutionStrategy();
        var act = () => strategy.ValidateComplaint(BuildComplaint("natwest"));
        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    // TelecomResolutionStrategy
    // -----------------------------------------------------------------------

    [Fact]
    public void TelecomStrategy_StrategyKey_IsTelecom()
    {
        new TelecomResolutionStrategy().StrategyKey.Should().Be("Telecom");
    }

    [Fact]
    public void TelecomStrategy_ResolutionDeadlineDays_Is56()
    {
        new TelecomResolutionStrategy().ResolutionDeadlineDays.Should().Be(56);
    }

    [Fact]
    public void TelecomStrategy_GetAcknowledgementMessage_ContainsADRReference()
    {
        var strategy = new TelecomResolutionStrategy();
        var complaint = BuildComplaint("o2");

        var msg = strategy.GetAcknowledgementMessage(complaint);

        msg.Should().Contain("Alternative Dispute Resolution");
        msg.Should().Contain("Ofcom");
    }

    // -----------------------------------------------------------------------
    // ResolutionStrategyFactory
    // -----------------------------------------------------------------------

    [Fact]
    public void Factory_GetStrategy_WithBanking_ReturnsBankStrategy()
    {
        var factory = BuildFactory();
        var strategy = factory.GetStrategy("Banking");
        strategy.Should().BeOfType<BankResolutionStrategy>();
    }

    [Fact]
    public void Factory_GetStrategy_WithTelecom_ReturnsTelecomStrategy()
    {
        var factory = BuildFactory();
        var strategy = factory.GetStrategy("Telecom");
        strategy.Should().BeOfType<TelecomResolutionStrategy>();
    }

    [Fact]
    public void Factory_GetStrategy_IsCaseInsensitive()
    {
        var factory = BuildFactory();
        // Both should return successfully without throwing
        factory.GetStrategy("banking").Should().BeOfType<BankResolutionStrategy>();
        factory.GetStrategy("TELECOM").Should().BeOfType<TelecomResolutionStrategy>();
    }

    [Fact]
    public void Factory_GetStrategy_WithUnknownKey_ThrowsInvalidOperationException()
    {
        var factory = BuildFactory();
        var act = () => factory.GetStrategy("Insurance");
        act.Should().Throw<InvalidOperationException>().WithMessage("*Insurance*");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ResolutionStrategyFactory BuildFactory()
        => new(new IResolutionStrategy[] { new BankResolutionStrategy(), new TelecomResolutionStrategy() });

    private static Complaint BuildComplaint(string tenantId)
        => Complaint.Create(tenantId, "Subject", "Description detail here.", ContactChannel.Web, "user-1");
}
