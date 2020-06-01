using System;
using System.Linq;
using Coop.Multiplayer;
using Coop.Network;
using NLog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Save;

namespace Coop.Mod
{
    internal enum ECommand
    {
        InitialWorldState
    }

    // TODO rename more relevant
    internal class SaveData : ISaveData
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public bool RequiresInitialWorldData => Coop.IsClient && !Coop.IsServer;

        public bool Receive(ArraySegment<byte> rawData)
        {
            ByteReader reader = new ByteReader(rawData);
            ECommand eData = (ECommand) reader.Binary.ReadInt32();
            switch (eData)
            {
                case ECommand.InitialWorldState:
                    return ReceiveWorldState(rawData);
                default:
                    return false;
            }
        }

        public byte[] SerializeInitialWorldState()
        {
            CampaignEventDispatcher.Instance.OnBeforeSave();

            // Save to memory
            InMemDriver memStream = new InMemDriver();
            SaveOutput save = null;
            GameLoopRunner.RunOnMainThread(
                () => save = SaveLoad.SaveGame(TaleWorlds.Core.Game.Current, memStream));
            Logger.Info(save.ToFriendlyString());

            // Write packet
            ByteWriter writer = new ByteWriter();
            writer.Binary.Write((int) ECommand.InitialWorldState);
            writer.Binary.Write(save.Data.GetData());
            return writer.ToArray();
        }

        private bool ReceiveWorldState(ArraySegment<byte> rawData)
        {
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

            GameLoopRunner.RunOnMainThread(() => SaveLoad.LoadGame(loadResult.LoadResult));
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
