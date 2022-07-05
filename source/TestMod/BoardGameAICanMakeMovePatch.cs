using HarmonyLib;
using SandBox.BoardGames.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoopTestMod
{
    [HarmonyPatch(typeof(BoardGameAIBase), nameof(BoardGameAIBase.CanMakeMove))]
    public class Board
    {
        static bool Postfix(bool result)
        {
            return false;
        }
    }

}
