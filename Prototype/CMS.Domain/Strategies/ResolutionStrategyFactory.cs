namespace CMS.Domain.Strategies;

// I used the Strategy pattern here to handle the fact that different tenants
// operate under different regulations. The factory picks the right strategy
// based on the tenant's industry type (Banking or Telecom).
// Adding a new industry just means registering a new IResolutionStrategy — nothing else changes.
public sealed class ResolutionStrategyFactory
{
    private readonly IReadOnlyDictionary<string, IResolutionStrategy> _strategies;

    // DI injects all registered IResolutionStrategy implementations at once.
    // I index them by their StrategyKey for fast lookup at runtime.
    public ResolutionStrategyFactory(IEnumerable<IResolutionStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.StrategyKey, StringComparer.OrdinalIgnoreCase);
    }

    public IResolutionStrategy GetStrategy(string industryKey)
    {
        if (_strategies.TryGetValue(industryKey, out var strategy))
            return strategy;

        throw new InvalidOperationException(
            $"No strategy registered for industry '{industryKey}'. " +
            $"Registered: {string.Join(", ", _strategies.Keys)}");
    }
}
