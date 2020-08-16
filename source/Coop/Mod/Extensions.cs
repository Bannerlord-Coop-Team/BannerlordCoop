using System;
using System.Collections.Generic;
using System.Reflection;
using Coop.Mod.Patch;
using Sync.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace Coop.Mod
{
    public static class Extensions
    {
        public static T GetGameModel<T>(this Game game)
            where T : GameModel
        {
            foreach (GameModel model in game.BasicModels.GetGameModels())
            {
                if (model is T t)
                {
                    return t;
                }
            }

            return null;
        }

        public static bool IsPlayerControlled(this MobileParty party)
        {
            return CoopClient.Instance.GameState.IsPlayerControlledParty(party);
        }

        public static string ToFriendlyString(this LoadGameResult loadResult)
        {
            if (!loadResult.LoadResult.Successful)
            {
                return "Error during load.";
            }

            string sRet = "Loading successful.";
            if (loadResult.ModuleCheckResults.Count > 0)
            {
                sRet += "Module missmatches in loaded file:";
                for (int i = 0; i < loadResult.ModuleCheckResults.Count; i++)
                {
                    ModuleCheckResult module = loadResult.ModuleCheckResults[i];
                    sRet += Environment.NewLine + $"[{i}] {module.ModuleName}: {module.Type}.";
                }
            }

            return sRet;
        }

        public static string ToFriendlyString(this LoadResult loadResult)
        {
            string sRet = "Loading " + (loadResult.Successful ? "success. " : "failed. ");
            if (loadResult.MetaData == null)
            {
                sRet += "No meta data";
            }
            else
            {
                sRet += loadResult.MetaData;
            }

            sRet += Environment.NewLine;

            sRet += loadResult.Errors.Length + " errors:";
            foreach (LoadError error in loadResult.Errors)
            {
                sRet += Environment.NewLine + error.ToFriendlyString();
            }

            return sRet;
        }

        public static string ToFriendlyString(this LoadError error)
        {
            return $"Error: {error.Message}";
        }

        public static byte[] GetBuffer(this InMemDriver driver)
        {
            return Utils.GetPrivateField<byte[]>(typeof(InMemDriver), "_data", driver);
        }

        public static void SetBuffer(this InMemDriver driver, byte[] buffer)
        {
            Utils.SetPrivateField(typeof(InMemDriver), "_data", driver, buffer);
        }

        public static long GetNumTicks(this CampaignTime time)
        {
            return m_GetNumTicks.Invoke(time);
        }

        private static Func<CampaignTime, long> m_GetNumTicks = InvokableFactory.CreateGetter<CampaignTime, long>(
            typeof(CampaignTime).GetField("_numTicks", 
                BindingFlags.NonPublic | BindingFlags.Instance));
    }
}
