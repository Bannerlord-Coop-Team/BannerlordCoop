using System;

namespace Missions.Agents.Handlers;

public interface IMovementTrafficBudgetFactory
{
    IMovementTrafficBudget Create(double bytesPerSecond, int burstBytes);
}

/// <summary>Creates independent token buckets for global and per-recipient movement limits.</summary>
public sealed class MovementTrafficBudgetFactory : IMovementTrafficBudgetFactory
{
    public IMovementTrafficBudget Create(double bytesPerSecond, int burstBytes) =>
        new MovementTrafficBudget(bytesPerSecond, burstBytes);
}

/// <summary>Compatibility factory for focused tests that supply per-recipient budgets.</summary>
internal sealed class DelegateMovementTrafficBudgetFactory : IMovementTrafficBudgetFactory
{
    private readonly Func<IMovementTrafficBudget> factory;
    private bool createUnboundedGlobal = true;

    public DelegateMovementTrafficBudgetFactory(Func<IMovementTrafficBudget> factory)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        this.factory = factory;
    }

    public IMovementTrafficBudget Create(double bytesPerSecond, int burstBytes)
    {
        // Compatibility seam for focused sender tests that supply only per-recipient budgets.
        if (createUnboundedGlobal)
        {
            createUnboundedGlobal = false;
            return new MovementTrafficBudget(int.MaxValue, int.MaxValue);
        }

        return factory();
    }
}
