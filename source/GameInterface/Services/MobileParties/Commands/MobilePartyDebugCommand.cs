using System;
using System.Collections.Generic;
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
            Hero owner = mobileParty.Owner;

            if(mobileParty == null )
            {
                return string.Format("ID: '{0}' not found", args[0]);
            }
            /*
             * Here are some classes/methods to 
             * give you a head start MobileParty.CalculateSpeedForPartyUnified, 
             * DefaultPartySpeedCalculatingModel, and DefaultInventoryCapacityModel.
             * My guess is that the player party does not have the same inventory or skills on the client/server
             */
            FieldInfo typeMobileParty = typeof(MobileParty).GetField("_lastCalculatedSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var _lastCalculatedSpeed = typeMobileParty.GetValue(mobileParty);



            // FieldInfo typeHero = typeof(Hero).GetField("_characterAttributes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // var _characterAttributes = typeHero.GetValue(mobileParty.Owner);  

            String skillsStr = "";
            foreach(SkillObject skill in Skills.All)
            {
                int skillValue = owner.GetSkillValue(skill);
                skillsStr += String.Format("'{0}': {1}\n", skill.StringId, skillValue);
            }
            //owner.GetTraitLevel(TraitObject.)
            String explanations = mobileParty.SpeedExplained.GetExplanations();
            return String.Format("MobileParty info for: '{0}':\nStringID: {1}\nSpeed: {2}\nDefaultInventoryCapacityModel: {3}\nWeight Carried: {4}\nLastCaulated Speed: {5}\nBaseSpeed: {6}\nPlayer Skills\n Explanations:\n{7}",
               owner, mobileParty.StringId, mobileParty.Speed, mobileParty.InventoryCapacity, mobileParty.TotalWeightCarried, _lastCalculatedSpeed, skillsStr, explanations);
        }
    }
}
