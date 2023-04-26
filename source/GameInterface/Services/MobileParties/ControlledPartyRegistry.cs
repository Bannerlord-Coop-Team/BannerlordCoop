using Common.Logging;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties
{
    internal interface IControlledPartyRegistry
    {
        ISet<string> ControlledParties { get; }
        void RegisterExistingParties(IEnumerable<string> partyIds);
        bool IsControlled(string partyId);
        bool RegisterAsControlled(string partyId);
        bool RemoveAsControlled(string partyId);
    }

    internal class ControlledPartyRegistry : IControlledPartyRegistry
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ControlledPartyRegistry>();

        public ISet<string> ControlledParties { get; } = new HashSet<string>();
        public void RegisterExistingParties(IEnumerable<string> partyIds)
        {
            var badIds = new List<string>();
            foreach (var partyId in partyIds)
            {
                if (RegisterAsControlled(partyId) == false)
                {
                    // Store bad id for logging
                    badIds.Add(partyId.ToString());
                }
            }

            if (badIds.IsEmpty() == false)
            {
                Logger.Error($"Could not register the following Party ids " +
                    $"as controlled {badIds}");
            }
        }
        public bool RegisterAsControlled(string partyId) => ControlledParties.Add(partyId);

        public bool IsControlled(string partyId) => ControlledParties.Contains(partyId);

        public bool RemoveAsControlled(string partyId) => ControlledParties.Remove(partyId);
    }
}
