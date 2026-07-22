using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.CampaignService.Data;
using GameInterface.Services.CampaignService.Interfaces;
using GameInterface.Services.CampaignService.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.CampaignService.Handlers;

internal class UpdateCampaignOptionsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<UpdateCampaignOptionsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IServerOptionsProvider serverOptionsProvider;

    public UpdateCampaignOptionsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IServerOptionsProvider serverOptionsProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.serverOptionsProvider = serverOptionsProvider;
        messageBroker.Subscribe<UpdateCampaignOptions>(Handle_UpdateCampaignOptions);
        messageBroker.Subscribe<NetworkUpdateCampaignOptions>(Handle_NetworkUpdateCampaignOptions);

        messageBroker.Subscribe<UpdateOtherOptions>(Handle_UpdateOtherOptions);
        messageBroker.Subscribe<NetworkUpdateOtherOptions>(Handle_NetworkUpdateOtherOptions);

        messageBroker.Subscribe<InitializeServerOptionsOnClient>(Handle_InitializeServerOptionsOnClient);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateCampaignOptions>(Handle_UpdateCampaignOptions);
        messageBroker.Unsubscribe<NetworkUpdateCampaignOptions>(Handle_NetworkUpdateCampaignOptions);

        messageBroker.Unsubscribe<UpdateOtherOptions>(Handle_UpdateOtherOptions);
        messageBroker.Unsubscribe<NetworkUpdateOtherOptions>(Handle_NetworkUpdateOtherOptions);

        messageBroker.Unsubscribe<InitializeServerOptionsOnClient>(Handle_InitializeServerOptionsOnClient);
    }

    private void Handle_UpdateCampaignOptions(MessagePayload<UpdateCampaignOptions> obj)
    {
        GameThread.RunSafe(() =>
        {
            var message = new NetworkUpdateCampaignOptions(
                CampaignOptions.AutoAllocateClanMemberPerks,
                CampaignOptions.PlayerTroopsReceivedDamage,
                CampaignOptions.RecruitmentDifficulty,
                CampaignOptions.PlayerMapMovementSpeed,
                CampaignOptions.StealthAndDisguiseDifficulty,
                CampaignOptions.CombatAIDifficulty,
                CampaignOptions.IsLifeDeathCycleDisabled,
                CampaignOptions.PersuasionSuccessChance,
                CampaignOptions.ClanMemberDeathChance,
                CampaignOptions.IsIronmanMode,
                CampaignOptions.BattleDeath
            );
            network.SendAll(message);
        });
    }

    private void Handle_NetworkUpdateCampaignOptions(MessagePayload<NetworkUpdateCampaignOptions> obj)
    {
        var newOptions = obj.What;

        GameThread.RunSafe(() =>
        {
            CampaignOptions.AutoAllocateClanMemberPerks = newOptions.AutoAllocateClanMemberPerks;
            CampaignOptions.PlayerTroopsReceivedDamage = newOptions.PlayerTroopsReceivedDamage;
            CampaignOptions.RecruitmentDifficulty = newOptions.RecruitmentDifficulty;
            CampaignOptions.PlayerMapMovementSpeed = newOptions.PlayerMapMovementSpeed;
            CampaignOptions.StealthAndDisguiseDifficulty = newOptions.StealthAndDisguiseDifficulty;
            CampaignOptions.CombatAIDifficulty = newOptions.CombatAIDifficulty;
            CampaignOptions.IsLifeDeathCycleDisabled = newOptions.IsLifeDeathCycleDisabled;
            CampaignOptions.PersuasionSuccessChance = newOptions.PersuasionSuccessChance;
            CampaignOptions.ClanMemberDeathChance = newOptions.ClanMemberDeathChance;
            CampaignOptions.IsIronmanMode = newOptions.IsIronmanMode;
            CampaignOptions.BattleDeath = newOptions.BattleDeath;
        });
    }

    private void Handle_UpdateOtherOptions(MessagePayload<UpdateOtherOptions> obj)
    {
        GameThread.RunSafe(() =>
        {
            var message = new NetworkUpdateOtherOptions(serverOptionsProvider.GetServerOptions());
            network.SendAll(message);
        });
    }

    private void Handle_NetworkUpdateOtherOptions(MessagePayload<NetworkUpdateOtherOptions> obj)
    {
        GameThread.RunSafe(() =>
        {
            var newOptions = obj.What.ServerOptions;
            UpdateOtherOptions(newOptions);
        });
    }

    private void Handle_InitializeServerOptionsOnClient(MessagePayload<InitializeServerOptionsOnClient> obj)
    {
        GameThread.RunSafe(() =>
        {
            var newOptions = obj.What.ServerOptions;
            UpdateOtherOptions(newOptions);
        });
    }

    private void UpdateOtherOptions(ServerOptions newOptions)
    {
        serverOptionsProvider.ApplyServerOptions(newOptions);
    }
}
