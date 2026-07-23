// Decompiled with JetBrains decompiler
// Type: TaleWorlds.AchievementSystem.Achievement
// Assembly: TaleWorlds.AchievementSystem, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B3D1508-773F-453E-9213-B90C66EB89F6
// Assembly location: E:\Mod Development\BannerlordCoop\mb2\bin\Win64_Shipping_Client\TaleWorlds.AchievementSystem.dll

#nullable disable
namespace TaleWorlds.AchievementSystem
{
  public class Achievement
  {
    public string Id { get; set; }

    public string LockedDisplayName { get; set; }

    public string UnlockedDisplayName { get; set; }

    public string LockedDescription { get; set; }

    public string UnlockedDescription { get; set; }

    public int TargetProgress { get; set; }

    public bool IsUnlocked { get; set; }

    public int CurrentProgress { get; set; }
  }
}
