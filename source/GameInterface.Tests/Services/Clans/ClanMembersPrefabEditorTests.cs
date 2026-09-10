using GameInterface.Services.Clans;
using System;
using System.IO;
using System.Linq;
using System.Xml;
using TaleWorlds.GauntletUI.PrefabSystem;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

public class ClanMembersPrefabEditorTests
{
    [Fact]
    public void InstalledPrefab_AllFourGroupsHaveDistinctBindingsAndWorkingToggleTargets()
    {
        var document = LoadPrefab();
        var root = document.SelectSingleNode("/Prefab/Window/*")!;
        var editor = new ClanPrefabEditor();
        editor.AddMemberGroups(root);

        string[] groups = { "Players", "Family", "OtherFamilies", "Companions" };
        var lists = root.SelectNodes(".//*[@Id='ClanElementsListPanel']/Children/NavigatableListPanel")!
            .Cast<XmlNode>().ToArray();
        Assert.Equal(groups.Select(group => "{" + group + "}"), lists.Select(list => list.Attributes!["DataSource"]!.Value));
        var toggles = root.SelectNodes(".//PartyHeaderToggleWidget")!.Cast<XmlNode>();
        Assert.Equal(groups.Select(group => group + "ToggleButton"), toggles.Select(toggle => toggle.Attributes!["Id"]!.Value));

        foreach (var group in groups)
        {
            var header = root.SelectSingleNode($".//*[@Id='{group}Header']")!;
            var toggle = root.SelectSingleNode($".//*[@Id='{group}ToggleButton']")!;
            Assert.Equal("..\\..\\..\\" + group + "ToggleButton", header.Attributes!["FixedHeader"]!.Value);
            Assert.EndsWith("\\" + group + "List", toggle.Attributes!["WidgetToClose"]!.Value);
            Assert.Equal(toggle.Attributes["WidgetToClose"]!.Value, toggle.Attributes["ListPanel"]!.Value);
            Assert.NotNull(toggle.SelectSingleNode($".//*[@Text='@{group}Text']"));
            Assert.NotNull(root.SelectSingleNode($".//NavigationAutoScrollWidget[@TrackedWidget='..\\{group}Header']"));
            Assert.NotNull(typeof(CoopClanMembersVM).GetProperty(group));
            Assert.NotNull(typeof(CoopClanMembersVM).GetProperty(group + "Text"));

            if (group == "Players" || group == "OtherFamilies")
            {
                Assert.Equal("@Has" + group, header.Attributes["IsRelevant"]!.Value);
                Assert.Equal("@Has" + group, toggle.Attributes["IsRelevant"]!.Value);
                Assert.Equal("@Has" + group, root.SelectSingleNode(
                    $".//NavigationAutoScrollWidget[@TrackedWidget='..\\{group}Header']/@IsVisible")!.Value);
                Assert.NotNull(typeof(CoopClanMembersVM).GetProperty("Has" + group));
            }
        }

        var ids = root.SelectNodes(".//*[@Id='ClanElementsScrollablePanel']//@Id")!
            .Cast<XmlAttribute>().Select(attribute => attribute.Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Null(root.SelectSingleNode(".//*[@Text='@RelationToMainHeroText']/@IsHidden"));
        Assert.NotNull(root.SelectSingleNode(".//*[@Text='@GovernorOfText']/@IsHidden"));

        var edited = root.OuterXml;
        editor.AddMemberGroups(root);
        Assert.Equal(edited, root.OuterXml);
    }

    [Fact]
    public void OtherPrefabs_AreUnchanged()
    {
        var document = LoadPrefab();
        var root = document.DocumentElement!;
        var original = root.OuterXml;

        new ClanPrefabEditor().AddMemberGroups(root);

        Assert.Equal(original, root.OuterXml);
    }

    [Theory]
    [InlineData("ClanScreen.xml", "LeaveClanButton", "{ClanMembers}", "@CanLeaveClan", "ExecuteLeaveClan")]
    [InlineData("ClanMembers.xml", "ManagePlayerButton", "{..}", "@CanManagePlayer", "ExecuteManagePlayer")]
    public void MembershipButtons_BindToMembersVmAndAreOnlyAddedOnce(string file, string id,
        string dataSource, string visibility, string command)
    {
        var document = new XmlDocument();
        document.Load(Path.Combine(AppContext.BaseDirectory, file));
        var root = document.SelectSingleNode("/Prefab/Window/*")!;
        var editor = new ClanPrefabEditor();
        editor.AddMembershipActions(root);
        editor.AddMembershipActions(root);

        var button = Assert.Single(root.SelectNodes($".//*[@Id='{id}']")!.Cast<XmlElement>());
        Assert.Equal(dataSource, button.GetAttribute("DataSource"));
        Assert.Equal(visibility, button.GetAttribute("IsVisible"));
        Assert.Equal(command, button.GetAttribute("Command.Click"));
        Assert.NotNull(typeof(CoopClanMembersVM).GetMethod(command));

        if (file == "ClanMembers.xml")
            Assert.NotNull(button.SelectSingleNode("ancestor::*[@DataSource='{CurrentSelectedMember}']"));
        else
            Assert.Null(button.SelectSingleNode("ancestor::*[@DataSource]"));
    }

    private static XmlDocument LoadPrefab()
    {
        var document = new XmlDocument();
        document.Load(Path.Combine(AppContext.BaseDirectory, "ClanMembers.xml"));
        return document;
    }

    [Theory]
    [InlineData("ClanScreen.xml")]
    [InlineData("ClanMembers.xml")]
    public void MembershipButtons_LoadWithGauntletPrefabParser(string file)
    {
        var document = new XmlDocument();
        using var reader = XmlReader.Create(Path.Combine(AppContext.BaseDirectory, file),
            new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true });
        document.Load(reader);
        var root = document.SelectSingleNode("/Prefab/Window/*")!;
        new ClanPrefabEditor().AddMembershipActions(root);

        Assert.NotNull(WidgetTemplate.LoadFrom(new PrefabExtensionContext(), new WidgetAttributeContext(), root));
    }
}
