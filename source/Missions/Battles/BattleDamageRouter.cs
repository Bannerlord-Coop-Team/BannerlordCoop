using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using Missions.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Combat damage routing for a coop battle: a local troop hitting a puppet is suppressed locally
/// (<c>BattleBlowInterceptPatch</c>) and routed to the puppet's owner, which applies it authoritatively — so
/// each agent's life/death is decided on exactly one client and the battles don't diverge.
/// </summary>
public interface IBattleDamageRouter : IDisposable
{
}

/// <inheritdoc cref="IBattleDamageRouter"/>
public class BattleDamageRouter : IBattleDamageRouter
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleDamageRouter>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;

    public BattleDamageRouter(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;

        messageBroker.Subscribe<BattlePuppetHit>(Handle_BattlePuppetHit);
        messageBroker.Subscribe<NetworkApplyBattleDamage>(Handle_NetworkApplyBattleDamage);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BattlePuppetHit>(Handle_BattlePuppetHit);
        messageBroker.Unsubscribe<NetworkApplyBattleDamage>(Handle_NetworkApplyBattleDamage);
    }

    // [Attacker's node] A local troop hit a puppet (suppressed locally by BattleBlowInterceptPatch). Route the
    // WHOLE blow to the puppet's owner; only the owner re-applies it. The attacker's network id rides along so
    // the owner can re-map the (per-client) attacker index to its local agent.
    private void Handle_BattlePuppetHit(MessagePayload<BattlePuppetHit> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            if (!registry.TryGetAgentInfo(payload.What.Victim, out var victimInfo))
            {
                Logger.Information("[DeathDiag] Local hit on a puppet that is not in our registry — cannot route it");
                return;
            }

            Guid attackerId = Guid.Empty;
            if (payload.What.Attacker != null && registry.TryGetAgentInfo(payload.What.Attacker, out var attackerInfo))
                attackerId = attackerInfo.AgentId;

            Logger.Information("[DeathDiag] Routing puppet hit to owner {Owner}: victim={Victim}, dmg={Dmg}", victimInfo.CurrentAuthority, victimInfo.AgentId, payload.What.Blow.InflictedDamage);
            network.SendAll(new NetworkApplyBattleDamage(victimInfo.AgentId, attackerId, payload.What.Blow, payload.What.CollisionData));
        });
    }

    // [Owner] Another client's troop hit one of OUR agents. Re-apply the real blow through Agent.RegisterBlow so
    // the engine resolves damage, hit reaction, ragdoll and (if lethal) death — the death then flows through
    // Agent.Die -> BattleAgentDiedPatch -> the normal death/casualty sync. Non-owners ignore it. No synthetic blow.
    private void Handle_NetworkApplyBattleDamage(MessagePayload<NetworkApplyBattleDamage> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            if (!registry.TryGetAgentInfo(payload.What.VictimAgentId, out var info)) return;
            if (info.CurrentAuthority != session.OwnControllerId) return;

            var victim = info.Agent;
            var blow = payload.What.Blow;
            var collisionData = payload.What.CollisionData;
            var attackerId = payload.What.AttackerAgentId;

            if (Mission.Current == null || victim == null || !victim.IsActive() || victim.Health <= 0) return;

            // Re-map the attacker index to OUR local agent (indices are per-client); -1 if not resolvable here.
            if (attackerId != Guid.Empty && registry.TryGetAgentInfo(attackerId, out var attackerInfo) && attackerInfo.Agent != null)
                blow.OwnerId = attackerInfo.Agent.Index;
            else
                blow.OwnerId = -1;

            // Missile blows: the projectile is simulated only on the shooter (missiles aren't synced), so its
            // index is absent from THIS client's _missilesDictionary — Mission.OnAgentHit does
            // _missilesDictionary[index] for a missile blow and throws KeyNotFound. Clear the missile flag (the
            // publicizer exposes the private BlowWeaponRecord._isMissile) and the dangling projectile index so
            // OnAgentHit takes the no-missile path, while keeping the weapon class/flags for the hit reaction.
            // The already-resolved InflictedDamage still lands and the agent dies naturally. (A visible arrow on
            // this client would need real missile sync — a separate feature.)
            bool wasMissile = blow.IsMissile;
            if (wasMissile)
            {
                blow.WeaponRecord._isMissile = false;
                blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = -1;
            }

            // TEMP diagnostic: confirms the routed blow's damage/missile flag per hit. Remove once solid.
            Logger.Information("[BattleSync] Applying routed blow to {Agent}: dmg={Damage}, missile={Missile}, health={Health}",
                victim.Name, blow.InflictedDamage, wasMissile, victim.Health);
            victim.RegisterBlow(blow, in collisionData);

            // A hero's in-mission Agent.Health only propagates to the campaign Hero.HitPoints when the agent is
            // removed (Mission.OnAgentRemoved), so a wounded-but-SURVIVING hero's damage never reaches the server.
            // Mirror the owned hero's post-blow health onto Hero.HitPoints; HeroHitPointsRequestPatch then forwards
            // it to the server. A lethal blow is left to the death path (Agent.Die + the native removal set_HitPoints).
            if (victim.Health > 0 && victim.Character is CharacterObject character && character.IsHero && character.HeroObject is Hero hero)
                hero.HitPoints = Math.Max(1, (int)victim.Health);
        });
    }
}
