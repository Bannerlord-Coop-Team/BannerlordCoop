using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.BesiegerCamps.Messages.Collection
{
    //public record BesiegerPartyData(string BesiegerCampId, string BesiegerPartyId); // Need that IsExternalInit for this sweet syntax

    public record BesiegerPartyData
    {
        public string BesiegerCampId { get; }
        public string BesiegerPartyId { get; }

        public BesiegerPartyData(string besiegerCampId, string besiegerPartyId)
        {
            BesiegerCampId = besiegerCampId;
            BesiegerPartyId = besiegerPartyId;
        }

    }
}
