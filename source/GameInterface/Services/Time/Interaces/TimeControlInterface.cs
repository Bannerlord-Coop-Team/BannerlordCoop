using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Serialization;
using GameInterface.Services.CharacterCreation.Messages;
using SandBox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using System.Reflection;

namespace GameInterface.Services.Heroes.Interfaces
{
    internal interface ITimeControlInterface : IGameAbstraction
    {
        void PauseAndDisableTimeControls();
        void EnableTimeControls();
    }

    internal class TimeControlInterface : ITimeControlInterface
    {
        internal static bool TimeLock = false;

        public void PauseAndDisableTimeControls()
        {
            if (Campaign.Current == null) return;

            Campaign.Current.SetTimeControlModeLock(false);
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            TimeLock = true;
        }

        public void EnableTimeControls()
        {
            TimeLock = false;
        }
    }
}
