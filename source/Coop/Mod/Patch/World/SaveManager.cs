using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TaleWorlds.PlayerServices;
using TaleWorlds.SaveSystem.Save;

namespace Coop.Mod.Patch.World
{
    [HarmonyPatch(typeof(Game), "Save")]
    class SaveManager
    {
        public static readonly Dictionary<string, MBGUID> PlayerParties = new Dictionary<string, MBGUID>();
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
                foreach(string key in PlayerParties.Keys)
                {
                    sw.Write($"{key} {PlayerParties[key]}");
                }                
            }
        }

        //public static void LoadPlayers()
        //{
        //    string pattern = BasePath.Name + "Modules/Coop/" + Campaign.Current.UniqueGameId + 
        //    string path = BasePath.Name + "Modules/Coop/" + Campaign.Current.UniqueGameId +
        //        DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");

        //    DateTime.FromFileTime()
        //}
    }
}
