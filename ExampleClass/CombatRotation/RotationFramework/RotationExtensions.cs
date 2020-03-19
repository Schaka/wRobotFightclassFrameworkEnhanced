using System.Collections.Generic;
using System.Linq;
using System.Text;
using wManager.Wow.Enums;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace CombatRotation.RotationFramework
{
	public static class Extensions
	{
		private static readonly Dictionary<int, string> _creatureTypeCache = new Dictionary<int, string>();

		public static bool HasDebuffType(this WoWUnit unit, string type)
		{
			return RotationCombatUtil.ExecuteActionOnUnit(unit, (luaUnitId) =>
			{
				string luaString = $@"
                local hasDebuff = false;
                for i=1,40 do
                    local name, rank, iconTexture, count, debuffType, duration, timeLeft = UnitDebuff(""{luaUnitId}"", i);
                    if debuffType == ""{type}"" then
                        hasDebuff = true
                        break;
                    end
                end
                return hasDebuff;";
				return Lua.LuaDoString<bool>(luaString);
			});
		}

		public static string AsString(this IEnumerable<string> list)
		{
			return list.Aggregate((s1, s2) => s1 + ", " + s2);
		}

		public static bool IsCasting(this WoWUnit unit)
		{
			return RotationCombatUtil.ExecuteActionOnUnit(unit, (luaUnitId) =>
			{
				string luaString = $@"return (UnitCastingInfo(""{luaUnitId}"") ~= nil or UnitChannelInfo(""{luaUnitId}"") ~= nil)";
				return Lua.LuaDoString<bool>(luaString);
			});
		}

		public static bool IsCreatureType(this WoWUnit unit, string creatureType)
		{
			if (_creatureTypeCache.ContainsKey(unit.Entry))
			{
				return _creatureTypeCache[unit.Entry] == creatureType;
			}

			var type = RotationCombatUtil.ExecuteActionOnUnit(unit, (luaUnitId) =>
			{
				string luaString = $@"return UnitCreatureType(""{luaUnitId}"")";
				return Lua.LuaDoString<string>(luaString);
			});
			_creatureTypeCache.Add(unit.Entry, type);
			return type == creatureType;
		}

		public static float CastingTimeLeft(this WoWUnit unit, string name)
		{
			return RotationCombatUtil.ExecuteActionOnUnit(unit, (luaUnitId) =>
			{
				string luaString = $@"
            local castingTimeLeft = 0;
    
            local name, rank, displayName, icon, startTime, endTime, isTradeSkill = UnitCastingInfo(""{luaUnitId}"")
            if name == ""{name}"" then
                castingTimeLeft = endTime - GetTime()
            end
            return castingTimeLeft;";
				return Lua.LuaDoString<float>(luaString);
			});
		}

		public static bool CastingTimeLessThan(this WoWUnit unit, string name, float lessThan)
		{
			var castingTimeLeft = CastingTimeLeft(unit, name);
			if (castingTimeLeft > 0 && castingTimeLeft < lessThan)
			{
				return true;
			}

			return false;
		}

		public static bool CastingSpell(this WoWUnit unit, params string[] names)
		{
			return RotationCombatUtil.ExecuteActionOnUnit(unit, (luaUnitId) =>
			{
				string luaString = $@"
	            local isCastingSpell = false;
	    
	            local name = UnitCastingInfo(""{luaUnitId}"")
	            if {LuaOrCondition(names, "name")} then
	                isCastingSpell = true
	            end
	            return isCastingSpell;";
				return Lua.LuaDoString<bool>(luaString);
			});
		}

		public static bool HasMana(this WoWUnit unit)
		{
			return RotationCombatUtil.ExecuteActionOnUnit(unit, (luaUnitId) =>
			{
				string luaString = $@"return (UnitPowerType(""{luaUnitId}"") == 0 and UnitMana(""{luaUnitId}"") > 1)";
				return Lua.LuaDoString<bool>(luaString);
			});
		}

		public static bool HaveAnyDebuff(this WoWUnit unit, params string[] names)
		{
			return RotationCombatUtil.ExecuteActionOnUnit(unit, (luaUnitId) =>
			{
				string luaString = $@"
		        local hasDebuff = false;
		        for i=1,40 do
			        local name, rank, iconTexture, count, debuffType, duration, timeLeft = UnitDebuff(""{luaUnitId}"", i);
		            if {LuaOrCondition(names, "name")} then
		                hasDebuff = true
		                break;
		            end
		        end
		        return hasDebuff;";
				return Lua.LuaDoString<bool>(luaString);
			});
		}

		//this does a multi-language check because it gets the spellname in English (not nameInGame)
		public static bool HasBuff(this WoWUnit unit, string name)
		{
			return unit.GetAllBuff().Any(b => name == b.GetSpell.Name);
		}

		public static bool HasAnyBuff(this WoWUnit unit, params string[] names)
		{
			return unit.GetAllBuff().Any(b => names.Contains(b.GetSpell.Name));
		}

		public static bool HaveAllDebuffs(this WoWUnit unit, params string[] names)
		{
			return unit.GetAllBuff().Select(b => b.GetSpell.Name).All(names.Contains);
		}

		public static bool HaveAllBuffsKnown(this WoWUnit unit, params string[] names)
		{
			foreach (string name in names)
			{
				if (RotationSpellbook.IsKnown(name) && !unit.HasBuff(name))
				{
					return false;
				}
			}

			return true;
		}

		public static bool IsPlayer(this WoWUnit unit)
		{
			return unit.Type == WoWObjectType.Player;
		}

		private static string LuaAndCondition(string[] names, string varname)
		{
			StringBuilder sb = new StringBuilder();
			foreach (var name in names)
			{
				sb.Append(" and " + varname + " == \"" + name + "\"");
			}

			return sb.ToString().Substring(5);
		}

		private static string LuaOrCondition(string[] names, string varname)
		{
			StringBuilder sb = new StringBuilder();
			foreach (var name in names)
			{
				sb.Append(" or " + varname + " == \"" + name + "\"");
			}

			return sb.ToString().Substring(4);
		}

		private static string LuaTable(string[] names)
		{
			string returnValue = "{";
			foreach (var name in names)
			{
				returnValue += "[\"" + name + "\"] = false,";
			}

			return returnValue += "};";
		}
	}
}