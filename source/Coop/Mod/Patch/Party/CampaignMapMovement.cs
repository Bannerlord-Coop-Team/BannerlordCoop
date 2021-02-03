using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Patch
{
    public class CampaignMapMovement : CoopManaged<CampaignMapMovement, MobileParty>
    {
        private static Condition PartyController = new Condition((eOriginator, instance) =>
        {
            return eOriginator == EOriginator.Game && Coop.IsController(instance as MobileParty);
        });
        static CampaignMapMovement()
        {
            When(PartyController)
                .Changes(
                    Field<AiBehavior>("_defaultBehavior"),
                    Field<Settlement>("_targetSettlement"),
                    Field<MobileParty>("_targetParty"),
                    Field<Vec2>("_targetPosition"),
                    Field<int>("_numberOfFleeingsAtLastTravel"))
                .Through(
                    Setter(nameof(MobileParty.DefaultBehavior)),
                    Setter(nameof(MobileParty.TargetSettlement)),
                    Setter(nameof(MobileParty.TargetParty)),
                    Setter(nameof(MobileParty.TargetPosition)))
                .Broadcast()
                .Keep(); // Keep the changes as a preview
            
            AutoWrapAllInstances(party => new CampaignMapMovement(party));
        }

        public CampaignMapMovement([NotNull] MobileParty instance) : base(instance)
        {
        }
    }
}
