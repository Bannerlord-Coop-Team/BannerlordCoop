using Common.Messaging;
using GameInterface.Services.ViewData.Data;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.ViewData.Messages;

public readonly struct InitializeClientViewData : IEvent
{
    public readonly ViewDataPlayerData ViewDataPlayerData;

    public InitializeClientViewData(ViewDataPlayerData viewDataPlayerData)
    {
        ViewDataPlayerData = viewDataPlayerData;
    }
}

public readonly struct ViewDataChanged : IEvent
{
    public readonly Hero MainHero;

    public ViewDataChanged(Hero mainHero)
    {
        MainHero = mainHero;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSaveViewData : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly PlayerViewData ViewData;

    public NetworkSaveViewData(string mainHeroId, PlayerViewData viewData)
    {
        MainHeroId = mainHeroId;
        ViewData = viewData;
    }
}
