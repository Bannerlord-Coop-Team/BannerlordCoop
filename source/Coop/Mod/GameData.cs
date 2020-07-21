using System;
using System.Linq;
using Network;
using Network.Infrastructure;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace Coop.Mod
{
    internal enum ECommand
    {
        InitialWorldState
    }

    // TODO rename interface to more relevant name
    internal class GameData : ISaveData
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public bool RequiresInitialWorldData => Coop.IsClientReqWorldData && !Coop.IsServer;
        // TODO determine if client needs to create a character
        public bool RequiresCharacterCreation => !Coop.IsServer;

        public LoadResult LoadResult { get; private set; }

        public bool Receive(ArraySegment<byte> rawData)
        {
            if (rawData.Count == 0)
            {
                throw new Exception("Payload is empty.");
            }

            ByteReader reader = new ByteReader(rawData);
            ECommand eData = (ECommand) reader.Binary.ReadInt32();
            switch (eData)
            {
                case ECommand.InitialWorldState:
                    return ReceiveWorldState(
                        new ArraySegment<byte>(
                            rawData.Array,
                            (int) reader.Position,
                            (int) reader.RemainingBytes));
                default:
                    return false;
            }
        }

        public byte[] SerializeInitialWorldState()
        {
            CampaignEventDispatcher.Instance.OnBeforeSave();

            // Save to memory
            InMemDriver memStream = new InMemDriver();
            SaveGameData save = null;
            GameLoopRunner.RunOnMainThread(() => save = SaveLoad.SaveGame(Game.Current, memStream));
            if (save == null)
            {
                throw new Exception("Saving the game failed. Abort.");
            }

            Logger.Info(save);

            // Write packet
            ByteWriter writer = new ByteWriter();
            writer.Binary.Write((int) ECommand.InitialWorldState);
            save.Serialize(writer);
            return writer.ToArray();
        }

        private bool ReceiveWorldState(ArraySegment<byte> rawData)
        {
            Logger.Debug("Total: {sizeInMemory} bytes.", rawData.Count);
            InMemDriver stream = DeserializeWorldState(rawData);
            if (stream == null)
            {
                Logger.Error("Error during world state serialization. Abort.");
                return false;
            }

            LoadGameResult loadResult = SaveLoad.LoadSaveGameData(stream);
            if (loadResult == null)
            {
                Logger.Error("Unable to load world state. Abort.");
                return false;
            }

            if (loadResult.LoadResult.Successful)
            {
                Logger.Info(loadResult.ToFriendlyString());
            }
            else
            {
                Logger.Error(loadResult.ToFriendlyString());
            }

            GameLoopRunner.RunOnMainThread(() => { LoadResult = loadResult.LoadResult; });
            return true;
        }

        private InMemDriver DeserializeWorldState(ArraySegment<byte> rawData)
        {
            InMemDriver memStream = new InMemDriver();
            memStream.SetBuffer(rawData.ToArray());
            return memStream;
        }
    }
}
