using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Rafts.Messages;

public readonly struct PlayerRaftResumeAttempted : IEvent
{
    public readonly MobileParty PlayerParty;

    public PlayerRaftResumeAttempted(MobileParty playerParty)
    {
        PlayerParty = playerParty;
    }
}
