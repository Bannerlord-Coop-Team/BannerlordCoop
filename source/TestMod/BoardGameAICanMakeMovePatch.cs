using HarmonyLib;
using SandBox.BoardGames.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CoopTestMod
{
    [HarmonyPatch]
    public class Board
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BoardGameAIBase), nameof(BoardGameAIBase.CanMakeMove));
            yield return AccessTools.Method(typeof(BoardGameAIBase), nameof(BoardGameAIBase.WantsToForfeit));
            yield return AccessTools.Method(typeof(BoardGameAISeega), nameof(BoardGameAIBase.WantsToForfeit));
        }

        static bool Postfix(bool result)
        {
            return false;
        }
    }

}
