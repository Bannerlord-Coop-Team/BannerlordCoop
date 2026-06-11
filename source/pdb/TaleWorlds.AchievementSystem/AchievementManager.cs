// Decompiled with JetBrains decompiler
// Type: TaleWorlds.AchievementSystem.AchievementManager
// Assembly: TaleWorlds.AchievementSystem, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B3D1508-773F-453E-9213-B90C66EB89F6
// Assembly location: E:\Mod Development\BannerlordCoop\mb2\bin\Win64_Shipping_Client\TaleWorlds.AchievementSystem.dll

using System.Threading.Tasks;

#nullable disable
namespace TaleWorlds.AchievementSystem
{
  public class AchievementManager
  {
    static AchievementManager()
    {
      AchievementManager.AchievementService = (IAchievementService) new TestAchievementService();
    }

    public static IAchievementService AchievementService { get; set; }

    public static bool SetStat(string name, int value)
    {
      return AchievementManager.AchievementService.SetStat(name, value);
    }

    public static async Task<int> GetStat(string name)
    {
      return await AchievementManager.AchievementService.GetStat(name);
    }

    public static async Task<int[]> GetStats(string[] names)
    {
      return await AchievementManager.AchievementService.GetStats(names);
    }
  }
}
