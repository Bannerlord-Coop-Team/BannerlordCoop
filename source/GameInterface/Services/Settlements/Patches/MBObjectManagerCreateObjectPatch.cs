using HarmonyLib;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Settlements.Patches
{
    [HarmonyPatch]
    public class MBObjectManagerCreateObjectPatch
    {
        static MethodBase TargetMethod()
        {
            var methods = typeof(MBObjectManager).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return methods.FirstOrDefault(m => m.Name == "CreateObject" && m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        }

        static void Prefix(MethodBase __originalMethod, ref string stringId)
        {
            if (__originalMethod == null) return;
            if (!__originalMethod.IsGenericMethod) return;

            var gargs = __originalMethod.GetGenericArguments();
            if (gargs == null || gargs.Length != 1) return;

            var gtype = gargs[0];
            if (!string.IsNullOrEmpty(stringId)) return;

            var logger = Common.Logging.LogManager.GetLogger<MBObjectManagerCreateObjectPatch>();
            logger.Warning("CreateObject<{type}> avec id null, réparation contextuelle", gtype.FullName);

            if (gtype == typeof(GameMenu))
            {
                var settlement = PlayerEncounter.EncounterSettlement;
                string id = null;
                if (settlement != null)
                {
                    if (settlement.IsTown) id = "town_outside";
                    else if (settlement.IsCastle) id = "castle_outside";
                    else if (settlement.IsVillage) id = "village_outside";
                }
                if (string.IsNullOrEmpty(id)) id = "town_outside";
                logger.Information("MBObjectManager.CreateObject<GameMenu> id réparé: {id}", id);
                stringId = id;
            }
        }
    }
}
