using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core
{
    public enum NetworkRole
    {
        None,
        Client,
        Server,
        Hybrid,
    }

    public interface ICoopInstanceInfo
    {
        Guid Id { get; internal set; }

        NetworkRole Role { get; internal set; }

        bool IsServer { get; }
        bool IsClient { get; }
    }

    public class CoopInstanceInfo : ICoopInstanceInfo
    {
        public Guid Id { get; set; }

        public NetworkRole Role { get; set; }

        public bool IsServer => Role == NetworkRole.Server || Role == NetworkRole.Hybrid;

        public bool IsClient => Role == NetworkRole.Client || Role == NetworkRole.Hybrid;

        public CoopInstanceInfo() 
        {
            Id = Guid.Empty;
            Role = NetworkRole.None;
        }
    }
}
