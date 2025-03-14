using GameInterface.Utils.LocalEvents;
@UsingDeclarations@

namespace DynamicSync
{
    public record @MessageType@ : GenericEvent<@ClassType@, @MemberType@>
    {
        public @MessageType@()
        {
        }

        public @MessageType@(@ClassType@ instance, @MemberType@ value) : base(instance, value)
        {
        }
    }
}
