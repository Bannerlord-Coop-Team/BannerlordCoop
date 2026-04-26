using Common.Messaging;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.HeroDevelopers.Messages
{
    public record RawXpGain : IEvent
    {
        public HeroDeveloper HeroDeveloper;
        public float RawXp;
        public bool ShouldNotify;

        public RawXpGain(HeroDeveloper heroDeveloper, float rawXp, bool shouldNotify)
        {
            HeroDeveloper = heroDeveloper;
            RawXp = rawXp;
            ShouldNotify = shouldNotify;
        }
    }
}
