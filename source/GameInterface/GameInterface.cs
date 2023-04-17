using Autofac;
using GameInterface.Services;
using HarmonyLib;
using System;
using System.Reflection;

namespace GameInterface
{
    public interface IGameInterface
    {
    }

    public class GameInterface : IGameInterface
    {
        private static Harmony harmony;
        public GameInterface()
        {
            if (harmony != null) return;

            harmony = new Harmony("com.Coop.GameInterface");
            harmony.PatchAll(typeof(GameInterface).Assembly);
        }
    }
}
