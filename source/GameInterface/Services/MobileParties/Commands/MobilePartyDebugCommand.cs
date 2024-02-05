using GameInterface.Services.MobileParties.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MobileParties.Commands
{
    internal class MobilePartyDebugCommand
    {
        [CommandLineArgumentFunction("info", "coop.debug.mobileparty")]
        public static string Info(List<string> args)
        {
            if(args.Count < 1)
            {
                return "Usage: coop.debug.mobileparty.info <PartyStringID>";
            }

            MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);

            if(mobileParty == null )
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }

            Hero owner = mobileParty.Owner;
            FieldInfo typeMobileParty = typeof(MobileParty).GetField("_lastCalculatedSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var _lastCalculatedSpeed = typeMobileParty.GetValue(mobileParty);

            String skillsStr = "";
            foreach(SkillObject skill in Skills.All)
            {
                int skillValue = owner.GetSkillValue(skill);
                skillsStr += String.Format("\n'{0}': {1}", skill.StringId, skillValue);
            }
            //owner.GetTraitLevel(TraitObject.)
            String explanations = mobileParty.SpeedExplained.GetExplanations();
            return String.Format("MobileParty info for: '{0}':\nStringID: {1}\nSpeed: {2}\nDefaultInventoryCapacityModel: {3}\nWeight Carried: {4}\nLastCalculated Speed: {5}\nBaseSpeed: {6}\nPlayer Skills:\nExplanations:\n{7}\n",
               owner, mobileParty.StringId, mobileParty.Speed, mobileParty.InventoryCapacity, mobileParty.TotalWeightCarried, _lastCalculatedSpeed, skillsStr, explanations);
        }

        [CommandLineArgumentFunction("list", "coop.debug.mobileparty")]
        public static string ListMobileParties(List<string> args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            List<MobileParty> mobileParty = Campaign.Current.CampaignObjectManager.MobileParties.ToList();

            mobileParty.ForEach((party) =>
            {
                stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", party.StringId, party.Name));
            });

            return stringBuilder.ToString();
        }


        [CommandLineArgumentFunction("set_wage_limit_test", "coop.debug.mobileparty")]
        public static string SetWagePaymentLimit(List<string> args)
        {
            if (args.Count < 1)
            {
                return "Usage: coop.debug.mobileparty.set_wage_limit <PartyStringID>";
            }

            MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);

            if (mobileParty == null)
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }

            WageChangesSettlementPatch.SetWagePaymentLimitOverrideTest(mobileParty, 2000);


            return "SetWagePaymentLimit Tested Should invoke both";
        }
    }
}
