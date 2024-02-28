using Common.Messaging;
using GameInterface.Policies;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Utils.AutoSync.Example;
public class PatchExample
{
    private static bool Prefix(MobileParty __instance, int value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        if (ModInformation.IsClient)
        {
            return true;
        }

        (new MessageData(__instance.StringId, value)).GetType();

        //MessageBroker.Instance.Publish(__instance, new EventMessage(
        //    new MessageData(__instance.StringId, value)));

        return true;
    }
}
