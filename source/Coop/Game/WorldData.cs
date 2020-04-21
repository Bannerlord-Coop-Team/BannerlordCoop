using Coop.Common;
using Coop.Game.Patch;
using Coop.Multiplayer;
using Coop.Network;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Save;

namespace Coop.Game
{
    enum ECommand
    {
        InitialWorldState
    }
    class WorldData : IWorldData
    {
        public bool Receive(byte[] rawData)
        {
            ByteReader reader = new ByteReader(rawData);
            ECommand eData = (ECommand)reader.Binary.ReadInt32();
            switch(eData)
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
            GameLoopRunner.RunOnMainThread(new Action(() => save = SaveLoad.SaveGame(TaleWorlds.Core.Game.Current, memStream)));
            Log.Info(save.ToFriendlyString());

            // Write packet
            ByteWriter writer = new ByteWriter();
            writer.Binary.Write((int)ECommand.InitialWorldState);
            writer.Binary.Write(save.Data.GetData());
            return writer.ToArray();
        }
        public bool ReceiveWorldState(byte[] rawData)
        {
            InMemDriver stream = DeserializeWorldState(rawData);
            if(stream == null)
            {
                Log.Error($"Error during world state serialization. Abort.");
                return false;
            }

            LoadGameResult loadResult = SaveLoad.LoadSaveGameData(stream);
            if(loadResult == null)
            {
                Log.Error($"Unable to load world state. Abort.");
                return false;
            }

            if(loadResult.LoadResult.Successful)
            {
                Log.Info(loadResult.ToFriendlyString());
            }
            else
            {
                Log.Error(loadResult.ToFriendlyString());
            }

            GameLoopRunner.RunOnMainThread(new Action(() => SaveLoad.LoadGame(loadResult.LoadResult)));
            return true;
        }
        public InMemDriver DeserializeWorldState(byte[] rawData)
        {
            InMemDriver memStream = new InMemDriver();
            memStream.SetBuffer(rawData);
            return memStream;
        }

        
    }
}
