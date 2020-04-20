using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace Coop.Game
{
    public static class Extensions
    {
		public static T GetGameModel<T>(this TaleWorlds.Core.Game game) where T : GameModel
		{
			foreach(var model in game.BasicModels.GetGameModels())
			{
				T t = model as T;
				if (t != null)
				{
					return t;
				}
			}
			return null;
		}
	}
}
