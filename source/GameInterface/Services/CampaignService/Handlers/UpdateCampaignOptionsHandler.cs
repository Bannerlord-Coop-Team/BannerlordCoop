using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.CampaignService.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CampaignService.Handlers;

internal class UpdateCampaignOptionsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<UpdateCampaignOptionsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public UpdateCampaignOptionsHandler(
        IMessageBroker messageBroker,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<UpdateCampaignOptions>(Handle_UpdateCampaignOptions);
        messageBroker.Subscribe<NetworkUpdateCampaignOptions>(Handle_NetworkUpdateCampaignOptions);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateCampaignOptions>(Handle_UpdateCampaignOptions);
        messageBroker.Unsubscribe<NetworkUpdateCampaignOptions>(Handle_NetworkUpdateCampaignOptions);
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
}
