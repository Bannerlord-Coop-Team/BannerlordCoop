using System;
using Coop.Game.Patch;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Save;

namespace Coop.Game
{
    public static class Extensions
    {
        public static T GetGameModel<T>(this TaleWorlds.Core.Game game)
            where T : GameModel
        {
            foreach (GameModel model in game.BasicModels.GetGameModels())
            {
                T t = model as T;
                if (t != null)
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

        public static string ToFriendlyString(this SaveOutput save)
        {
            if (save.Successful)
            {
                return "Successful save.";
            }

            string sRet = "Errors during save:";
            for (int i = 0; i < save.Errors.Length; i++)
            {
                sRet += Environment.NewLine + $"[{i}] {save.Errors[i]}";
            }

            return sRet;
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

        public static byte[] GetBuffer(this InMemDriver driver)
        {
            return Utils.GetPrivateField<byte[]>(typeof(InMemDriver), "_data", driver);
        }

        public static void SetBuffer(this InMemDriver driver, byte[] buffer)
        {
            Utils.SetPrivateField(typeof(InMemDriver), "_data", driver, buffer);
        }
    }
}
