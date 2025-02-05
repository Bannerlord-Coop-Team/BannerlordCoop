using ProtoBuf;

namespace GameInterface.Services.CraftingService.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record CraftingCreatedData
    {
        [ProtoMember(1)]
        public string CraftingId { get; }
        [ProtoMember(2)]
        public string CraftingTemplateId { get; }
        [ProtoMember(3)]
        public string CultureId { get; }
        [ProtoMember(4)]
        public string Name { get; }

        public CraftingCreatedData(string craftingId, string craftingTemplateId, string cultureId, string name)
        {
            CraftingId = craftingId;
            CraftingTemplateId = craftingTemplateId;
            CultureId = cultureId;
            Name = name;
        }
    }
}
