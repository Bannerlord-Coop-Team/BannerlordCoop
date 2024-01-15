using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    public record CalculateSpeed : ICommand
    {
        public float Speed { get; }

        public CalculateSpeed(float speed)
        {
            Speed = speed;
        }
    }
}
