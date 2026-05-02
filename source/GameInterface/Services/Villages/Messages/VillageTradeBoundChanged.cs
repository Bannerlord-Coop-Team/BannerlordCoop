using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Messages;

[BatchLogMessage]
public readonly struct VillageTradeBoundChanged : IEvent
{
    public readonly Village Village;
    public readonly Settlement TradeBound;

    public VillageTradeBoundChanged(Village village, Settlement tradeBound)
    {
        Village = village;
        TradeBound = tradeBound;
    }
}