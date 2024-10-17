namespace GameInterface.Services.BesiegerCamps.Messages.Collection
{
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