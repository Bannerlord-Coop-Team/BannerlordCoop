using Common;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Missions
{
    public interface IMissionPlayerNetwork
    {
        void DetectTimeout();

        void Disconnect();

        void LeaveBattle();

        void RecieveConnect(MessagePayload<ConnectMessage> message);

        void RecieveDisconnect(MessagePayload<DisconnectMessage> message);

        void SendNumberControlled();

        void RecieveNumberControlled(MessagePayload<AgentControlledAmountMessage> message);

        void ClaimControl();

        void RecieveControlClaim(MessagePayload<ClaimControlMessage> message);
    }
}
