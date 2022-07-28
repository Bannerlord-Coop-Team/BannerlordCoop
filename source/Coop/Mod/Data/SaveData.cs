using Common;
using Common.Serialization;
using Coop.Mod.Serializers.Custom;
using Network;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace Coop.Mod.Data
{
    /// <summary>
    /// Package class for game data that is sent to client
    /// </summary>
    [Serializable]
    public class SaveData
    {
        #region Static
        public static implicit operator byte[](SaveData data) { return CommonSerializer.Serialize(data); }

        private static Logger Logger = LogManager.GetCurrentClassLogger();
        #endregion

        public List<Tuple<MBGUIDSerializer, Guid>> Associations { get; private set; }
        public Guid PlayerId { get; private set; }


        byte[] serializedSaveData;

        #region Properties
        public LoadResult LoadResult 
        { 
            get
            {
                if(serializedSaveData != null)
                {
                    return UnpackWorldData(serializedSaveData);
                }
                else
                {
                    throw new NullReferenceException($"{nameof(serializedSaveData)} is null.");
                }
            } 
        }
        #endregion

        public SaveData(Guid playerId)
        {
            PlayerId = playerId;
            serializedSaveData = SerializeInitialWorldState();
            Associations = GenerateMBGUIDAssociations()
                .Select(assosiation => new Tuple<MBGUIDSerializer, Guid>(new MBGUIDSerializer(assosiation.Key), assosiation.Value))
                .ToList();
        }

        #region Send
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

            // Write packet
            ByteWriter writer = new ByteWriter();
            save.Serialize(writer);
            return writer.ToArray();
        }
        #endregion

        #region Receive
        public void AssosiateIds()
        {
            // Desearialize MBGUIDS
            Dictionary<MBGUID, Guid> assosiations = Associations.ToDictionary(
                assosiation => (MBGUID)assosiation.Item1.Deserialize(), 
                assosiation => assosiation.Item2);

            // Associate Objects
            AssociateObjectsFromMBGUID(assosiations);
        }

        private LoadResult UnpackWorldData(byte[] data)
        {
            ArraySegment<byte> rawData = new ArraySegment<byte>(data);
            Logger.Debug("Total: {sizeInMemory} bytes.", rawData.Count);
            InMemDriver stream = DeserializeWorldState(rawData);
            if (stream == null)
            {
                Logger.Error("Error during world state serialization. Abort.");
                return null;
            }

            LoadResult loadResult = SaveLoad.LoadSaveGameData(stream);
            if (loadResult == null)
            {
                Logger.Error("Unable to load world state. Abort.");
                return null;
            }

            if (loadResult.Successful)
            {
                Logger.Info(loadResult.ToFriendlyString());
            }
            else
            {
                Logger.Error(loadResult.ToFriendlyString());
            }

            return loadResult;
        }

        private InMemDriver DeserializeWorldState(ArraySegment<byte> rawData)
        {
            InMemDriver memStream = new InMemDriver();
            memStream.SetBuffer(rawData.ToArray());
            return memStream;
        }

        public static SaveData Deserialize(ArraySegment<byte> payload)
        {
            return (SaveData)CommonSerializer.Deserialize(payload);
        }

        /// <summary>
        /// Creates dictionary with associations to object MBGUID to/from Guid
        /// </summary>
        /// <exception cref="Exception">Collision exception of 2 different objects with same Guid</exception>
        /// <returns>Dictionary of MBGUID to Guid associations</returns>
        public static Dictionary<MBGUID, Guid> GenerateMBGUIDAssociations()
        {
            Dictionary<MBGUID, Guid> result = new Dictionary<MBGUID, Guid>();
            foreach (KeyValuePair<Guid, WeakReference<object>> pair in CoopObjectManager.Objects)
            {
                if (!pair.Value.TryGetTarget(out object obj))
                {
                    continue;
                }

                if (obj is MBObjectBase mbObject)
                {
                    if (!result.ContainsKey(mbObject.Id))
                    {
                        result.Add(mbObject.Id, pair.Key);
                    }
                    else
                    {
                        MBObjectBase object1 = mbObject;
                        MBObjectBase object2 = CoopObjectManager.GetObject<MBObjectBase>(result[mbObject.Id]);
                        throw new Exception($"Key Collision {object1} and {object2}");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Registers existing objects with <see cref="CoopObjectManager"/> using an associated 
        /// dictionary to lookup Guid values from MBGUID.
        /// </summary>
        /// <param name="GuidAssociations">Associated dictionary to lookup Guid values from MBGUID</param>
        /// <returns>true if all Guid associations were found, false otherwise</returns>
        public static bool AssociateObjectsFromMBGUID(Dictionary<MBGUID, Guid> GuidAssociations)
        {
            foreach (MBObjectBase mbObject in Hero.AllAliveHeroes)
            {
                if (GuidAssociations.ContainsKey(mbObject.Id))
                {
                    CoopObjectManager.Assert(GuidAssociations[mbObject.Id], mbObject);
                    GuidAssociations.Remove(mbObject.Id);
                }
            }

            foreach (MBObjectBase mbObject in Hero.DeadOrDisabledHeroes)
            {
                if (GuidAssociations.ContainsKey(mbObject.Id))
                {
                    CoopObjectManager.Assert(GuidAssociations[mbObject.Id], mbObject);
                    GuidAssociations.Remove(mbObject.Id);
                }
            }

            foreach (MBObjectBase mbObject in Settlement.All)
            {
                if (GuidAssociations.ContainsKey(mbObject.Id))
                {
                    CoopObjectManager.Assert(GuidAssociations[mbObject.Id], mbObject);
                    GuidAssociations.Remove(mbObject.Id);
                }
            }

            foreach (MBObjectBase mbObject in Town.AllFiefs)
            {
                if (GuidAssociations.ContainsKey(mbObject.Id))
                {
                    CoopObjectManager.Assert(GuidAssociations[mbObject.Id], mbObject);
                    GuidAssociations.Remove(mbObject.Id);
                }
            }

            foreach (MBObjectBase mbObject in Village.All)
            {
                if (GuidAssociations.ContainsKey(mbObject.Id))
                {
                    CoopObjectManager.Assert(GuidAssociations[mbObject.Id], mbObject);
                    GuidAssociations.Remove(mbObject.Id);
                }
            }

            foreach (MBObjectBase mbObject in Campaign.Current.Factions)
            {
                if (GuidAssociations.ContainsKey(mbObject.Id))
                {
                    CoopObjectManager.Assert(GuidAssociations[mbObject.Id], mbObject);
                    GuidAssociations.Remove(mbObject.Id);
                }
            }

            foreach (MBObjectBase mbObject in Kingdom.All)
            {
                if (GuidAssociations.ContainsKey(mbObject.Id))
                {
                    CoopObjectManager.Assert(GuidAssociations[mbObject.Id], mbObject);
                    GuidAssociations.Remove(mbObject.Id);
                }
            }

            foreach (MBObjectBase mbObject in MobileParty.All)
            {
                if (GuidAssociations.ContainsKey(mbObject.Id))
                {
                    CoopObjectManager.Assert(GuidAssociations[mbObject.Id], mbObject);
                    GuidAssociations.Remove(mbObject.Id);
                }
            }

            foreach (CharacterObject mbObject in CharacterObject.All)
            {
                if (GuidAssociations.ContainsKey(mbObject.Id))
                {
                    CoopObjectManager.Assert(GuidAssociations[mbObject.Id], mbObject);
                    GuidAssociations.Remove(mbObject.Id);
                }
            }

            foreach (MBObjectBase mbObject in Clan.All)
            {
                if (GuidAssociations.ContainsKey(mbObject.Id))
                {
                    CoopObjectManager.Assert(GuidAssociations[mbObject.Id], mbObject);
                    GuidAssociations.Remove(mbObject.Id);
                }
            }

            

#if DEBUG
            List<Guid> unresolved = new List<Guid>(GuidAssociations.Values);
            if (GuidAssociations.Count > 0)
            {
                CoopClient.Instance.Session.Connection.Send(
                    new Network.Protocol.Packet(
                        Network.Protocol.EPacket.BadID,
                        CommonSerializer.Serialize(unresolved))
                    );
            }
#endif

            return GuidAssociations.Count == 0;
        }
        #endregion
    }
}
