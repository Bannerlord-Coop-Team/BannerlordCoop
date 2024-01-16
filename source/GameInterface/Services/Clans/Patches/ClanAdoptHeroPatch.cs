using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(AdoptHeroAction), "ApplyInternal")]
    public class ClanAdoptHeroPatch
    {
        public static bool Prefix(Hero adoptedHero)
        {
            if (PolicyProvider.AllowOriginalCalls) return true;

            string playerClanId = Clan.PlayerClan.StringId;

            string playerHeroId = Hero.MainHero.StringId;

            MessageBroker.Instance.Publish(adoptedHero, new HeroAdopted(adoptedHero.StringId, playerClanId, playerHeroId));

            return false;
        }

        public static void RunFixedAdoptHero(Hero adoptedHero, Clan playerClan, Hero playerHero)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                if(playerHero.IsFemale)
                {
                    adoptedHero.Mother = playerHero;
                }
                else
                {
                    adoptedHero.Father = playerHero;
                }
                adoptedHero.Clan = playerClan;
            }, true);
        }
    }
}
