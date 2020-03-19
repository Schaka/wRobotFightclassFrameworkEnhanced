using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace CombatRotation.RotationFramework
{
	class RotationLua : RotationAction
	{
		private readonly string _luaAction;
		private readonly float _actionRange;
		private readonly bool _ignoresGlobal;

		public RotationLua(string lua, float range = 30, bool ignoresGlobal = false)
		{
			_luaAction = lua;
			_actionRange = range;
			_ignoresGlobal = ignoresGlobal;
		}

		public bool Execute(WoWUnit target, bool force = false)
		{
			if (force && RotationFramework.Me.IsCasting())
			{
				Lua.LuaDoString("SpellStopCasting();");
			}

			Lua.LuaDoString(_luaAction);

			return true;
		}

		public float Range()
		{
			return _actionRange;
		}

		public bool IgnoresGlobal()
		{
			return _ignoresGlobal;
		}
	}
}