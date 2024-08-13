using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages.Properties;

public class ChangeStaticBodyProperties : ITargetCommand
{
    public ChangeStaticBodyProperties(string id, string target, ulong keyParty1, ulong keyParty2, ulong keyParty3, ulong keyParty4, ulong keyParty5, ulong keyParty6, ulong keyParty7, ulong keyParty8)
    {
        Id = id;
        Target = target;
        KeyParty1 = keyParty1;
        KeyParty2 = keyParty2;
        KeyParty3 = keyParty3;
        KeyParty4 = keyParty4;
        KeyParty5 = keyParty5;
        KeyParty6 = keyParty6;
        KeyParty7 = keyParty7;
        KeyParty8 = keyParty8;
    }
    
    public string Id { get; set; }
    public string Target { get; set; }

    public ulong KeyParty1 { get; set; }

    public ulong KeyParty2 { get; set; }

    public ulong KeyParty3 { get; set; }

    public ulong KeyParty4 { get; set; }

    public ulong KeyParty5 { get; set; }

    public ulong KeyParty6 { get; set; }

    public ulong KeyParty7 { get; set; }

    public ulong KeyParty8 { get; set; }
}