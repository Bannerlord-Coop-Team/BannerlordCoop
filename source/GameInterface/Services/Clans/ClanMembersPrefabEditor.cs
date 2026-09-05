using System.Xml;

namespace GameInterface.Services.Clans;

public interface IClanMembersPrefabEditor
{
    void AddMemberGroups(XmlNode root);
}

public class ClanMembersPrefabEditor : IClanMembersPrefabEditor
{
    public void AddMemberGroups(XmlNode root)
    {
        if (root.Attributes?["Id"]?.Value != "ClanMembersWidget" ||
            root.SelectSingleNode(".//*[@Id='PlayersList']") != null) return;

        AddGroup(root, "Players", "Family");
        AddGroup(root, "OtherFamilies", "Companions");

        // The local player now has a role label too; keep vanilla IsMainHero action restrictions.
        var relation = root.SelectSingleNode(".//*[@Text='@RelationToMainHeroText']");
        relation?.Attributes?.RemoveNamedItem("IsHidden");
    }

    private void AddGroup(XmlNode root, string group, string beforeGroup)
    {
        var header = root.SelectSingleNode(".//*[@Id='FamilyHeader']");
        var list = root.SelectSingleNode(".//*[@Id='FamilyList']");
        var navigation = root.SelectSingleNode(".//NavigationAutoScrollWidget[@TrackedWidget='..\\FamilyHeader']");
        var before = root.SelectSingleNode($".//NavigationAutoScrollWidget[@TrackedWidget='..\\{beforeGroup}Header']");
        before.ParentNode.InsertBefore(CloneGroup(navigation, group, "IsVisible"), before);
        before.ParentNode.InsertBefore(CloneGroup(header, group, "IsRelevant"), before);
        before.ParentNode.InsertBefore(CloneGroup(list, group), before);

        var toggle = root.SelectSingleNode(".//*[@Id='FamilyToggleButton']");
        var beforeToggle = root.SelectSingleNode($".//*[@Id='{beforeGroup}ToggleButton']");
        beforeToggle.ParentNode.InsertBefore(CloneGroup(toggle, group, "IsRelevant"), beforeToggle);
    }

    private XmlNode CloneGroup(XmlNode source, string group, string visibilityProperty = null)
    {
        var clone = (XmlElement)source.CloneNode(true);
        foreach (XmlAttribute attribute in clone.SelectNodes(".//@*"))
        {
            attribute.Value = attribute.Value.Replace("Family", group);
        }

        if (visibilityProperty != null)
            clone.SetAttribute(visibilityProperty, "@Has" + group);

        return clone;
    }
}
