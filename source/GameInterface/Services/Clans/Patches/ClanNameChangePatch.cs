using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(Clan))]
public class ClanNameChangePatch
{
    private static ILogger Logger = LogManager.GetLogger<ClanNameChangePatch>();

    [HarmonyPatch(nameof(Clan.ChangeClanName))]
    [HarmonyPrefix]
    public static bool ChangeClanNamePrefix(Clan __instance, TextObject name, TextObject informalName)
    {
        var message = new ChangeClanName(__instance, name, informalName);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    public static void ChangeClanNameOverride(Clan clan, TextObject name, TextObject informalName)
    {
        clan.Name = name;
        clan.InformalName = informalName;
    }
}