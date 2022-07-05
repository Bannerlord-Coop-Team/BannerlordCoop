using System;
using System.Collections.Generic;
using System.Text;

namespace MissionsShared
{
    public enum MessageType : uint
    {
        EnterLocation,
        ExitLocation,
        PlayerSync,
        ConnectionId,
        PeersAtLocation,
        PlayerDamage, 
        AddAgent,
        BoardGameChallenge,
        BoardGame,
        PawnCapture
    }
}
