using System;
using System.Threading;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Exposes the weapon and originating peer of a routed blow while the victim owner re-applies it through
/// native damage, so reward and sound code can read what the native call does not carry.
/// </summary>
public interface IRoutedBlowTracker
{
    /// <summary>The routed blow's attacker weapon on the calling thread, or null outside a routed blow.</summary>
    WeaponComponentData AttackerWeapon { get; }

    /// <summary>The controller id of the peer that routed the blow on the calling thread, or null outside a routed blow.</summary>
    string SourceControllerId { get; }

    /// <summary>Opens a routed blow scope on the calling thread; disposing it restores the previous blow.</summary>
    IDisposable BeginRoutedBlow(WeaponComponentData attackerWeapon, string sourceControllerId);
}

/// <inheritdoc cref="IRoutedBlowTracker"/>
public class RoutedBlowTracker : IRoutedBlowTracker, IDisposable
{
    // Per thread because the scope wraps a single synchronous Agent.RegisterBlow call on the game thread.
    private readonly ThreadLocal<RoutedBlow> current = new ThreadLocal<RoutedBlow>();

    /// <inheritdoc/>
    public WeaponComponentData AttackerWeapon => current.Value?.AttackerWeapon;

    /// <inheritdoc/>
    public string SourceControllerId => current.Value?.SourceControllerId;

    /// <inheritdoc/>
    public IDisposable BeginRoutedBlow(WeaponComponentData attackerWeapon, string sourceControllerId)
    {
        return new ThreadLocalValueScope<RoutedBlow>(current, new RoutedBlow(attackerWeapon, sourceControllerId));
    }

    /// <summary>Releases the per-thread slot when the owning container is disposed.</summary>
    public void Dispose() => current.Dispose();

    /// <summary>The weapon and source peer of one routed blow.</summary>
    private sealed class RoutedBlow
    {
        // Captures the routed blow's weapon and the peer it came from.
        public RoutedBlow(WeaponComponentData attackerWeapon, string sourceControllerId)
        {
            AttackerWeapon = attackerWeapon;
            SourceControllerId = sourceControllerId;
        }

        public WeaponComponentData AttackerWeapon { get; }
        public string SourceControllerId { get; }
    }
}
