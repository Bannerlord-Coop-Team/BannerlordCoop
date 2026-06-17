using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace GameInterface.Services.Clans.Messages;

public readonly struct ClanManagementVMCreated : IEvent
{
    public readonly ClanManagementVM ClanManagementVM;

    public ClanManagementVMCreated(ClanManagementVM clanManagementVM)
    {
        ClanManagementVM = clanManagementVM;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RefreshPartiesList : ICommand {}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RefreshWorkshopsList : ICommand { }