using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Holds the live coop battle's <see cref="IBattleAgentAuthority"/> for its lifetime and forwards questions to it.
/// Every answer is null while no battle is attached.
/// </summary>
public interface IBattleAgentAuthorityRegistry : IBattleAgentAuthority
{
    /// <summary>Makes <paramref name="authority"/> the live battle's answers, replacing any earlier one.</summary>
    void Attach(IBattleAgentAuthority authority);

    /// <summary>Clears the live answers only if <paramref name="authority"/> still owns them.</summary>
    void Detach(IBattleAgentAuthority authority);
}

/// <inheritdoc cref="IBattleAgentAuthorityRegistry"/>
public class BattleAgentAuthorityRegistry : IBattleAgentAuthorityRegistry
{
    private readonly object sync = new();
    private IBattleAgentAuthority current;

    /// <inheritdoc/>
    public void Attach(IBattleAgentAuthority authority)
    {
        lock (sync)
        {
            current = authority;
        }
    }

    /// <inheritdoc/>
    // Keyed so a stale battle disposed after a newer one attached cannot clear the newer one's answers.
    public void Detach(IBattleAgentAuthority authority)
    {
        lock (sync)
        {
            if (!ReferenceEquals(current, authority)) return;

            current = null;
        }
    }

    /// <inheritdoc/>
    public bool? IsMountRemotelyOwned(Agent mount) => GetCurrent()?.IsMountRemotelyOwned(mount);

    /// <inheritdoc/>
    public bool? IsHeroAgentLocallyOwned(Hero hero) => GetCurrent()?.IsHeroAgentLocallyOwned(hero);

    // Reads the attached authority under the lock, so the call into it runs outside the lock.
    private IBattleAgentAuthority GetCurrent()
    {
        lock (sync)
        {
            return current;
        }
    }
}
