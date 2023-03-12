using Coop.Mod.Patch.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Behaviour
{
    public class GameLoadedBehaviour : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, GameLoaded);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void GameLoaded(CampaignGameStarter gameStarter)
        {
        }

        private static void LoadPlayers()
        {
            string pattern = $"({Campaign.Current.UniqueGameId})([0-9-]+)";
            string path = BasePath.Name + "Modules/Coop/";

            Dictionary<DateTime, string> creationTimes = new Dictionary<DateTime, string>();

            foreach (string filepath in Directory.GetFiles(path))
            {
                if (filepath.Contains(Campaign.Current.UniqueGameId))
                {
                    creationTimes.Add(File.GetCreationTime(filepath), filepath);
                }
            }
            if (creationTimes.Count > 0)
            {
                DateTime latestDate = creationTimes.Max(kvp => kvp.Key);
                string filePath = creationTimes[latestDate];

                foreach (string line in File.ReadAllLines(filePath))
                {
                    string[] data = line.Split(' ');

                    string clientId = data[0];
                    Guid partyGUID = Guid.Parse(data[1]);

                    if (CoopSaveManager.PlayerParties.ContainsKey(clientId))
                    {
                        if (CoopSaveManager.PlayerParties[clientId] != partyGUID)
                        {
                            throw new Exception("Party GUID does not equal saved ID when loading from file.");
                        }
                    }
                    else
                    {
                        CoopSaveManager.PlayerParties.Add(clientId, partyGUID);
                    }
                }
            }
        }
    }
}
