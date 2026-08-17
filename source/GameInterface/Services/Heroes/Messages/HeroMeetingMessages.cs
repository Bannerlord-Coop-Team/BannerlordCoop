using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages;

public record InitializeClientHeroMeetingData : IEvent
{
    public HeroMeetingData HeroMeetingData { get; }

    public InitializeClientHeroMeetingData(HeroMeetingData heroMeetingData)
    {
        HeroMeetingData = heroMeetingData;
    }
}

public record PlayerMetHero : IEvent
{
    public Hero PlayerHero { get; }
    public Hero MetHero { get; }
    public CampaignTime LastMeetingTime { get; }

    public PlayerMetHero(Hero playerHero, Hero metHero, CampaignTime lastMeetingTime)
    {
        PlayerHero = playerHero;
        MetHero = metHero;
        LastMeetingTime = lastMeetingTime;
    }
}

[ProtoContract(SkipConstructor = true)]
public record NetworkPlayerMetHero : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId { get; }

    [ProtoMember(2)]
    public string MetHeroId { get; }

    [ProtoMember(3)]
    public long LastMeetingTimeTicks { get; }

    public NetworkPlayerMetHero(string playerHeroId, string metHeroId, long lastMeetingTimeTicks)
    {
        PlayerHeroId = playerHeroId;
        MetHeroId = metHeroId;
        LastMeetingTimeTicks = lastMeetingTimeTicks;
    }
}
