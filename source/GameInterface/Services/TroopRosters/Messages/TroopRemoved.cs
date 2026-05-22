using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

public readonly struct TroopRemoved : IEvent
{
    public TroopRoster TroopRoster { get; }
    public CharacterObject Troop { get; }
    public int NumberToRemove { get; }
    public int Xp { get; }

    public TroopRemoved(TroopRoster troopRoster, CharacterObject troop, int numberToRemove, int xp)
    {
        TroopRoster = troopRoster;
        Troop = troop;
        NumberToRemove = numberToRemove;
        Xp = xp;
    }
}