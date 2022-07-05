using HarmonyLib;
using LiteNetLib;
using LiteNetLib.Utils;
using MissionsShared;
using ProtoBuf;
using SandBox.BoardGames.MissionLogics;
using SandBox.Conversation.MissionLogics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                if (agent.Character.IsPlayerCharacter)
                {
                    int index = agent.Index;
                    InformationManager.ShowInquiry(new InquiryData("Board Game Challenge", string.Empty, true, true, "Challenge", "Pussy out",
                        new Action(() => { SendGameRequest(index); }) , new Action(() => { })));

                    return false;
                }

                return true;
            }
        }

        public static void AcceptGameRequest(int index)
        {
            BoardGameChallenge boardGameChallenge = new BoardGameChallenge();
            boardGameChallenge.ChallengeResponse = true;
            boardGameChallenge.OtherAgentId = ClientAgentManager.Instance().GetIdFromIndex(index);

            var netDataWriter = new NetDataWriter();
            netDataWriter.Put((uint)MessageType.BoardGameChallenge);

            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix<BoardGameChallenge>(memoryStream, boardGameChallenge, PrefixStyle.Fixed32BigEndian);
                netDataWriter.Put(memoryStream.ToArray());
            }

            MissionBoardGameLogic boardGameLogic = Mission.Current.GetMissionBehavior<MissionBoardGameLogic>();

            boardGameLogic.SetBoardGame(Settlement.CurrentSettlement.Culture.BoardGame);
            boardGameLogic.SetStartingPlayer(true);
            boardGameLogic.StartBoardGame();
            //boardGameLogic.Board.GetType().GetProperty("RotateBoard", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(boardGameLogic.Board, true);

            MissionNetworkBehavior.client.SendToAll(netDataWriter, DeliveryMethod.ReliableSequenced);
        }

        private static void SendGameRequest(int index)
        {
            InformationManager.DisplayMessage(new InformationMessage("Challenge Sent"));
            BoardGameChallenge boardGameChallenge = new BoardGameChallenge();

            boardGameChallenge.ChallengeRequest = true;
            boardGameChallenge.OtherAgentId = ClientAgentManager.Instance().GetIdFromIndex(index);

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
