using HarmonyLib;
using LiteNetLib;
using LiteNetLib.Utils;
using MissionsShared;
using ProtoBuf;
using SandBox.BoardGames;
using SandBox.BoardGames.MissionLogics;
using SandBox.Conversation.MissionLogics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CoopTestMod
{
    internal class AgentInteractionPatch
    {
        [HarmonyPatch(typeof(MissionConversationLogic), "OnAgentInteraction")]
        public class OnAgentInteractionPatch
        {
            static bool Prefix(ref Agent userAgent, ref Agent agent)
            {
                if (!agent.Character.IsPlayerCharacter)
                {
                    return true;

                }

                if (!ClientAgentManager.Instance().IsNetworkAgent(agent.Index))
                {
                    return true;
                }

                int senderIndex = userAgent.Index;
                int otherIndex = agent.Index;
                InformationManager.DisplayMessage(new InformationMessage($"Interact with agent {otherIndex}"));

                InformationManager.ShowInquiry(new InquiryData("Board Game Challenge", string.Empty, true, true, "Challenge", "Pussy out",
                    new Action(() => { SendGameRequest(senderIndex, otherIndex); }), new Action(() => { })));

                return false;

            }
        }

        public static void AcceptGameRequest(string senderID, string otherID)
        {
            BoardGameChallenge boardGameChallenge = new BoardGameChallenge();

            boardGameChallenge.ChallengeResponse = true;
            boardGameChallenge.OtherAgentId = otherID;

            var netDataWriter = new NetDataWriter();
            netDataWriter.Put((uint)MessageType.BoardGameChallenge);

            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix<BoardGameChallenge>(memoryStream, boardGameChallenge, PrefixStyle.Fixed32BigEndian);
                netDataWriter.Put(memoryStream.ToArray());
            }

            MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();

            boardGameLogic.SetBoardGame(Settlement.CurrentSettlement.Culture.BoardGame);

            //if ((PlayerTurn)(boardGameLogic.GetType().GetField("_startingPlayer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(boardGameLogic)) != PlayerTurn.PlayerOne)
            //{
            //    boardGameLogic.SetStartingPlayer(true);
            //}

            boardGameLogic.SetStartingPlayer(true);
            boardGameLogic.StartBoardGame();
            Agent opposingAgent = ClientAgentManager.Instance().GetNetworkAgent(senderID).Agent;

            boardGameLogic.GetType().GetProperty("OpposingAgent", BindingFlags.Public | BindingFlags.Instance).SetValue(boardGameLogic, opposingAgent);

            MissionNetworkBehavior.client.SendToAll(netDataWriter, DeliveryMethod.ReliableSequenced);
        }

        private static void SendGameRequest(int senderIndex, int otherIndex)
        {
            InformationManager.DisplayMessage(new InformationMessage("Challenge Sent"));
            BoardGameChallenge boardGameChallenge = new BoardGameChallenge();

            boardGameChallenge.ChallengeRequest = true;
            boardGameChallenge.OtherAgentId = ClientAgentManager.Instance().GetIdFromIndex(otherIndex);
            boardGameChallenge.SenderAgentId = ClientAgentManager.Instance().GetIdFromIndex(senderIndex);



            var netDataWriter = new NetDataWriter();
            netDataWriter.Put((uint)MessageType.BoardGameChallenge);

            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix<BoardGameChallenge>(memoryStream, boardGameChallenge, PrefixStyle.Fixed32BigEndian);
                netDataWriter.Put(memoryStream.ToArray());
            }

            MissionNetworkBehavior.client.SendToAll(netDataWriter, DeliveryMethod.ReliableOrdered);
            
        }

    }
}
