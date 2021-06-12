using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Coop.Mod.Patch.World
{
    class HeroSync : CoopManaged<HeroSync, Hero>
    {
        static HeroSync()
        {
            When(GameLoop)
                .Calls(Setter(nameof(Hero.HasMet)),
                       Method(nameof(Hero.ChangeState)),
                       Method(typeof(AddCompanionAction), nameof(AddCompanionAction.Apply)))
                .Broadcast(() => CoopClient.Instance.Synchronization)
                .DelegateTo(IsServer);

            ApplyStaticPatches();
            AutoWrapAllInstances(c => new HeroSync(c));
        }

        private static ECallPropagation IsServer(IPendingMethodCall call)
        {
            if(Coop.IsServer || (Hero)call.Instance == Hero.MainHero)
            {
                return ECallPropagation.CallOriginal;
            }
            else
            {
                return ECallPropagation.Skip;
            }
        }

        public HeroSync([NotNull] Hero instance) : base(instance)
        {
        }
    }
}
