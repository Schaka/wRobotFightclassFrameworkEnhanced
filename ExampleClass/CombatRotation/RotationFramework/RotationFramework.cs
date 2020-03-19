using System.Collections.Generic;
using System.Linq;
using System.Threading;
using robotManager.Helpful;
using wManager;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace CombatRotation.RotationFramework
{
	public class RotationFramework
	{
		private static RotationSpellbook _rotationSpellbook;
		private static bool _slowRotation = false;
		private static bool _framelock = true;

		private static WoWLocalPlayer player = ObjectManager.Me;
		private static WoWUnit pet = ObjectManager.Pet;
		private static WoWUnit target = ObjectManager.Target;
		private static List<WoWUnit> units = ObjectManager.GetObjectWoWUnit();

		internal static bool IsCast { get; set; }

		public static void Initialize(bool slowRotation = false, bool framelock = true)
		{
			if (wManager.Wow.Memory.WowMemory.FrameIsLocked)
			{
				wManager.Wow.Memory.WowMemory.UnlockFrame();
			}

			_rotationSpellbook = new RotationSpellbook();
			_slowRotation = slowRotation;
			_framelock = framelock;
			RotationEventHandler.Start();
		}

		public static void Dispose()
		{
			if (wManager.Wow.Memory.WowMemory.FrameIsLocked)
			{
				wManager.Wow.Memory.WowMemory.UnlockFrame();
			}

			RotationEventHandler.Stop();
		}

		public static void RunRotation(List<RotationStep> rotation)
		{
			float globalCd = GetGlobalCooldown();
			bool gcdEnabled = globalCd != 0;
			IsCast = Me.IsCast || Me.IsCasting();

			if (_slowRotation)
			{
				if (IsCast)
				{
					var temp = Me.CastingTimeLeft;
					RotationLogger.Fight($"Slow rotation - still casting! Wait for {temp + 100}");
					Thread.Sleep(temp + 100);
				}
				//if no spell was executed successfully, we are assuming to still be on GCD and sleep the thread until the GCD ends
				//this prevents the rotation from re-checking if a no-gcd spell like Vanish, Judgement etc is ready
				else if (gcdEnabled)
				{
					RotationLogger.Fight($"No spell casted, waiting for {(globalCd * 1000 + 100)} for global cooldown to end!");
					Thread.Sleep((int) (globalCd * 1000 + 100));
				}
			}


			var watch = System.Diagnostics.Stopwatch.StartNew();

			if (_framelock)
			{
				RunInFrameLock(rotation, gcdEnabled);
			}
			else
			{
				RunInLock(rotation, gcdEnabled);
			}

			watch.Stop();
			if (watch.ElapsedMilliseconds > 150)
			{
				RotationLogger.Fight("Iteration took " + watch.ElapsedMilliseconds + "ms");
			}
		}

		private static void RunInLock(List<RotationStep> rotation, bool gcdEnabled)
		{
			lock (ObjectManager.Locker)
			{
				UpdateUnits();

				foreach (var step in rotation)
				{
					if (step.ExecuteStep(gcdEnabled))
					{
						break;
					}
				}
			}
		}

		private static void RunInFrameLock(List<RotationStep> rotation, bool gcdEnabled)
		{
			if (_framelock)
			{
				wManagerSetting.CurrentSetting.UseLuaToMove = true;
				wManager.Wow.Memory.WowMemory.LockFrame();
			}

			UpdateUnits();

			foreach (var step in rotation)
			{
				if (step.ExecuteStep(gcdEnabled))
				{
					break;
				}
			}

			if (_framelock)
			{
				wManager.Wow.Memory.WowMemory.UnlockFrame();
			}
		}

		private static void UpdateUnits()
		{
			player = ObjectManager.Me;
			target = ObjectManager.Target;
			pet = ObjectManager.Pet;
			List<WoWUnit> relevantUnits = new List<WoWUnit>();
			relevantUnits.AddRange(ObjectManager.GetObjectWoWPlayer().Where(u => u.GetDistance <= 50));
			relevantUnits.AddRange(ObjectManager.GetObjectWoWUnit().Where(u => u.GetDistance <= 50));
			units.Clear();
			units.AddRange(relevantUnits);
		}

		public static WoWLocalPlayer Me => player;
		public static WoWUnit Target => target;
		public static WoWUnit Pet => pet;
		public static List<WoWUnit> Units => units;

		public static float GetGlobalCooldown()
		{
			if (Usefuls.WowVersion > 8606)
			{
				return SpellManager.GlobalCooldownTimeLeft() / 1000f;
			}

			string luaString = @"
	        local lastCd = 0;
	        local globalCd = 0;

	        for i = 1, 20 do
	            
	            local spellName, spellRank = GetSpellName(i, BOOKTYPE_SPELL);

	            if not spellName then
	                break;
	            end
	            
	            local start, duration, enabled = GetSpellCooldown(i, BOOKTYPE_SPELL);

	            if enabled == 1 and start > 0 and duration > 0 then
	                lastCd = (start + duration - GetTime()); -- cooldown in seconds
	            end            

	            if lastCd > 0 and lastCd <= 1.5 then
	                local currentCd = (start + duration - GetTime());
	                if lastCd - currentCd <= 0.001 then
	                    globalCd = currentCd;
	                    break;
	                end
	            end

	        end

	        return globalCd;";
			return Lua.LuaDoString<float>(luaString);
		}

		public static float GetItemCooldown(string itemName)
		{
			string luaString = $@"
	        for bag=0,4 do
	            for slot=1,36 do
	                local name = GetContainerItemLink(bag,slot);
	                if (name and name == ""{itemName}"") then
	                    local start, duration, enabled = GetContainerItemCooldown(bag, slot);
	                    if enabled then
	                        return (duration - (GetTime() - start)) * 1000;
	                    end
	                end;
	            end;
	        end
	        return 0;";
			return Lua.LuaDoString<float>(luaString);
		}
	}
}