using System;
using System.Collections.Generic;
using System.Linq;
using wManager.Wow;
using wManager.Wow.Enums;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace CombatRotation.RotationFramework
{
	public class RotationCombatUtil
	{
		private static object _locker = new object();
		private static object _focusLocker = new object();

		private static List<string> AreaSpells = new List<string>
		{
			"Mass Dispel",
			"Blizzard",
			"Rain of Fire",
			"Freeze",
			"Volley",
			"Flare",
			"Hurricane",
			"Flamestrike",
			"Distract"
		};

		public static WoWUnit FindFriend(Func<WoWUnit, bool> predicate)
		{
			if (RotationFramework.Me.HealthPercent < 60)
			{
				return RotationFramework.Me;
			}

			return RotationFramework.Units
				.Where(u =>
					       u.IsAlive &&
					       u.Reaction == Reaction.Friendly &&
					       predicate(u) &&
					       !TraceLine.TraceLineGo(u.Position))
				.OrderBy(u => u.HealthPercent)
				.FirstOrDefault();
		}

		public static WoWUnit FindEnemy(Func<WoWUnit, bool> predicate)
		{
			return FindEnemy(RotationFramework.Units, predicate);
		}

		public static WoWUnit FindEnemyPlayer(Func<WoWUnit, bool> predicate)
		{
			return FindEnemy(RotationFramework.Units, u => predicate(u) && u.IsPlayer());
		}

		public static WoWUnit FindEnemyCasting(Func<WoWUnit, bool> predicate)
		{
			return FindEnemyCasting(RotationFramework.Units, predicate);
		}

		public static WoWUnit FindPlayerCasting(Func<WoWUnit, bool> predicate)
		{
			return FindEnemyCasting(RotationFramework.Units, u => predicate(u) && u.IsPlayer());
		}

		public static WoWUnit FindEnemyCastingOnMe(Func<WoWUnit, bool> predicate)
		{
			return FindEnemyCastingOnMe(RotationFramework.Units, predicate);
		}

		public static WoWUnit FindPlayerCastingOnMe(Func<WoWUnit, bool> predicate)
		{
			return FindEnemyCastingOnMe(RotationFramework.Units, u => predicate(u) && u.IsPlayer());
		}

		private static WoWUnit FindEnemyCasting(IEnumerable<WoWUnit> units, Func<WoWUnit, bool> predicate)
		{
			return FindEnemy(units, (u) => predicate(u) && u.WowClass != WoWClass.Hunter && u.WowClass != WoWClass.Warrior && u.WowClass != WoWClass.Rogue && u.IsCasting());
		}

		private static WoWUnit FindEnemyCastingOnMe(IEnumerable<WoWUnit> units, Func<WoWUnit, bool> predicate)
		{
			return FindEnemyCasting(units, (u) => predicate(u) && u.Target == RotationFramework.Me.Guid);
		}

		private static WoWUnit FindEnemy(IEnumerable<WoWUnit> units, Func<WoWUnit, bool> rangePredicate)
		{
			return units.Where(u =>
				                   rangePredicate(u) &&
				                   u.IsAlive &&
				                   (int) u.Reaction < 3 &&
				                   !TraceLine.TraceLineGo(RotationFramework.Me.Position, u.Position, CGWorldFrameHitFlags.HitTestWMO))
				.OrderBy(u => u.GetDistance).FirstOrDefault();
		}

		public static WoWUnit BotTarget(Func<WoWUnit, bool> predicate)
		{
			var target = RotationFramework.Target;
			return !TraceLine.TraceLineGo(target.Position) && predicate(target) ? target : null;
		}

		public static WoWUnit FindPet(Func<WoWUnit, bool> predicate)
		{
			var target = RotationFramework.Pet;
			return !TraceLine.TraceLineGo(target.Position) && predicate(target) ? target : null;
		}

		public static WoWUnit FindMe(Func<WoWUnit, bool> predicate)
		{
			return RotationFramework.Me;
		}


		public static void CastBuff(RotationSpell buff, WoWUnit target)
		{
			if (buff.Spell.Name == "Power Word: Fortitude" && target.HasBuff("Prayer of Fortitude"))
			{
				return;
			}

			if (buff.Spell.Name == "Mark of the Wild" && target.HasBuff("Gift of the Wild"))
			{
				return;
			}

			if (buff.Spell.Name == "Divine Spirit" && target.HasBuff("Prayer of Spirit"))
			{
				return;
			}

			if (buff.Spell.Name == "Blessing of Kings" && target.HasBuff("Greater Blessing of Kings"))
			{
				return;
			}

			if (buff.IsKnown() && buff.CanCast() && !target.HasBuff(buff.Spell.Name))
			{
				CastSpell(buff, target);
			}
		}

		public static void CastBuff(RotationSpell buff)
		{
			CastBuff(buff, RotationFramework.Me);
		}

		public static bool IsAutoRepeating(string name)
		{
			return Lua.LuaDoString<bool>($"return IsAutoRepeatSpell(\"{name}\")");
		}

		public static bool IsAutoAttacking()
		{
			return Lua.LuaDoString<bool>("return IsCurrentSpell('Attack') == 1 or IsCurrentSpell('Attack') == true");
		}

		public static bool CastSpell(RotationSpell spell, WoWUnit unit, bool force = false)
		{
			// still waiting to make sure last spell was casted successfully, this can be interrupted
			// by interrupting the current cast to cast something else (which will clear the verification)
			if (RotationSpellVerifier.IsWaitingForVerification() && !force)
			{
				return false;
			}

			// no need to check for spell availability
			// already wanding, don't turn it on again!
			if (spell.Spell.Name == "Shoot" && IsAutoRepeating("Shoot"))
			{
				return true;
			}

			// targetfinder function already checks that they are in LoS and RotationStep takes care of the range check
			if (unit != null && spell.IsKnown() && spell.CanCast())
			{
				Lua.LuaDoString("if IsMounted() then Dismount() end");

				if (spell.Spell.CastTime > 0)
				{
					if (spell.Verification != RotationSpell.VerificationType.NONE)
					{
						//setting this for delegates, so we don't miss events
						//SetFocusGuid(unit.Guid);
						RotationSpellVerifier.QueueVerification(spell.Spell.Name, unit, spell.Verification);
					}

					//force iscast so we don't have to wait for client updates
					RotationFramework.IsCast = true;
					//ObjectManager.Me.ForceIsCast = true;
				}

				if (AreaSpells.Contains(spell.Spell.Name))
				{
					SpellManager.CastSpellByIDAndPosition(spell.Spell.Id, unit.Position);
				}
				else
				{
					if (unit.Guid != RotationFramework.Me.Guid && unit.Guid != RotationFramework.Target.Guid)
					{
						MovementManager.Face(unit);
					}

					ExecuteActionOnUnit<object>(unit, (luaUnitId =>
					{
						RotationLogger.Fight($"Casting {spell.FullName()} ({spell.Spell.Name} on {luaUnitId} with guid {unit.Guid}");
						//MovementManager.StopMoveTo(false, (int) spell.CastTime());
						Lua.LuaDoString($@"
						if {force.ToString().ToLower()} then SpellStopCasting() end
                        CastSpellByName(""{spell.FullName()}"", ""{luaUnitId}"");
						--CombatTextSetActiveUnit(""{luaUnitId}"");
						FocusUnit(""{luaUnitId}"");
						");
						return null;
					}));
				}

				return true;
			}

			return false;
		}

		public static T ExecuteActionOnUnit<T>(WoWUnit unit, Func<string, T> action)
		{
			return ExecuteActionOnTarget(unit.Guid, action);
		}

		public static T ExecuteActionOnTarget<T>(ulong target, Func<string, T> action)
		{
			if (target == RotationFramework.Me.Guid)
			{
				return action("player");
			}

			if (target == RotationFramework.Target.Guid)
			{
				return action("target");
			}


			lock (_locker)
			{
				SetMouseoverGuid(target);
				return action("mouseover");
			}
		}

		private static void SetMouseoverUnit(WoWUnit unit)
		{
			SetMouseoverGuid(unit.Guid);
		}

		public static T ExecuteActionOnFocus<T>(ulong target, Func<string, T> action)
		{
			lock (_focusLocker)
			{
				SetFocusGuid(target);
				return action("focus");
			}
		}

		public static void SetFocusGuid(ulong guid)
		{
			RotationFramework.Me.FocusGuid = guid;
			return;
			
			switch (Usefuls.WowVersion)
			{
				case 5875:
					throw new Exception("Vanilla does not support focus");
					break;
				case 8606:
					Memory.WowMemory.Memory.WriteUInt64((uint) Memory.WowMemory.Memory.MainModuleAddress + 0x86E980, guid);
					break;
				case 12340:
					Memory.WowMemory.Memory.WriteUInt64((uint) 0x00BD07D0, guid);
					break;
				default:
					throw new Exception("Wow version is not supported!");
			}
		}

		private static void SetMouseoverGuid(ulong guid)
		{
			RotationFramework.Me.MouseOverGuid = guid;
			return;

			switch (Usefuls.WowVersion)
			{
				case 5875:
					Memory.WowMemory.Memory.WriteUInt64((uint) Memory.WowMemory.Memory.MainModuleAddress + 0x74E2C8, guid);
					break;
				case 8606:
					Memory.WowMemory.Memory.WriteUInt64((uint) Memory.WowMemory.Memory.MainModuleAddress + 0x86E950, guid);
					break;
				case 12340:
					Memory.WowMemory.Memory.WriteUInt64((uint) 0x00BD07A0, guid);
					break;
				default:
					throw new Exception("Wow version is not supported!");
			}
		}
	}
}