using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TaleWorlds.PlayerServices;
using TaleWorlds.SaveSystem.Save;

namespace Coop.Mod.Patch.World
{
    class CoopSaveManager
    {
        public static readonly Dictionary<string, MBGUID> PlayerParties = new Dictionary<string, MBGUID>();
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

    [HarmonyPatch(typeof(Game), "LoadSaveGame")]
    class LoadPatch
    {
        static void Postfix()
        {
            LoadPlayers();
        }

        public static void LoadPlayers()
        {
            string pattern = $"({Campaign.Current.UniqueGameId})([0-9-]+)";
            string path = BasePath.Name + "Modules/Coop/";

            Dictionary<DateTime, string> creationTimes = new Dictionary<DateTime, string>();

            foreach (string filepath in Directory.GetFiles(path)) {
                if (filepath.Contains(Campaign.Current.UniqueGameId))
                {
                    creationTimes.Add(File.GetCreationTime(filepath), filepath);
                }                
            }
            if(creationTimes.Count > 0)
            {
                DateTime latestDate = creationTimes.Max(kvp => kvp.Key);
                string filePath = creationTimes[latestDate];

                foreach (string line in File.ReadAllLines(filePath))
                {
                    string[] data = line.Split(' ');

                    string clientId = data[0];
                    uint partyId = uint.Parse(data[1]);

                    MBGUID partyGUID = new MBGUID(partyId);

                    CoopSaveManager.PlayerParties.Add(clientId, partyGUID);
                }
            }
        }
    }
}
