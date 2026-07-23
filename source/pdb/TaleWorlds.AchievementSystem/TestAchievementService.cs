// Decompiled with JetBrains decompiler
// Type: TaleWorlds.AchievementSystem.TestAchievementService
// Assembly: TaleWorlds.AchievementSystem, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B3D1508-773F-453E-9213-B90C66EB89F6
// Assembly location: E:\Mod Development\BannerlordCoop\mb2\bin\Win64_Shipping_Client\TaleWorlds.AchievementSystem.dll

using System.Threading.Tasks;

#nullable disable
namespace TaleWorlds.AchievementSystem
{
  public class TestAchievementService : IAchievementService
  {
    bool IAchievementService.SetStat(string name, int value) => true;

    Task<int> IAchievementService.GetStat(string name) => Task.FromResult<int>(0);

    Task<int[]> IAchievementService.GetStats(string[] names)
    {
      return Task.FromResult<int[]>(new int[names.Length]);
    }

    bool IAchievementService.IsInitializationCompleted() => true;
  }
}
