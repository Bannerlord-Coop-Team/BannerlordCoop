#if DEBUG
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using Missions.Agents;
using Missions.Agents.Packets;
using Missions.Battles;
using Missions.Missiles.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Missions.Tournaments;

public interface ITournamentCombatFixture : IDisposable
{
    string Apply(
        NetworkTournamentCombatFixtureCommand command,
        ITournamentMissionSession session,
        TournamentSessionSnapshot snapshot,
        TournamentSpawnManifestData manifest,
        INetworkAgentRegistry agentRegistry,
        IAgentPositionInterpolator agentPositionInterpolator);

    void Tick(float dt, INetworkAgentRegistry agentRegistry);

    string GetState(
        ITournamentMissionSession session,
        INetworkAgentRegistry agentRegistry);
}

public class TournamentCombatFixture : ITournamentCombatFixture
{
    private const float AttackPressSeconds = 0.35f;
    private const float AttackCycleSeconds = 1.5f;
    private const string JavelinItemId = "eastern_javelin_1_t2_blunt";
    private const string ShieldItemId = "eastern_wicker_sparring_shield";
    private const string SwordItemId = "empire_sword_1_t2_blunt";
    private const string PolearmItemId = "empire_lance_1_t3_blunt";
    private const Agent.MovementControlFlag AttackFlags =
        Agent.MovementControlFlag.AttackDown |
        Agent.MovementControlFlag.AttackUp |
        Agent.MovementControlFlag.AttackLeft |
        Agent.MovementControlFlag.AttackRight;

    private readonly IMessageBroker messageBroker;
    private readonly List<AiPauseState> aiPauseStates = new();
    private bool active;
    private bool disposed;
    private string ownControllerId;
    private string playerOneControllerId;
    private string playerTwoControllerId;
    private Guid playerOneAgentId;
    private Guid playerTwoAgentId;
    private Guid aiAgentId;
    private Agent playerOneAgent;
    private MissionWeapon originalWeapon0;
    private MissionWeapon originalWeapon1;
    private MissionWeapon originalPlayerTwoWeapon0;
    private MissionWeapon originalPlayerTwoWeapon1;
    private MissionWeapon originalAiWeapon0;
    private MissionWeapon originalAiWeapon1;
    private AgentEquipmentData originalWieldedEquipment;
    private AgentEquipmentData originalPlayerTwoWieldedEquipment;
    private AgentEquipmentData originalAiWieldedEquipment;
    private Agent.MovementControlFlag originalDefendFlags;
    private Agent.MovementControlFlag originalPlayerTwoMovementFlags;
    private Agent.GuardMode originalGuardMode;
    private AgentControllerType originalAiController;
    private string originalAiAuthority;
    private bool aiAuthorityTransferred;
    private IAgentPositionInterpolator agentPositionInterpolator;
    private Team originalPlayerTwoTeam;
    private Agent originalAiTarget;
    private bool originalAllowAiTicking;
    private Vec3 originalPlayerTwoPosition;
    private Vec3 originalPlayerTwoLookDirection;
    private Vec3 originalAiPosition;
    private Vec3 originalAiLookDirection;
    private float baselineHealth;
    private int baselineShieldHitPoints;
    private int baselineMissileCount;
    private int aiStrikeBaselineShieldHitPoints;
    private int playerStrikeBaselineShieldHitPoints;
    private bool aiStrikeRequested;
    private bool aiStrikeObserved;
    private bool playerStrikeRequested;
    private bool playerStrikeObserved;
    private FixtureStrike activeStrike;
    private bool drivesActiveStrike;
    private bool attackersPositioned;
    private float strikeElapsed;
    private bool javelinRequested;
    private bool javelinVisibleObserved;
    private readonly HashSet<int> mountedPolearmPlayerOneVisualAnimations = new();
    private readonly HashSet<int> mountedPolearmPlayerTwoVisualAnimations = new();
    private bool mountedPolearmGuardRequested;
    private bool mountedPolearmStrikeRequested;
    private bool mountedPolearmReactionObserved;
    private int mountedPolearmGuardActionIndex = -1;
    private int mountedPolearmGuardAnimationIndex = -1;
    private int mountedPolearmReactionActionIndex = -1;
    private int mountedPolearmReactionAnimationIndex = -1;
    private float mountedPolearmBaselineHealth;

    public TournamentCombatFixture(IMessageBroker messageBroker)
    {
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<AgentShoot>(Handle_AgentShoot);
        messageBroker.Subscribe<MissileReconstructed>(Handle_MissileReconstructed);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        messageBroker.Unsubscribe<AgentShoot>(Handle_AgentShoot);
        messageBroker.Unsubscribe<MissileReconstructed>(Handle_MissileReconstructed);
    }

    private void Handle_AgentShoot(MessagePayload<AgentShoot> payload)
    {
        if (!active || !javelinRequested ||
            payload.What.Agent == null ||
            payload.What.Agent != playerOneAgent ||
            payload.What.MissionWeapon.Item?.StringId != JavelinItemId)
            return;

        javelinVisibleObserved = true;
    }

    private void Handle_MissileReconstructed(
        MessagePayload<MissileReconstructed> payload)
    {
        if (!active || !javelinRequested ||
            payload.What.AgentId != playerOneAgentId ||
            payload.What.MissileItemId != JavelinItemId)
            return;

        javelinVisibleObserved = true;
    }

    public string Apply(
        NetworkTournamentCombatFixtureCommand command,
        ITournamentMissionSession session,
        TournamentSessionSnapshot snapshot,
        TournamentSpawnManifestData manifest,
        INetworkAgentRegistry agentRegistry,
        IAgentPositionInterpolator agentPositionInterpolator)
    {
        if (command == null ||
            session == null ||
            agentRegistry == null ||
            agentPositionInterpolator == null)
            return "Tournament combat fixture command was invalid";

        return command.Action switch
        {
            TournamentCombatFixtureAction.Initialize =>
                Initialize(
                    command,
                    session,
                    snapshot,
                    manifest,
                    agentRegistry,
                    agentPositionInterpolator),
            TournamentCombatFixtureAction.AiShieldStrike =>
                ApplyAiShieldStrike(command, session, snapshot, manifest, agentRegistry),
            TournamentCombatFixtureAction.PlayerShieldStrike =>
                ApplyPlayerShieldStrike(command, session, agentRegistry),
            TournamentCombatFixtureAction.JavelinThrow =>
                ThrowJavelin(command, session, agentRegistry),
            TournamentCombatFixtureAction.MountedPolearmGuard =>
                ApplyMountedPolearmGuard(command, session, agentRegistry),
            TournamentCombatFixtureAction.MountedPolearmStrike =>
                ApplyMountedPolearmStrike(command, session, agentRegistry),
            TournamentCombatFixtureAction.Restore =>
                Restore(session, agentRegistry),
            _ => "Tournament combat fixture command was not recognized"
        };
    }

    public void Tick(float dt, INetworkAgentRegistry agentRegistry)
    {
        if (!active || !TryGetPlayerOneAgent(agentRegistry, out Agent playerOne))
            return;

        if (ownControllerId == playerOneControllerId)
        {
            AgentActionData.ApplyDefendMovementFlags(
                playerOne,
                Agent.MovementControlFlag.DefendBlock | Agent.MovementControlFlag.DefendUp);
            AgentActionData.ApplyGuardState(playerOne, Agent.GuardMode.Up, force: true);
        }
        if (mountedPolearmGuardRequested)
            ObserveMountedPolearmState(agentRegistry, playerOne);

        int shieldHitPoints = GetShieldHitPoints(playerOne);
        if (aiStrikeRequested &&
            shieldHitPoints < aiStrikeBaselineShieldHitPoints)
            aiStrikeObserved = true;
        if (playerStrikeRequested &&
            shieldHitPoints < playerStrikeBaselineShieldHitPoints)
            playerStrikeObserved = true;

        bool strikeObserved =
            activeStrike == FixtureStrike.Ai
                ? aiStrikeObserved
                : activeStrike == FixtureStrike.Player
                    ? playerStrikeObserved
                    : activeStrike == FixtureStrike.Polearm
                        && mountedPolearmReactionObserved;
        if (strikeObserved)
        {
            StopActiveStrike(agentRegistry);
            activeStrike = FixtureStrike.None;
            drivesActiveStrike = false;
        }

        PauseTournamentAi(agentRegistry);
        if (!attackersPositioned)
        {
            PositionAttackers(agentRegistry, playerOne);
            attackersPositioned = true;
        }

        if (!drivesActiveStrike || activeStrike == FixtureStrike.None)
            return;

        strikeElapsed += dt;
        if (activeStrike == FixtureStrike.Ai)
            DriveAiAttack(agentRegistry, playerOne);
        else
            DrivePlayerAttack(agentRegistry);
    }

    public string GetState(
        ITournamentMissionSession session,
        INetworkAgentRegistry agentRegistry)
    {
        string localControllerId = session?.OwnControllerId ?? "none";
        if (!active || !TryGetPlayerOneAgent(agentRegistry, out Agent playerOne))
        {
            return $"fixtureActive=false local={localControllerId} " +
                "Assert-AiShieldBlocked=false Assert-PlayerShieldBlocked=false " +
                "Assert-ShieldBlocked=false Assert-JavelinVisible=false " +
                "Assert-MountedPolearmBlocked=false";
        }

        int shieldHitPoints = GetShieldHitPoints(playerOne);
        int missileCount = Mission.Current?.MissilesList?.Count ?? 0;
        bool healthUnchanged = Math.Abs(playerOne.Health - baselineHealth) < 0.001f;
        bool aiShieldBlocked = aiStrikeRequested && aiStrikeObserved && healthUnchanged;
        bool playerShieldBlocked =
            playerStrikeRequested && playerStrikeObserved && healthUnchanged;
        bool shieldBlocked =
            playerStrikeRequested ? playerShieldBlocked : aiShieldBlocked;
        bool mountedPolearmHealthUnchanged =
            Math.Abs(playerOne.Health - mountedPolearmBaselineHealth) < 0.001f;
        bool mountedPolearmBlocked =
            mountedPolearmStrikeRequested
            && mountedPolearmReactionObserved
            && mountedPolearmHealthUnchanged;
        string playerOneVisualAnimations =
            FormatAnimationIndexes(mountedPolearmPlayerOneVisualAnimations);
        string playerTwoVisualAnimations =
            FormatAnimationIndexes(mountedPolearmPlayerTwoVisualAnimations);
        string playerOneWeapon =
            playerOne.Equipment[EquipmentIndex.Weapon0].Item?.StringId ?? "none";
        bool playerTwoMounted =
            TryGetActiveAgent(agentRegistry, playerTwoAgentId, out Agent playerTwo)
            && playerTwo.HasMount;

        return $"fixtureActive=true local={localControllerId} player1={playerOneControllerId} " +
            $"player2={playerTwoControllerId} health={playerOne.Health:0.##} " +
            $"baselineHealth={baselineHealth:0.##} shieldHp={shieldHitPoints} " +
            $"baselineShieldHp={baselineShieldHitPoints} missileCount={missileCount} " +
            $"baselineMissileCount={baselineMissileCount} aiStrikeBaselineShieldHp={aiStrikeBaselineShieldHitPoints} " +
            $"playerStrikeBaselineShieldHp={playerStrikeBaselineShieldHitPoints} " +
            $"activeStrike={activeStrike} Assert-AiShieldBlocked={aiShieldBlocked} " +
            $"Assert-PlayerShieldBlocked={playerShieldBlocked} " +
            $"Assert-ShieldBlocked={shieldBlocked} Assert-JavelinVisible={javelinVisibleObserved} " +
            $"player1Mounted={playerOne.HasMount} player2Mounted={playerTwoMounted} " +
            $"player1Weapon={playerOneWeapon} mountedPolearmGuardRequested={mountedPolearmGuardRequested} " +
            $"mountedPolearmStrikeRequested={mountedPolearmStrikeRequested} " +
            $"mountedPolearmGuardAction={mountedPolearmGuardActionIndex} " +
            $"mountedPolearmGuardAnimation={mountedPolearmGuardAnimationIndex} " +
            $"mountedPolearmReactionAction={mountedPolearmReactionActionIndex} " +
            $"mountedPolearmReactionAnimation={mountedPolearmReactionAnimationIndex} " +
            $"mountedPolearmPlayer1VisualAnimations={playerOneVisualAnimations} " +
            $"mountedPolearmPlayer2VisualAnimations={playerTwoVisualAnimations} " +
            $"Assert-MountedPolearmBlocked={mountedPolearmBlocked}";
    }

    private string Initialize(
        NetworkTournamentCombatFixtureCommand command,
        ITournamentMissionSession session,
        TournamentSessionSnapshot snapshot,
        TournamentSpawnManifestData manifest,
        INetworkAgentRegistry agentRegistry,
        IAgentPositionInterpolator agentPositionInterpolator)
    {
        if (active)
            return $"Initialize-TournamentCombatFixture: already active on {session.OwnControllerId}";
        if (string.IsNullOrEmpty(command.PlayerOneControllerId) ||
            string.IsNullOrEmpty(command.PlayerTwoControllerId))
            return "Initialize-TournamentCombatFixture: both controller ids are required";
        if (!TryResolveHumanAgent(
                command.PlayerOneControllerId,
                snapshot,
                manifest,
                agentRegistry,
                out TournamentAgentSpawnData playerOneData,
                out Agent playerOne))
            return $"Initialize-TournamentCombatFixture: active fighter {command.PlayerOneControllerId} was not found";
        if (!TryResolvePlayerAgent(
                command.PlayerTwoControllerId,
                snapshot,
                manifest,
                agentRegistry,
                out CoopAgentInfo playerTwoInfo))
            return $"Initialize-TournamentCombatFixture: player opponent {command.PlayerTwoControllerId} was not found";
        if (!TryResolveAiAgent(
                snapshot,
                manifest,
                agentRegistry,
                playerOne,
                out CoopAgentInfo aiInfo))
            return "Initialize-TournamentCombatFixture: tournament AI opponent was not found";
        if (!TryGetFixtureItems(
                out ItemObject javelin,
                out ItemObject shield,
                out ItemObject sword))
            return $"Initialize-TournamentCombatFixture: {JavelinItemId}, {ShieldItemId}, or {SwordItemId} was not found";

        ownControllerId = session.OwnControllerId;
        playerOneControllerId = command.PlayerOneControllerId;
        playerTwoControllerId = command.PlayerTwoControllerId;
        playerOneAgentId = playerOneData.AgentId;
        playerTwoAgentId = playerTwoInfo.AgentId;
        aiAgentId = aiInfo.AgentId;
        playerOneAgent = playerOne;
        originalWeapon0 = playerOne.Equipment[EquipmentIndex.Weapon0];
        originalWeapon1 = playerOne.Equipment[EquipmentIndex.Weapon1];
        originalPlayerTwoWeapon0 = playerTwoInfo.Agent.Equipment[EquipmentIndex.Weapon0];
        originalPlayerTwoWeapon1 = playerTwoInfo.Agent.Equipment[EquipmentIndex.Weapon1];
        originalAiWeapon0 = aiInfo.Agent.Equipment[EquipmentIndex.Weapon0];
        originalAiWeapon1 = aiInfo.Agent.Equipment[EquipmentIndex.Weapon1];
        originalWieldedEquipment = new AgentEquipmentData(playerOne);
        originalPlayerTwoWieldedEquipment = new AgentEquipmentData(playerTwoInfo.Agent);
        originalAiWieldedEquipment = new AgentEquipmentData(aiInfo.Agent);
        originalDefendFlags = AgentActionData.GetDefendMovementFlags(playerOne.MovementFlags);
        originalPlayerTwoMovementFlags = playerTwoInfo.Agent.MovementFlags;
        originalGuardMode = playerOne.CurrentGuardMode;
        originalAiController = aiInfo.Agent.Controller;
        originalAiAuthority = aiInfo.CurrentAuthority;
        aiAuthorityTransferred = false;
        this.agentPositionInterpolator = agentPositionInterpolator;
        originalPlayerTwoTeam = playerTwoInfo.Agent.Team;
        originalAiTarget = aiInfo.Agent.GetTargetAgent();
        originalAllowAiTicking = Mission.Current.AllowAiTicking;
        originalPlayerTwoPosition = playerTwoInfo.Agent.Position;
        originalPlayerTwoLookDirection = playerTwoInfo.Agent.LookDirection;
        originalAiPosition = aiInfo.Agent.Position;
        originalAiLookDirection = aiInfo.Agent.LookDirection;
        baselineHealth = playerOne.Health;
        baselineMissileCount = Mission.Current?.MissilesList?.Count ?? 0;
        aiStrikeBaselineShieldHitPoints = 0;
        playerStrikeBaselineShieldHitPoints = 0;
        aiStrikeRequested = false;
        aiStrikeObserved = false;
        playerStrikeRequested = false;
        playerStrikeObserved = false;
        activeStrike = FixtureStrike.None;
        drivesActiveStrike = false;
        attackersPositioned = false;
        strikeElapsed = 0f;
        javelinRequested = false;
        javelinVisibleObserved = false;
        ResetMountedPolearmState();
        CaptureAiPauseStates(manifest, snapshot, agentRegistry);

        using (new AllowedThread())
        {
            var javelinWeapon = new MissionWeapon(javelin, null, null);
            var shieldWeapon = new MissionWeapon(shield, null, null);
            var playerTwoSword = new MissionWeapon(sword, null, null);
            var aiSword = new MissionWeapon(sword, null, null);
            ReplaceWeapon(playerOne, EquipmentIndex.Weapon0, javelinWeapon);
            ReplaceWeapon(playerOne, EquipmentIndex.Weapon1, shieldWeapon);
            ReplaceWeapon(playerTwoInfo.Agent, EquipmentIndex.Weapon0, playerTwoSword);
            ReplaceWeapon(playerTwoInfo.Agent, EquipmentIndex.Weapon1, MissionWeapon.Invalid);
            ReplaceWeapon(aiInfo.Agent, EquipmentIndex.Weapon0, aiSword);
            ReplaceWeapon(aiInfo.Agent, EquipmentIndex.Weapon1, MissionWeapon.Invalid);
            playerTwoInfo.Agent.SetTeam(aiInfo.Agent.Team, false);
            playerOne.SetWieldedItemIndexAsClient(
                Agent.HandIndex.MainHand,
                EquipmentIndex.Weapon0,
                false,
                false,
                0);
            playerOne.SetWieldedItemIndexAsClient(
                Agent.HandIndex.OffHand,
                EquipmentIndex.Weapon1,
                false,
                false,
                0);
            playerTwoInfo.Agent.SetWieldedItemIndexAsClient(
                Agent.HandIndex.MainHand,
                EquipmentIndex.Weapon0,
                false,
                false,
                0);
            aiInfo.Agent.SetWieldedItemIndexAsClient(
                Agent.HandIndex.MainHand,
                EquipmentIndex.Weapon0,
                false,
                false,
                0);
        }

        baselineShieldHitPoints = playerOne.Equipment[EquipmentIndex.Weapon1].HitPoints;
        active = true;
        Tick(0f, agentRegistry);

        return $"Initialize-TournamentCombatFixture: local={session.OwnControllerId} " +
            $"player1Agent={playerOneAgentId} health={baselineHealth:0.##} " +
            $"shieldHp={baselineShieldHitPoints} player2Agent={playerTwoAgentId} " +
            $"aiAgent={aiAgentId}";
    }

    private string ApplyAiShieldStrike(
        NetworkTournamentCombatFixtureCommand command,
        ITournamentMissionSession session,
        TournamentSessionSnapshot snapshot,
        TournamentSpawnManifestData manifest,
        INetworkAgentRegistry agentRegistry)
    {
        if (!MatchesActiveFixture(command.PlayerOneControllerId, null))
            return "Invoke-AiShieldStrike: fixture is not active for this fighter";
        if (!TryGetPlayerOneAgent(agentRegistry, out Agent playerOne))
            return "Invoke-AiShieldStrike: fighter agent was not found";
        if (activeStrike != FixtureStrike.None)
            return $"Invoke-AiShieldStrike: {activeStrike} strike is still active";

        TournamentAgentSpawnData aiData = manifest?.Agents?
            .FirstOrDefault(candidate => candidate?.AgentId == aiAgentId);
        if (aiData == null)
            return "Invoke-AiShieldStrike: tournament AI attacker was not found";
        if (!TryTransferAiAuthority(session, agentRegistry))
            return "Invoke-AiShieldStrike: tournament AI authority could not be transferred";

        aiStrikeBaselineShieldHitPoints = GetShieldHitPoints(playerOne);
        aiStrikeRequested = true;
        aiStrikeObserved = false;
        activeStrike = FixtureStrike.Ai;
        drivesActiveStrike = session.OwnControllerId == playerTwoControllerId;
        attackersPositioned = false;
        strikeElapsed = 0f;
        if (!drivesActiveStrike)
            return null;

        return $"Invoke-AiShieldStrike: local={session.OwnControllerId} " +
            $"aiAgent={aiData.AgentId} victim={playerOneAgentId} " +
            $"host={snapshot?.HostControllerId} nativeAttack=true";
    }

    private string ApplyPlayerShieldStrike(
        NetworkTournamentCombatFixtureCommand command,
        ITournamentMissionSession session,
        INetworkAgentRegistry agentRegistry)
    {
        if (!MatchesActiveFixture(command.PlayerOneControllerId, command.PlayerTwoControllerId))
            return "Invoke-PlayerShieldStrike: fixture is not active for these controllers";
        if (!TryGetPlayerOneAgent(agentRegistry, out Agent playerOne))
            return "Invoke-PlayerShieldStrike: fighter agent was not found";
        if (activeStrike != FixtureStrike.None)
            return $"Invoke-PlayerShieldStrike: {activeStrike} strike is still active";

        playerStrikeBaselineShieldHitPoints = GetShieldHitPoints(playerOne);
        playerStrikeRequested = true;
        playerStrikeObserved = false;
        activeStrike = FixtureStrike.Player;
        drivesActiveStrike = session.OwnControllerId == playerTwoControllerId;
        attackersPositioned = false;
        strikeElapsed = 0f;
        if (!drivesActiveStrike)
            return null;

        return $"Invoke-PlayerShieldStrike: local={session.OwnControllerId} " +
            $"attacker={playerTwoControllerId} victim={playerOneAgentId} nativeAttack=true";
    }

    private string ThrowJavelin(
        NetworkTournamentCombatFixtureCommand command,
        ITournamentMissionSession session,
        INetworkAgentRegistry agentRegistry)
    {
        if (!MatchesActiveFixture(command.PlayerOneControllerId, null))
            return "Invoke-TournamentJavelinThrow: fixture is not active for this fighter";
        if (!TryGetPlayerOneAgent(agentRegistry, out Agent playerOne))
            return "Invoke-TournamentJavelinThrow: fighter agent was not found";
        if (activeStrike != FixtureStrike.None)
            return $"Invoke-TournamentJavelinThrow: {activeStrike} strike is still active";

        EquipmentIndex weaponIndex = EquipmentIndex.Weapon0;
        MissionWeapon weapon = playerOne.Equipment[weaponIndex];
        if (weapon.IsEmpty || weapon.Item?.StringId != JavelinItemId)
            return "Invoke-TournamentJavelinThrow: fixture javelin is not equipped";

        javelinRequested = true;
        javelinVisibleObserved = false;
        if (session.OwnControllerId != command.PlayerOneControllerId)
            return null;

        playerOne.SetWieldedItemIndexAsClient(
            Agent.HandIndex.MainHand,
            weaponIndex,
            false,
            false,
            0);
        Vec3 direction = playerOne.LookDirection;
        direction.z += 0.35f;
        direction.Normalize();
        Vec3 velocity = direction * 15f;
        Mission.Current.OnAgentShootMissile(
            playerOne,
            weaponIndex,
            playerOne.GetEyeGlobalPosition(),
            velocity,
            playerOne.Frame.rotation,
            true,
            true,
            -1);

        return $"Invoke-TournamentJavelinThrow: local={session.OwnControllerId} " +
            $"shooter={playerOneAgentId}";
    }

    private string ApplyMountedPolearmGuard(
        NetworkTournamentCombatFixtureCommand command,
        ITournamentMissionSession session,
        INetworkAgentRegistry agentRegistry)
    {
        if (!MatchesActiveFixture(
                command.PlayerOneControllerId,
                command.PlayerTwoControllerId))
            return "Invoke-MountedPolearmGuard: fixture is not active for these controllers";
        if (!TryGetPlayerOneAgent(agentRegistry, out Agent playerOne))
            return "Invoke-MountedPolearmGuard: fighter agent was not found";
        if (!playerOne.HasMount)
            return "Invoke-MountedPolearmGuard: fighter is not mounted";
        if (activeStrike != FixtureStrike.None)
            return $"Invoke-MountedPolearmGuard: {activeStrike} strike is still active";

        ItemObject polearm =
            MBObjectManager.Instance.GetObject<ItemObject>(PolearmItemId);
        if (polearm == null)
            return $"Invoke-MountedPolearmGuard: {PolearmItemId} was not found";

        using (new AllowedThread())
        {
            var polearmWeapon = new MissionWeapon(polearm, null, null);
            ReplaceWeapon(playerOne, EquipmentIndex.Weapon0, polearmWeapon);
            ReplaceWeapon(
                playerOne,
                EquipmentIndex.Weapon1,
                MissionWeapon.Invalid);
            playerOne.SetWieldedItemIndexAsClient(
                Agent.HandIndex.MainHand,
                EquipmentIndex.Weapon0,
                false,
                false,
                0);
        }

        ResetMountedPolearmState();
        mountedPolearmGuardRequested = true;
        mountedPolearmBaselineHealth = playerOne.Health;
        attackersPositioned = false;
        Tick(0f, agentRegistry);

        return $"Invoke-MountedPolearmGuard: local={session.OwnControllerId} " +
            $"fighter={playerOneAgentId} mount={playerOne.MountAgent.Index} " +
            $"weapon={PolearmItemId}";
    }

    private string ApplyMountedPolearmStrike(
        NetworkTournamentCombatFixtureCommand command,
        ITournamentMissionSession session,
        INetworkAgentRegistry agentRegistry)
    {
        if (!MatchesActiveFixture(
                command.PlayerOneControllerId,
                command.PlayerTwoControllerId))
            return "Invoke-MountedPolearmStrike: fixture is not active for these controllers";
        if (!mountedPolearmGuardRequested)
            return "Invoke-MountedPolearmStrike: mounted polearm guard is not active";
        if (!TryGetPlayerOneAgent(agentRegistry, out Agent playerOne))
            return "Invoke-MountedPolearmStrike: fighter agent was not found";
        if (activeStrike != FixtureStrike.None)
            return $"Invoke-MountedPolearmStrike: {activeStrike} strike is still active";

        mountedPolearmStrikeRequested = true;
        mountedPolearmReactionObserved = false;
        mountedPolearmReactionActionIndex = -1;
        mountedPolearmReactionAnimationIndex = -1;
        mountedPolearmBaselineHealth = playerOne.Health;
        activeStrike = FixtureStrike.Polearm;
        drivesActiveStrike = session.OwnControllerId == playerTwoControllerId;
        attackersPositioned = false;
        strikeElapsed = 0f;
        if (!drivesActiveStrike)
            return null;

        return $"Invoke-MountedPolearmStrike: local={session.OwnControllerId} " +
            $"attacker={playerTwoControllerId} victim={playerOneAgentId} nativeAttack=true";
    }

    private string Restore(
        ITournamentMissionSession session,
        INetworkAgentRegistry agentRegistry)
    {
        if (!active)
            return $"Restore-TournamentCombatFixture: no fixture was active on {session.OwnControllerId}";

        StopActiveStrike(agentRegistry);
        if (TryGetPlayerOneAgent(agentRegistry, out Agent playerOne))
        {
            using (new AllowedThread())
            {
                ReplaceWeapon(playerOne, EquipmentIndex.Weapon0, originalWeapon0);
                ReplaceWeapon(playerOne, EquipmentIndex.Weapon1, originalWeapon1);
                originalWieldedEquipment.Apply(playerOne);
                AgentActionData.ApplyDefendMovementFlags(playerOne, originalDefendFlags);
                AgentActionData.ApplyGuardState(playerOne, originalGuardMode, force: true);
            }
        }
        if (TryGetActiveAgent(agentRegistry, playerTwoAgentId, out Agent playerTwo))
        {
            using (new AllowedThread())
            {
                ReplaceWeapon(playerTwo, EquipmentIndex.Weapon0, originalPlayerTwoWeapon0);
                ReplaceWeapon(playerTwo, EquipmentIndex.Weapon1, originalPlayerTwoWeapon1);
                originalPlayerTwoWieldedEquipment.Apply(playerTwo);
                playerTwo.SetTeam(originalPlayerTwoTeam, false);
                playerTwo.MovementFlags = originalPlayerTwoMovementFlags;
            }
        }
        if (TryGetActiveAgent(agentRegistry, aiAgentId, out Agent ai))
        {
            using (new AllowedThread())
            {
                ReplaceWeapon(ai, EquipmentIndex.Weapon0, originalAiWeapon0);
                ReplaceWeapon(ai, EquipmentIndex.Weapon1, originalAiWeapon1);
                originalAiWieldedEquipment.Apply(ai);
                ai.Controller = originalAiController;
                ai.SetTargetAgent(originalAiTarget?.IsActive() == true
                    ? originalAiTarget
                    : null);
            }
        }
        RestoreAgentPosition(agentRegistry, playerTwoAgentId, originalPlayerTwoPosition, originalPlayerTwoLookDirection);
        RestoreAgentPosition(agentRegistry, aiAgentId, originalAiPosition, originalAiLookDirection);
        RestoreAiPauseStates(agentRegistry);

        Reset();
        return $"Restore-TournamentCombatFixture: restored on {session.OwnControllerId}";
    }

    private bool MatchesActiveFixture(string playerOne, string playerTwo)
    {
        return active &&
            playerOne == playerOneControllerId &&
            (string.IsNullOrEmpty(playerTwo) || playerTwo == playerTwoControllerId);
    }

    private bool TryGetPlayerOneAgent(
        INetworkAgentRegistry agentRegistry,
        out Agent agent)
    {
        agent = null;
        if (agentRegistry == null ||
            !agentRegistry.TryGetAgentInfo(playerOneAgentId, out CoopAgentInfo agentInfo))
            return false;

        agent = agentInfo.Agent;
        return agent != null && agent.IsActive() && agent.Mission == Mission.Current;
    }

    private static bool TryResolveHumanAgent(
        string controllerId,
        TournamentSessionSnapshot snapshot,
        TournamentSpawnManifestData manifest,
        INetworkAgentRegistry agentRegistry,
        out TournamentAgentSpawnData data,
        out Agent agent)
    {
        data = null;
        agent = null;
        TournamentContestantData contestant = snapshot?.Contestants?
            .FirstOrDefault(candidate =>
                candidate.ControllerId == controllerId &&
                candidate.IsHuman &&
                !candidate.IsReplaced);
        if (contestant == null)
            return false;

        data = manifest?.Agents?
            .FirstOrDefault(candidate => candidate?.SlotId == contestant.SlotId);
        if (data == null ||
            !agentRegistry.TryGetAgentInfo(data.AgentId, out CoopAgentInfo agentInfo))
            return false;

        agent = agentInfo.Agent;
        return agent != null && agent.IsActive() && agent.Mission == Mission.Current;
    }

    private static bool IsHumanContestant(
        string slotId,
        TournamentSessionSnapshot snapshot)
    {
        return snapshot?.Contestants?
            .Any(candidate =>
                candidate.SlotId == slotId &&
                candidate.IsHuman &&
                !candidate.IsReplaced) == true;
    }

    private static bool TryResolvePlayerAgent(
        string controllerId,
        TournamentSessionSnapshot snapshot,
        TournamentSpawnManifestData manifest,
        INetworkAgentRegistry agentRegistry,
        out CoopAgentInfo agentInfo)
    {
        if (TryResolveHumanAgent(
                controllerId,
                snapshot,
                manifest,
                agentRegistry,
                out TournamentAgentSpawnData humanData,
                out _) &&
            agentRegistry.TryGetAgentInfo(humanData.AgentId, out agentInfo))
            return true;

        agentInfo = agentRegistry?
            .GetAgents(controllerId)
            .FirstOrDefault(candidate =>
                candidate?.Agent != null &&
                candidate.Agent.IsActive() &&
                candidate.Agent.Mission == Mission.Current &&
                manifest?.Agents?.Any(data =>
                    data?.AgentId == candidate.AgentId ||
                    data?.MountAgentId == candidate.AgentId) != true);
        return agentInfo != null;
    }

    private static bool TryResolveAiAgent(
        TournamentSessionSnapshot snapshot,
        TournamentSpawnManifestData manifest,
        INetworkAgentRegistry agentRegistry,
        Agent playerOne,
        out CoopAgentInfo agentInfo)
    {
        agentInfo = null;
        TournamentAgentSpawnData data = manifest?.Agents?
            .FirstOrDefault(candidate =>
                candidate != null &&
                !IsHumanContestant(candidate.SlotId, snapshot) &&
                agentRegistry.TryGetAgentInfo(candidate.AgentId, out CoopAgentInfo info) &&
                info.Agent != null &&
                info.Agent.IsActive() &&
                info.Agent.Mission == Mission.Current &&
                info.Agent.Team?.IsEnemyOf(playerOne.Team) == true);
        return data != null &&
            agentRegistry.TryGetAgentInfo(data.AgentId, out agentInfo);
    }

    private static bool TryGetFixtureItems(
        out ItemObject javelin,
        out ItemObject shield,
        out ItemObject sword)
    {
        javelin = MBObjectManager.Instance.GetObject<ItemObject>(JavelinItemId);
        shield = MBObjectManager.Instance.GetObject<ItemObject>(ShieldItemId);
        sword = MBObjectManager.Instance.GetObject<ItemObject>(SwordItemId);
        return javelin != null && shield != null && sword != null;
    }

    private static void ReplaceWeapon(
        Agent agent,
        EquipmentIndex slot,
        MissionWeapon replacement)
    {
        if (!agent.Equipment[slot].IsEmpty)
            agent.RemoveEquippedWeapon(slot);
        if (!replacement.IsEmpty)
            agent.EquipWeaponWithNewEntity(slot, ref replacement);
    }

    private static int GetShieldHitPoints(Agent agent)
    {
        MissionWeapon shield = agent.Equipment[EquipmentIndex.Weapon1];
        return shield.IsEmpty ? 0 : shield.HitPoints;
    }

    private void ObserveMountedPolearmState(
        INetworkAgentRegistry agentRegistry,
        Agent playerOne)
    {
        ObserveVisualAnimations(
            playerOne,
            mountedPolearmPlayerOneVisualAnimations);
        CapturePresentationAction(
            playerOne,
            AgentActionData.IsDefendingAction,
            ref mountedPolearmGuardActionIndex,
            ref mountedPolearmGuardAnimationIndex);

        if (!TryGetActiveAgent(
                agentRegistry,
                playerTwoAgentId,
                out Agent playerTwo))
            return;

        ObserveVisualAnimations(
            playerTwo,
            mountedPolearmPlayerTwoVisualAnimations);
        if (CapturePresentationAction(
                playerOne,
                IsBlockReaction,
                ref mountedPolearmReactionActionIndex,
                ref mountedPolearmReactionAnimationIndex))
        {
            mountedPolearmReactionObserved = true;
        }
    }

    private static bool CapturePresentationAction(
        Agent agent,
        Func<Agent.ActionCodeType, bool> predicate,
        ref int actionIndex,
        ref int animationIndex)
    {
        for (int channel = 1; channel >= 0; channel--)
        {
            if (!predicate(agent.GetCurrentActionType(channel)))
                continue;

            ActionIndexCache action = agent.GetCurrentAction(channel);
            if (action == ActionIndexCache.act_none)
                continue;

            actionIndex = action.Index;
            animationIndex = MBActionSet.GetAnimationIndexOfAction(
                agent.ActionSet,
                in action);
            return true;
        }

        return false;
    }

    private static bool IsBlockReaction(Agent.ActionCodeType actionType)
    {
        return actionType == Agent.ActionCodeType.ParriedMelee
            || actionType == Agent.ActionCodeType.BlockedMelee;
    }

    private static void ObserveVisualAnimations(
        Agent agent,
        HashSet<int> observedAnimations)
    {
        Skeleton skeleton = null;
        try
        {
            MBAgentVisuals visuals = agent.AgentVisuals;
            if (ReferenceEquals(visuals, null) || !visuals.IsValid())
                return;

            skeleton = visuals.GetSkeleton();
            if (ReferenceEquals(skeleton, null))
                return;

            for (int channel = 0; channel <= 1; channel++)
            {
                int animationIndex =
                    skeleton.GetAnimationIndexAtChannel(channel);
                if (animationIndex >= 0)
                    observedAnimations.Add(animationIndex);
            }
        }
        catch
        {
            // Agent visuals can disappear while the tournament is ending.
        }
        finally
        {
            if (!ReferenceEquals(skeleton, null))
                skeleton.ManualInvalidate();
        }
    }

    private static bool TryGetActiveAgent(
        INetworkAgentRegistry agentRegistry,
        Guid agentId,
        out Agent agent)
    {
        agent = null;
        if (agentRegistry == null ||
            !agentRegistry.TryGetAgentInfo(agentId, out CoopAgentInfo info))
            return false;

        agent = info.Agent;
        return agent != null &&
            agent.IsActive() &&
            Mission.Current != null &&
            agent.Mission == Mission.Current;
    }

    private void CaptureAiPauseStates(
        TournamentSpawnManifestData manifest,
        TournamentSessionSnapshot snapshot,
        INetworkAgentRegistry agentRegistry)
    {
        aiPauseStates.Clear();
        foreach (TournamentAgentSpawnData data in manifest?.Agents ??
                     Array.Empty<TournamentAgentSpawnData>())
        {
            if (data == null ||
                IsHumanContestant(data.SlotId, snapshot) ||
                !TryGetActiveAgent(agentRegistry, data.AgentId, out Agent agent))
                continue;

            aiPauseStates.Add(new AiPauseState(data.AgentId, agent.IsPaused));
        }
    }

    private void PauseTournamentAi(INetworkAgentRegistry agentRegistry)
    {
        foreach (AiPauseState state in aiPauseStates)
        {
            if (state.AgentId == aiAgentId &&
                drivesActiveStrike &&
                activeStrike == FixtureStrike.Ai)
                continue;
            if (TryGetActiveAgent(agentRegistry, state.AgentId, out Agent agent))
                agent.SetIsAIPaused(true);
        }
    }

    private void RestoreAiPauseStates(INetworkAgentRegistry agentRegistry)
    {
        foreach (AiPauseState state in aiPauseStates)
        {
            if (TryGetActiveAgent(agentRegistry, state.AgentId, out Agent agent))
                agent.SetIsAIPaused(state.WasPaused);
        }
    }

    private void DriveAiAttack(
        INetworkAgentRegistry agentRegistry,
        Agent playerOne)
    {
        if (!TryGetActiveAgent(agentRegistry, aiAgentId, out Agent ai))
            return;

        if (ai.Controller != AgentControllerType.AI)
            ai.Controller = AgentControllerType.AI;
        Mission.Current.AllowAiTicking = true;
        ai.SetTargetAgent(playerOne);
        ai.SetWatchState(Agent.WatchState.Alarmed);
        ai.SetIsAIPaused(false);
    }

    private void DrivePlayerAttack(INetworkAgentRegistry agentRegistry)
    {
        if (!TryGetActiveAgent(agentRegistry, playerTwoAgentId, out Agent playerTwo))
            return;

        Agent.MovementControlFlag flags =
            originalPlayerTwoMovementFlags & ~AttackFlags;
        if (strikeElapsed % AttackCycleSeconds < AttackPressSeconds)
            flags |= Agent.MovementControlFlag.AttackUp;
        playerTwo.MovementFlags = flags;
    }

    private void StopActiveStrike(INetworkAgentRegistry agentRegistry)
    {
        if (TryGetActiveAgent(agentRegistry, playerTwoAgentId, out Agent playerTwo))
        {
            playerTwo.MovementFlags =
                originalPlayerTwoMovementFlags & ~AttackFlags;
        }
        if (TryGetActiveAgent(agentRegistry, aiAgentId, out Agent ai))
        {
            agentPositionInterpolator?.Forget(ai);
            ai.SetTargetAgent(null);
            ai.SetIsAIPaused(true);
            if (ai.Controller != originalAiController)
                ai.Controller = originalAiController;
        }
        if (aiAuthorityTransferred &&
            agentRegistry.TryTransferAuthority(originalAiAuthority, aiAgentId))
            aiAuthorityTransferred = false;
        if (Mission.Current != null)
            Mission.Current.AllowAiTicking = originalAllowAiTicking;
    }

    private bool TryTransferAiAuthority(
        ITournamentMissionSession session,
        INetworkAgentRegistry agentRegistry)
    {
        if (string.IsNullOrEmpty(originalAiAuthority) ||
            !TryGetActiveAgent(agentRegistry, aiAgentId, out Agent ai) ||
            !agentRegistry.TryTransferAuthority(playerTwoControllerId, aiAgentId))
            return false;

        bool ownsAi = session.OwnControllerId == playerTwoControllerId;
        agentPositionInterpolator.Forget(ai);
        using (new AllowedThread())
        {
            if (ownsAi)
            {
                ai.Controller = AgentControllerType.AI;
                AgentAiWaker.Wake(ai);
            }
            else
            {
                ai.SetIsAIPaused(true);
                ai.Controller = AgentControllerType.None;
            }
        }
        aiAuthorityTransferred = true;
        return true;
    }

    private void PositionAttackers(
        INetworkAgentRegistry agentRegistry,
        Agent playerOne)
    {
        Vec3 forward = playerOne.LookDirection;
        forward.z = 0f;
        if (forward.LengthSquared < 0.0001f)
            forward = new Vec3(0f, 1f, 0f);
        forward.Normalize();
        Vec3 right = new Vec3(-forward.y, forward.x, 0f);
        bool playerStrike =
            activeStrike == FixtureStrike.Player
            || activeStrike == FixtureStrike.Polearm;
        float playerTwoOffset = playerStrike ? 0f : -0.75f;
        float aiOffset = activeStrike == FixtureStrike.Ai ? 0f : 0.75f;
        if (activeStrike == FixtureStrike.Ai)
            playerTwoOffset = -2.5f;
        else if (playerStrike)
            aiOffset = 2.5f;

        PositionAgent(
            agentRegistry,
            playerTwoAgentId,
            playerOne.Position + (forward * 1.25f) + (right * playerTwoOffset),
            playerOne.Position);
        PositionAgent(
            agentRegistry,
            aiAgentId,
            playerOne.Position + (forward * 1.25f) + (right * aiOffset),
            playerOne.Position);
    }

    private static void PositionAgent(
        INetworkAgentRegistry agentRegistry,
        Guid agentId,
        Vec3 position,
        Vec3 lookAt)
    {
        if (!agentRegistry.TryGetAgentInfo(agentId, out CoopAgentInfo info) ||
            info.Agent == null ||
            !info.Agent.IsActive())
            return;

        Vec3 lookDirection = lookAt - position;
        lookDirection.z = 0f;
        if (lookDirection.LengthSquared > 0.0001f)
        {
            lookDirection.Normalize();
            info.Agent.LookDirection = lookDirection;
        }
        info.Agent.TeleportToPosition(position);
    }

    private static void RestoreAgentPosition(
        INetworkAgentRegistry agentRegistry,
        Guid agentId,
        Vec3 position,
        Vec3 lookDirection)
    {
        if (!agentRegistry.TryGetAgentInfo(agentId, out CoopAgentInfo info) ||
            info.Agent == null ||
            !info.Agent.IsActive())
            return;

        info.Agent.TeleportToPosition(position);
        info.Agent.LookDirection = lookDirection;
    }

    private void Reset()
    {
        active = false;
        ownControllerId = null;
        playerOneControllerId = null;
        playerTwoControllerId = null;
        playerOneAgentId = Guid.Empty;
        playerTwoAgentId = Guid.Empty;
        aiAgentId = Guid.Empty;
        playerOneAgent = null;
        originalWeapon0 = MissionWeapon.Invalid;
        originalWeapon1 = MissionWeapon.Invalid;
        originalPlayerTwoWeapon0 = MissionWeapon.Invalid;
        originalPlayerTwoWeapon1 = MissionWeapon.Invalid;
        originalAiWeapon0 = MissionWeapon.Invalid;
        originalAiWeapon1 = MissionWeapon.Invalid;
        originalWieldedEquipment = default;
        originalPlayerTwoWieldedEquipment = default;
        originalAiWieldedEquipment = default;
        originalDefendFlags = Agent.MovementControlFlag.None;
        originalPlayerTwoMovementFlags = Agent.MovementControlFlag.None;
        originalGuardMode = Agent.GuardMode.None;
        originalAiController = AgentControllerType.None;
        originalAiAuthority = null;
        aiAuthorityTransferred = false;
        agentPositionInterpolator = null;
        originalPlayerTwoTeam = null;
        originalAiTarget = null;
        originalAllowAiTicking = false;
        originalPlayerTwoPosition = Vec3.Zero;
        originalPlayerTwoLookDirection = Vec3.Zero;
        originalAiPosition = Vec3.Zero;
        originalAiLookDirection = Vec3.Zero;
        baselineHealth = 0f;
        baselineShieldHitPoints = 0;
        baselineMissileCount = 0;
        aiStrikeBaselineShieldHitPoints = 0;
        playerStrikeBaselineShieldHitPoints = 0;
        aiStrikeRequested = false;
        aiStrikeObserved = false;
        playerStrikeRequested = false;
        playerStrikeObserved = false;
        activeStrike = FixtureStrike.None;
        drivesActiveStrike = false;
        attackersPositioned = false;
        strikeElapsed = 0f;
        javelinRequested = false;
        javelinVisibleObserved = false;
        ResetMountedPolearmState();
        aiPauseStates.Clear();
    }

    private void ResetMountedPolearmState()
    {
        mountedPolearmPlayerOneVisualAnimations.Clear();
        mountedPolearmPlayerTwoVisualAnimations.Clear();
        mountedPolearmGuardRequested = false;
        mountedPolearmStrikeRequested = false;
        mountedPolearmReactionObserved = false;
        mountedPolearmGuardActionIndex = -1;
        mountedPolearmGuardAnimationIndex = -1;
        mountedPolearmReactionActionIndex = -1;
        mountedPolearmReactionAnimationIndex = -1;
        mountedPolearmBaselineHealth = 0f;
    }

    private static string FormatAnimationIndexes(
        HashSet<int> animationIndexes)
    {
        return animationIndexes.Count == 0
            ? "none"
            : string.Join(",", animationIndexes.OrderBy(value => value));
    }

    private enum FixtureStrike
    {
        None,
        Ai,
        Player,
        Polearm
    }

    private sealed class AiPauseState
    {
        public Guid AgentId { get; }
        public bool WasPaused { get; }

        public AiPauseState(Guid agentId, bool wasPaused)
        {
            AgentId = agentId;
            WasPaused = wasPaused;
        }
    }
}
#endif
