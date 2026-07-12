using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct PropertyObjectSurrogate
{
    [ProtoMember(1)]
    public string StringId { get; set; }

    [ProtoMember(2)]
    public TextObject Name { get; set; }

    [ProtoMember(3)]
    public TextObject Description { get; set; }

    public PropertyObjectSurrogate(PropertyObject propertyObject)
    {
        StringId = propertyObject.StringId;
        Name = propertyObject.Name;
        Description = propertyObject.Description;
    }

    public static implicit operator PropertyObjectSurrogate(PropertyObject propertyObject)
    {
        return new PropertyObjectSurrogate(propertyObject);
    }

    public static implicit operator PropertyObject(PropertyObjectSurrogate surrogate)
    {
        var propertyObject = new PropertyObject(surrogate.StringId);

        propertyObject.Initialize(surrogate.Name, surrogate.Description);

        return propertyObject;
    }
}

