using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Messages;

public readonly struct ChangeClanName : IEvent
{
    public readonly Clan Clan;
    public readonly TextObject Name;
    public readonly TextObject InformalName;

    public ChangeClanName(
        Clan clan,
        TextObject name,
        TextObject informalName)
    {
        Clan = clan;
        Name = name;
        InformalName = informalName;
    }
}
