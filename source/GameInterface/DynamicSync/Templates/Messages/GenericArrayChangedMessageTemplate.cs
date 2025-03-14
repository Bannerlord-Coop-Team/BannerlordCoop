using GameInterface.Utils.LocalEvents;
@UsingDeclarations@

namespace DynamicSync
{
    public record @MessageType@ : GenericArrayChangedEvent<@ClassType@, @MemberType@>
    {
        public @MessageType@(@ClassType@ instance, @MemberType@ value, int index) : base(instance, value, index)
        {
        }
    }
}
