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
        public GameInterface()
        {
            Harmony harmony = new Harmony("com.Coop.GameInterface");
            harmony.PatchAll();
        }
    }
}
