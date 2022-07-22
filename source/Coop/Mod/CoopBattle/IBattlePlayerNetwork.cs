using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.CoopBattle
{
    public interface IBattlePlayerNetwork
    {

        void DetectTimeout();

        void Disconnect();

        void LeaveBattle();

        void RecieveConnect(MessagePayload<ConnectMessage>);

        void RecieveDisconnect(MessagePayload<DisconnectMessage>);

        void SendNumberControlled();

        void RecieveNumberControlled(MessagePayload<AgentControlledAmountMessage>);

        void ClaimControl();

        void RecieveControlClaim(MessagePayload<ClaimControlMessage>);



    }
}
