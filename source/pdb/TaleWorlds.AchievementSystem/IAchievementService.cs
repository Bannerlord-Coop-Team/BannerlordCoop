// Decompiled with JetBrains decompiler
// Type: TaleWorlds.AchievementSystem.IAchievementService
// Assembly: TaleWorlds.AchievementSystem, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 1B3D1508-773F-453E-9213-B90C66EB89F6
// Assembly location: E:\Mod Development\BannerlordCoop\mb2\bin\Win64_Shipping_Client\TaleWorlds.AchievementSystem.dll

using System.Threading.Tasks;

#nullable disable
namespace TaleWorlds.AchievementSystem
{
  public interface IAchievementService
  {
    bool SetStat(string name, int value);

    Task<int> GetStat(string name);

    Task<int[]> GetStats(string[] names);

    bool IsInitializationCompleted();
  }
}
