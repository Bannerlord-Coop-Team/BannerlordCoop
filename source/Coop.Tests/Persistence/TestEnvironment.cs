using System;
using System.Collections.Generic;
using Coop.Game.Persistence;
using Coop.Sync;
using HarmonyLib;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Tests
{
    internal class TestEnvironmentClient : IEnvironmentClient
    {
        public readonly TestableField<Vec2> TargetPosition_Test = new TestableField<Vec2>();
        public Field TargetPosition => TargetPosition_Test.Field;

        public readonly TestableField<CampaignTimeControlMode> TimeControlMode_Test = new TestableField<CampaignTimeControlMode>();
        public Field TimeControlMode => TimeControlMode_Test.Field;

        public object GetTimeController()
        {
            return TimeControlMode_Test;
        }

        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            throw new NotImplementedException();
        }
    }

    internal class TestEnvironmentServer : IEnvironmentServer
    {
    }
}
