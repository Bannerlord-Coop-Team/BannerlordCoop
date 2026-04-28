using Common.Messaging;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.HeroDevelopers.Messages
{
    public record SkillLevelChange : IEvent
    {
        public HeroDeveloper HeroDeveloper;
        public SkillObject SkillObject;
        public int ChangeAmount;
        public bool ShouldNotify;

        public SkillLevelChange(HeroDeveloper heroDeveloper, SkillObject skillObject, int changeAmount, bool shouldNotify)
        {
            HeroDeveloper = heroDeveloper;
            SkillObject = skillObject;
            ChangeAmount = changeAmount;
            ShouldNotify = shouldNotify;
        }
    }
}
