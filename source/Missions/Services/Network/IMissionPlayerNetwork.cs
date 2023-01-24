<<<<<<< HEAD
﻿using Common.Messaging;
=======
﻿using Common;
>>>>>>> NetworkEvent-refactor
using Missions.Messages;

namespace Missions.Services.Network
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
