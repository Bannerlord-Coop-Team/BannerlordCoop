using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection determining if a character already
/// exists for this connection
/// </summary>
public class ResolveCharacterState : ConnectionStateBase
{
    private static readonly ILogger Logger = LogManager.GetLogger<ResolveCharacterState>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IModuleValidator moduleValidator;
    private readonly IPlayerManager playerManager;
    private readonly IPlayerPartyRestorer playerPartyRestorer;
    private readonly IObjectManager objectManager;
    private readonly IModuleInfoProvider moduleInfoProvider;
    private readonly IExistingPlayerSender existingPlayerSender;

    public ResolveCharacterState(IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        IModuleValidator moduleValidator,
        IPlayerManager playerManager,
        IPlayerPartyRestorer playerPartyRestorer,
        IObjectManager objectManager,
        IModuleInfoProvider moduleInfoProvider,
        IExistingPlayerSender existingPlayerSender)
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.moduleValidator = moduleValidator;
        this.playerManager = playerManager;
        this.playerPartyRestorer = playerPartyRestorer;
        this.objectManager = objectManager;
        this.moduleInfoProvider = moduleInfoProvider;
        this.existingPlayerSender = existingPlayerSender;

        messageBroker.Subscribe<NetworkClientValidate>(Handle_ClientValidate);
        messageBroker.Subscribe<NetworkModuleVersionsValidate>(Handle_ModuleVersionsValidate);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkClientValidate>(Handle_ClientValidate);
        messageBroker.Unsubscribe<NetworkModuleVersionsValidate>(Handle_ModuleVersionsValidate);
    }

    internal void Handle_ModuleVersionsValidate(MessagePayload<NetworkModuleVersionsValidate> obj)
    {
        // Same guard as Handle_ClientValidate: every connection in this state receives every
        // client's broadcastable messages, so without it each concurrent joiner would also be
        // answered with a result computed from another client's module list.
        var peer = obj.Who as NetPeer;
        if (peer != ConnectionLogic.Peer) return;

        bool result;
        string error;
        try
        {
            var clientModules = obj.What.Modules;
            var serverModules = moduleInfoProvider.GetModuleInfos();

            if (!ModInformation.MatchesBuildVersion(obj.What.CoopBuildVersion))
            {
                result = false;
                error = GetIncompatibleBuildReason(obj.What.CoopBuildVersion);
            }
            else
            {
                result = moduleValidator.Validate(
                    serverModules,
                    clientModules.Select(ConvertToModuleInfo),
                    out error);
            }
        }
        catch (Exception e)
        {
            // A throw here used to die in the network poller, so the client never received an
            // answer and sat on the "Validating modules..." loading screen forever. Answer with a
            // denial instead so the joiner gets a visible reason.
            Logger.Error(e, "Module validation threw for peer {Peer}", ConnectionLogic.Peer?.Id);
            result = false;
            error = $"The server failed to validate the module list ({e.GetType().Name}). " +
                    "Check that the client and server run the same game and mod versions.";
        }

        var validateMessage = new NetworkModuleVersionsValidated(
            result,
            error,
            ModInformation.BuildVersion);
        network.SendImmediate(ConnectionLogic.Peer, validateMessage);
    }

    private static string GetIncompatibleBuildReason(string? clientBuildVersion)
    {
        if (string.IsNullOrEmpty(clientBuildVersion))
        {
            return $"Incompatible co-op mod build. The server uses '{ModInformation.BuildVersion}', " +
                   "but the client did not report an exact build. Update the co-op mod on both sides.";
        }

        return $"Incompatible co-op mod build. The server uses '{ModInformation.BuildVersion}', " +
               $"but the client uses '{clientBuildVersion}'. Update the co-op mod on both sides.";
    }

    internal void Handle_ClientValidate(MessagePayload<NetworkClientValidate> obj)
    {
        var peer = obj.Who as NetPeer;
        if (peer != ConnectionLogic.Peer) return;

        try
        {
            ResolveCharacter(peer, obj.What.PlayerId);
        }
        catch (Exception e)
        {
            // Same reasoning as Handle_ModuleVersionsValidate: a throw here dies in the network
            // poller with no answer sent, so the joiner sits on the loading screen until its
            // validation deadline expires. NetworkClientValidated carries no reason, and
            // answering "no hero" would push the player into creating a second character, so
            // drop the connection instead — the client surfaces a disconnect immediately.
            Logger.Error(e, "Resolving the character for peer {Peer} failed; disconnecting the joiner", peer?.Id);

            // Nothing may escape into the poller, including the teardown itself.
            try
            {
                peer?.Disconnect();
            }
            catch (Exception disconnectFailure)
            {
                Logger.Error(disconnectFailure, "Disconnecting peer {Peer} failed", peer?.Id);
            }
        }
    }

    private void ResolveCharacter(NetPeer peer, string controllerId)
    {
        if (playerManager.TryGetPlayer(controllerId, out var player))
        {
            var heroExists = false;
            var partyRestored = false;
            var registrationReplaced = false;
            var restoredPlayer = player;

            // Resolve campaign objects and repair the registration on the game thread. A loaded
            // party can be registered by an earlier queued apply even though this poll-thread
            // validation has already arrived.
            GameThread.Run(() =>
            {
                heroExists = objectManager.TryGetObjectWithLogging(player.HeroId, out Hero _);
                if (!heroExists) return;

                partyRestored = playerPartyRestorer.TryRestore(player, out restoredPlayer);
                if (!partyRestored || ReferenceEquals(restoredPlayer, player)) return;

                registrationReplaced = playerManager.ReplacePlayer(player, restoredPlayer);
            }, blocking: true);

            if (heroExists)
            {
                if (!partyRestored || (!ReferenceEquals(restoredPlayer, player) && !registrationReplaced))
                {
                    Logger.Error(
                        "Controller {ControllerId} hero {HeroId} has no recoverable player party; disconnecting the joiner",
                        controllerId,
                        player.HeroId);
                    peer.Disconnect();
                    return;
                }

                if (registrationReplaced)
                    network.SendAllBut(peer, new NetworkPlayerRegistrationUpdated(restoredPlayer));

                // This peer is a new NetPeer for an already registered player, so the
                // peer-Player link must be established here
                playerManager.SetPeer(controllerId, peer);
                network.SendImmediate(peer, new NetworkClientValidated(true, restoredPlayer));
                ConnectionLogic.TransferSave();

                existingPlayerSender.SendExistingPlayers(peer, controllerId);
                return;
            }

            // Registered, but the hero it names is not in the campaign — a registration that
            // outlived its objects. Deregister it before routing to character creation: the
            // character created next registers this same controller id, and leaving the dead
            // entry behind would make the controller ambiguous for the rest of the session.
            Logger.Warning(
                "Controller {ControllerId} is registered to hero {HeroId}, which no longer exists; " +
                "dropping the stale registration and creating a new character",
                controllerId, player.HeroId);

            playerManager.RemovePlayer(player);
        }

        network.SendImmediate(peer, new NetworkClientValidated(false, null));
        ConnectionLogic.CreateCharacter();
    }

    public override void CreateCharacter()
    {
        ConnectionLogic.SetState<CreateCharacterState>();
    }

    public override void TransferSave()
    {
        // SetState packages and sends the save synchronously; then move to LoadingState to await the
        // client reporting it has entered the campaign. Load() must run here (not inside the
        // TransferSaveState ctor) so it resolves against TransferSaveState, not this state.
        ConnectionLogic.SetState<TransferSaveState>();
        ConnectionLogic.Load();
    }

    public override void Load()
    {
    }

    public override void EnterCampaign()
    {
    }

    public override void EnterMission()
    {
    }

    private static ModuleInfo ConvertToModuleInfo(NetworkModuleInfo networkModuleInfo)
    {
        return new ModuleInfo()
        {
            Id = networkModuleInfo.Id,
            IsOfficial = networkModuleInfo.IsOfficial,
            IsDlc = networkModuleInfo.IsDlc,
            Version = new ApplicationVersion((ApplicationVersionType)networkModuleInfo.Version.ApplicationVersionType, networkModuleInfo.Version.Major, networkModuleInfo.Version.Minor, networkModuleInfo.Version.Revision, networkModuleInfo.Version.ChangeSet)
        };
    }
}
