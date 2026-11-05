using System.Xml;

namespace GameInterface.Services.Barters;

public interface IBarterPrefabEditor : IGameAbstraction
{
    void AddSpousePortrait(XmlNode root);
}

public class BarterPrefabEditor : IBarterPrefabEditor
{
    public void AddSpousePortrait(XmlNode root)
    {
        var portrait = root.SelectSingleNode("./Children/ImageIdentifierWidget[@Id='ImageIdentifierWidget']");
        if (portrait == null) return;

        var portraits = root.OwnerDocument.CreateElement("Widget");
        portraits.SetAttribute("Id", "Portraits");
        portraits.SetAttribute("WidthSizePolicy", "StretchToParent");
        portraits.SetAttribute("HeightSizePolicy", "StretchToParent");
        portraits.SetAttribute("ClipContents", "true");
        portraits.SetAttribute("DoNotAcceptEvents", "true");
        portraits.InnerXml = "<Children>" +
            "<Widget Id='HeroPortrait' WidthSizePolicy='StretchToParent' HeightSizePolicy='StretchToParent' " +
                "DoNotAcceptEvents='true' PositionXOffset='@PortraitXOffset'>" +
                "<Children>" + portrait.OuterXml + "</Children>" +
            "</Widget>" +
            "<Widget Id='SpousePortrait' WidthSizePolicy='StretchToParent' HeightSizePolicy='StretchToParent' " +
                "DoNotAcceptEvents='true' IsVisible='@HasSpouseVisualIdentifier' PositionXOffset='@SpousePortraitXOffset'>" +
                "<Children>" +
                    "<ImageIdentifierWidget DataSource='{SpouseVisualIdentifier}' DoNotAcceptEvents='true' " +
                        "WidthSizePolicy='StretchToParent' HeightSizePolicy='StretchToParent' VerticalAlignment='Center' " +
                        "AdditionalArgs='@AdditionalArgs' ImageId='@Id' TextureProviderName='@TextureProviderName' IsBig='true' />" +
                "</Children>" +
            "</Widget>" +
            "</Children>";

        root.SelectSingleNode("./Children").ReplaceChild(portraits, portrait);
        ((XmlElement)root).SetAttribute("ImageIdentifierWidget", "Portraits\\HeroPortrait\\ImageIdentifierWidget");
    }
}
