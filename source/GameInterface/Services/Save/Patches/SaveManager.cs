using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Mod.Patch.World
{
    class CoopSaveManager
    {
        public static readonly Dictionary<string, Guid> PlayerParties = new Dictionary<string, Guid>();
    }


    [HarmonyPatch(typeof(Game), "Save")]
    class SavePatch
    {

        static void Postfix()
        {
            SavePlayers();
        }

        public static void SavePlayers()
        {
            string path = BasePath.Name + "Modules/Coop/" + Campaign.Current.UniqueGameId +
                DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");
            using (StreamWriter sw = File.CreateText(path))
            {
                foreach (string key in CoopSaveManager.PlayerParties.Keys)
                {
                    sw.WriteLine($"{key} {CoopSaveManager.PlayerParties[key]}");
                }
            }
        }
    }
}
