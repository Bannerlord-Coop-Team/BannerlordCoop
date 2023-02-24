using Autofac;
using GameInterface.Services;
using HarmonyLib;

namespace GameInterface
{
    public interface IGameInterface
    {
    }

    public class GameInterface : IGameInterface
    {
        private readonly Harmony harmony;
        public GameInterface()
        {
            harmony = new Harmony("com.Coop.GameInterface");
            harmony.PatchAll();
        }
    }
}
