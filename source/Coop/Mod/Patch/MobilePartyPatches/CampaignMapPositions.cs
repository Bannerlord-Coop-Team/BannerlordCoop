using System;
using System.Collections.Generic;
using Common;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using HarmonyLib;
using Sync.Value;
using TaleWorlds.CampaignSystem;
using TaleWorlds.DotNet;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Patch.MobilePartyPatches
{
    /// <summary>
    ///     On the server, this patch writes all changes to the <see cref="MobileParty"/> positions during one tick to
    ///     a buffer. The buffer will then be written to the authoritative state at the end of a tick.
    /// </summary>
    [HarmonyPatch(typeof(Managed), "ApplicationTick")]
    public class CampaignMapPositions
    {
        private static FieldChangeStack m_Buffer = null;
        
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (Coop.IsServer)
            {
                if (m_Buffer == null)
                {
                    m_Buffer = new FieldChangeStack();
                }
                
                m_Buffer.PushMarker();
                WriteAllPositionsToBuffer();
            }
            return true;
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            if(m_Buffer != null)
            {
                WriteAllPositionsToBuffer();
                FieldChangeBuffer buffer = m_Buffer.PopUntilMarker(false);
                m_Buffer = null;

                if (Coop.IsServer)
                {
                    ProcessBufferedChanges(buffer);
                }
            }
        }
        private static void WriteAllPositionsToBuffer()
        {
            if (!Coop.IsCoopGameSession() ||
                MBObjectManager.Instance == null)
            {
                return;
            }
            
            foreach (var item in CampaignMapMovement.Instances)
            {
                MobileParty party = CoopObjectManager.GetObject(item.Key) as MobileParty;
                if (party != null)
                {
                    m_Buffer.PushValue(CampaignMapMovement.MapPosition, party);
                }
            }
        }
        private static void ProcessBufferedChanges(FieldChangeBuffer buffer)
        {
            var changes = buffer.FetchChanges();
            List<Tuple<MobileParty, Vec2>> positionChanges = new List<Tuple<MobileParty, Vec2>>();
            if (changes.ContainsKey(CampaignMapMovement.MapPosition))
            {
                foreach (var change in changes[CampaignMapMovement.MapPosition])
                {
                    if (change.Key is MobileParty party &&
                        change.Value.OriginalValue is Vec2 before &&
                        change.Value.RequestedValue is Vec2 request)
                    {
                        if (!Compare.CoordinatesEqual(before, request))
                        {
                            positionChanges.Add(new Tuple<MobileParty, Vec2>(party, request));
                        }
                    }
                }
            }

            CoopServer.Instance.Persistence?.MobilePartyEntityManager.UpdatePosition(positionChanges);
        }
    }
}