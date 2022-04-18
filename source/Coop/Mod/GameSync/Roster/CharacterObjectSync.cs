using Common;
using CoopFramework;
using NLog;
using Sync.Call;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.GameSync
{
    public class CharacterObjectSync
    {
        /// <summary>
        ///     !! WORKAROUND !!
        ///     As of right now, there's a bit of a mess with getting all CharacterObject
        ///     registered on all cients with <see cref="CoopObjectManager"/>. This method checks if
        ///     the object is already registered. If it is, the method does nothing.
        ///     
        ///     If the object ist not yet registered, it adds it to the local <see cref="CoopObjectManager"/>
        ///     and issues an RPC with a serialized copy to add it remote as well.
        ///     
        ///     This is not really a good solution because it will lead to duplicate CharacterObjects. 
        ///     All CharacterObject need to be registered with <see cref="CoopObjectManager"/> in
        ///     all game instances.
        /// </summary>
        /// <param name="character"></param>
        public static void AssertCharacterIsRegistered(CharacterObject character)
        {
            if (!CoopObjectManager.ContainsElement(character))
            {
                Logger.Trace($"Missing from CoopObjectManager: {character}. Sending.");
                Guid guid = CoopObjectManager.AddObject(character);
                CoopServer.Instance.Synchronization.Broadcast(OnRegisterCharacterRPC.Id, null, new object[] { character, guid });
            }
        }

        [PatchInitializer]
        private static void InitRPC()
        {
            OnRegisterCharacterRPC = new Invokable(typeof(CharacterObjectSync).GetMethod(nameof(CharacterObjectSync.OnRegisterCharacter), BindingFlags.NonPublic | BindingFlags.Static), EInvokableFlag.TransferArgumentsByValue);
        }

        /// <summary>
        ///     RPC that is called to register a new character in the CoopObjectManager
        /// </summary>
        private static Invokable OnRegisterCharacterRPC;
        private static void OnRegisterCharacter(CharacterObject character, Guid guid)
        {
            if (Coop.IsServer)
            {
                return;
            }

            CoopObjectManager.Assert(guid, character);
        }

        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();
    }
}
