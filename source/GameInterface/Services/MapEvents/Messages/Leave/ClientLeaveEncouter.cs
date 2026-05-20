using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Leave;

public readonly struct ClientLeaveEncouter
{
    public readonly MobileParty MobileParty;

    public ClientLeaveEncouter(MobileParty mobileParty)
    {
        MobileParty = mobileParty;
    }
}
