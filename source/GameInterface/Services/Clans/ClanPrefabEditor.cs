using System.Xml;

namespace GameInterface.Services.Clans;

public interface IClanPrefabEditor : IGameAbstraction
{
    void AddMemberGroups(XmlNode root);
    void ApplyIncomePermissions(XmlNode root);
    void AddMembershipActions(XmlNode root);
}

public class ClanPrefabEditor : IClanPrefabEditor
{
    public void AddMembershipActions(XmlNode root)
    {
        var fragment = root.OwnerDocument.CreateDocumentFragment();
        XmlNode previous;
        if (root.Attributes?["Id"]?.Value == "ClanScreenWidget" &&
            root.SelectSingleNode(".//*[@Id='LeaveClanButton']") == null)
        {
            fragment.InnerXml = @"
                <NavigationScopeTargeter ScopeID='ClanLeaveScope' ScopeParent='..\LeaveClanButton' ScopeMovements='Horizontal' />
                <ButtonWidget Id='LeaveClanButton' DataSource='{ClanMembers}' IsVisible='@CanLeaveClan'
                    WidthSizePolicy='Fixed' HeightSizePolicy='Fixed' SuggestedWidth='250' SuggestedHeight='60'
                    HorizontalAlignment='Right' MarginRight='300' MarginTop='25' Brush='Popup.Delete.Button'
                    DoNotPassEventsToChildren='true' Command.Click='ExecuteLeaveClan' GamepadNavigationIndex='0'>
                    <Children>
                        <TextWidget WidthSizePolicy='StretchToParent' HeightSizePolicy='StretchToParent'
                            Brush='Popup.Button.Text' Text='@LeaveClanText' />
                    </Children>
                </ButtonWidget>";
            previous = root.SelectSingleNode(".//*[@Id='TopPanel']/Children").LastChild;
        }
        else if (root.Attributes?["Id"]?.Value == "ClanMembersWidget" &&
            root.SelectSingleNode(".//*[@Id='ManagePlayerButton']") == null)
        {
            fragment.InnerXml = @"
                <NavigationScopeTargeter ScopeID='ClanManagePlayerScope' ScopeParent='..\ManagePlayerButton' ScopeMovements='Horizontal' />
                <ButtonWidget Id='ManagePlayerButton' DataSource='{..}' IsVisible='@CanManagePlayer'
                    WidthSizePolicy='Fixed' HeightSizePolicy='Fixed' SuggestedWidth='250' SuggestedHeight='50'
                    HorizontalAlignment='Center' Brush='Popup.Cancel.Button' Command.Click='ExecuteManagePlayer'
                    DoNotPassEventsToChildren='true' GamepadNavigationIndex='0'>
                    <Children>
                        <TextWidget WidthSizePolicy='StretchToParent' HeightSizePolicy='StretchToParent'
                            Brush='Popup.Button.Text' Text='@ManagePlayerText' />
                    </Children>
                </ButtonWidget>";
            previous = root.SelectSingleNode(".//*[@Id='LastSeenLocationParent']");
        }
        else return;

        // Gauntlet treats indentation retained by InnerXml as widgets with null attributes.
        foreach (XmlNode whitespace in fragment.SelectNodes(".//text()[normalize-space(.)='']"))
            whitespace.ParentNode.RemoveChild(whitespace);

        previous.ParentNode.InsertAfter(fragment, previous);
    }

    public void ApplyIncomePermissions(XmlNode root)
    {
        foreach (XmlElement button in root.SelectNodes(".//*[@Id='ManageWorkshopButton' or @Id='ManageAlleyButton']"))
            button.SetAttribute("IsEnabled", "@CanManageAsset");

        if (root.SelectSingleNode(".//*[@Id='ManageWorkshopButton']") == null) return;

        // Warehouse contents are private to the owning client.
        foreach (var text in new[] { "UseWarehouseAsInputText", "StoreOutputPercentageText", "WarehouseCapacityText" })
        {
            var row = root.SelectSingleNode($".//*[@Text='@{text}']/../..") as XmlElement;
            row?.SetAttribute("IsVisible", "@CanManageAsset");
        }
        foreach (XmlElement widget in root.SelectNodes(
            ".//*[@IntText='@WarehouseInputAmount' or @IntText='@WarehouseOutputAmount' or @Sprite='SPGeneral\\GameMenu\\warehouse_icon']"))
            widget.SetAttribute("IsVisible", "@CanManageAsset");
    }

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
