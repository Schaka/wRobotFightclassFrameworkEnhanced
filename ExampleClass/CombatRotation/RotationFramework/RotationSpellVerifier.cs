using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using robotManager.Helpful;
using wManager.Wow.Enums;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace CombatRotation.RotationFramework
{
	public class RotationSpellVerifier
	{
		private static readonly object _verificationLock = new object();

		private static Tuple<string, ulong, RotationSpell.VerificationType, DateTime> _emptyVerify =
			new Tuple<string, ulong, RotationSpell.VerificationType, DateTime>("Empty", 0, RotationSpell.VerificationType.NONE, DateTime.MinValue);

		private static Tuple<string, ulong, RotationSpell.VerificationType, DateTime> _verification = _emptyVerify;

		private static string _delegateVerification = string.Empty;

		private static ulong _playerGuid = ObjectManager.Me.Guid;

		private static Dictionary<RotationSpell.VerificationType, HashSet<string>> _successEvents = new Dictionary<RotationSpell.VerificationType, HashSet<string>>
		{
			{
				RotationSpell.VerificationType.CAST_RESULT, new HashSet<string>
				{
					"SPELL_DAMAGE",
					"RANGED_DAMAGE",
					"SPELL_MISSED",
					"SPELL_HEAL",
					"SPELL_DRAIN",
					"SPELL_LEECH",
					"SPELL_SUMMON",
					"SPELL_CREATE",
					"SPELL_INSTAKILL",
				}
			},
			{
				RotationSpell.VerificationType.CAST_SUCCESS, new HashSet<string>
				{
					"SPELL_CAST_SUCCESS",
					"SPELL_MISSED",
				}
			},
			{
				RotationSpell.VerificationType.AURA, new HashSet<string>
				{
					"SPELL_AURA_APPLIED",
					"SPELL_AURA_APPLIED_DOSE",
					"SPELL_AURA_REFRESH",
					"SPELL_MISSED",
				}
			},
			{RotationSpell.VerificationType.NONE, new HashSet<string>()},
		};

		private static HashSet<Tuple<string, string>> _eventDelegates = new HashSet<Tuple<string, string>>
		{
			new Tuple<string, string>("SPELL_HEAL", "UNIT_HEALTH"),
			new Tuple<string, string>("SPELL_AURA_APPLIED", "UNIT_AURA"),
		};

		public static void NotifyCombatLog(List<string> args)
		{
			lock (_verificationLock)
			{
				string timestamp = args[0];
				string eventName = args[1];
				string sourceGuid = args[2];
				string sourceName = args[3];
				string sourceFlags = args[4];
				string destGuid = args[5];
				string destName = args[6];
				string destFlags = args[7];

				// we have to check that the event fired is an expected event for the type of spell being casted
				// so that spells expecting an aura will only be verified on aura appliance 
				RotationSpell.VerificationType type = GetVerificationType();
				if (_successEvents[type].Contains(eventName))
				{
					string spellId = args[8];
					string spellName = args[9];
					string spellSchool = args[10];

					RotationLogger.Trace($"{eventName} {sourceGuid} {sourceName} {destGuid} {destName} {spellId} {spellName} {spellSchool}");

					ulong castedBy = GetGUIDForLuaGUID(sourceGuid);
					if (castedBy == _playerGuid && IsSpellWaitingForVerification(spellName))
					{
						var delegated = _eventDelegates.FirstOrDefault(e => e.Item1 == eventName);
						if (delegated != null)
						{
							string delegatedEvent = delegated.Item2;
							RotationLogger.Debug($"Delegating {eventName} to {delegatedEvent}");
							CreatePassiveEventDelegate(delegatedEvent);
						}
						else
						{
							RotationLogger.Debug($"Clearing verification for {spellName}");
							_verification = _emptyVerify;
						}
					}

					ulong spellTarget = GetGUIDForLuaGUID(destGuid);
					if (castedBy == 0 && IsWaitingForSpellOnTarget(spellName, spellTarget))
					{
						var delegated = _eventDelegates.FirstOrDefault(e => e.Item1 == eventName);
						if (delegated != null)
						{
							string delegatedEvent = delegated.Item2;
							RotationLogger.Debug($"Delegating {eventName} to {delegatedEvent}");
							CreatePassiveEventDelegate(delegatedEvent);
						}
						else
						{
							RotationLogger.Debug($"Clearing verification for spell with no source {spellName}");
							_verification = _emptyVerify;
						}
					}
				}

				if (eventName == "SPELL_CAST_FAILED")
				{
					string spellId = args[8];
					string spellName = args[9];
					string spellSchool = args[10];
					string failedType = args[11];

					ulong castedBy = GetGUIDForLuaGUID(sourceGuid);
					if (castedBy == _playerGuid && IsSpellWaitingForVerification(spellName) && failedType != "Another action is in progress")
					{
						RotationLogger.Debug($"Clearing verification for {spellName} because {failedType}");
						_verification = _emptyVerify;
					}
				}

				if (eventName == "UNIT_DIED")
				{
					ulong deadUnit = GetGUIDForLuaGUID(destGuid);
					if (IsWaitingOnTarget(deadUnit))
					{
						RotationLogger.Debug($"Clearing verification because target died");
						_verification = _emptyVerify;
					}

					if (deadUnit == _playerGuid)
					{
						RotationLogger.Debug($"Clearing verification because we died");
						_verification = _emptyVerify;
					}
				}

				ClearVerificationOlderThan(10);
			}
		}

		public static void QueueVerification(string spellName, WoWUnit target, RotationSpell.VerificationType type)
		{
			lock (_verificationLock)
			{
				RotationLogger.Debug($"Queueing verification for {spellName} on {Thread.CurrentThread.Name}");
				_verification = new Tuple<string, ulong, RotationSpell.VerificationType, DateTime>(spellName, target.Guid, type, DateTime.Now);
				RegisterCombatLogClearer();
			}
		}

		public static void ForceClearVerification()
		{
			lock (_verificationLock)
			{
				if (_verification.Item1 != _emptyVerify.Item1)
				{
					RotationLogger.Debug($"Force clearing verification with current spell waiting on {_verification.Item1}");
					_verification = _emptyVerify;
				}
			}
		}

		public static void ForceClearVerification(string spellName)
		{
			lock (_verificationLock)
			{
				RotationLogger.Debug($"Force clearing verification for {spellName}");
				_verification = _emptyVerify;
			}
		}

		public static bool IsWaitingForVerification()
		{
			lock (_verificationLock)
			{
				return _verification.Item1 != _emptyVerify.Item1;
			}
		}

		public static bool IsSpellWaitingForVerification(string spellName)
		{
			lock (_verificationLock)
			{
				return _verification.Item1 == spellName;
			}
		}

		public static void NotifyForDelegate(string id, List<string> args)
		{
			lock (_verificationLock)
			{
				if (!string.IsNullOrEmpty(_delegateVerification) && id == _delegateVerification && args[0] == "focus")
				{
					RotationLogger.Debug($"Clearing verification for {_verification.Item1} after delegated event {id}");
					_verification = _emptyVerify;
					_delegateVerification = string.Empty;
				}
			}
		}


		/*
		 * Example of NotifyForDelegate when usign COMBAT_TEXT_UPDATE instead of FocusUnit
		 *
		 * 
		 public static void NotifyForDelegate(string id, List<string> args)
		{
			lock (VerificationLock)
			{
				if (_delegateVerification?.Count > 0 && id == "COMBAT_TEXT_UPDATE" && _delegateVerification.Contains(args[0]))
				{
					string eventName = args[0];
					string auraOrHealerName = args[1];

					if (eventName.Contains("HEAL") && auraOrHealerName == RotationFramework.Me.Name)
					{
						Blindly.Run(() =>
						{
							Thread.Sleep(Usefuls.Latency / 2);
							RotationLogger.Debug($"Clearing verification for {_verification.Item1} after delegated event {eventName}");
							_verification = EmptyVerify;
							_delegateVerification = new List<string>();
						});
					}
					else if (eventName.Contains("AURA") && auraOrHealerName == _verification.Item1)
					{
						Blindly.Run(() =>
						{
							Thread.Sleep(Usefuls.LatencyReal);
							RotationLogger.Debug($"Clearing verification for {_verification.Item1} after delegated event {eventName}");
							_verification = EmptyVerify;
							_delegateVerification = new List<string>();
						});
						
					}
				}
			}
		}
		 */

		public static void ClearIfOutOfRange()
		{
			lock (_verificationLock)
			{
				if (_verification.Item1 != _emptyVerify.Item1)
				{
					bool isInRange = RotationCombatUtil.ExecuteActionOnTarget(
						_verification.Item2,
						luaUnitId => Lua.LuaDoString<bool>($@"
                    local spellInRange = IsSpellInRange(""{_verification.Item1}"", ""{luaUnitId}"") == 1;
                    --DEFAULT_CHAT_FRAME:AddMessage(""Checking range of {_verification.Item1} on {luaUnitId} is "" .. (spellInRange and 'true' or 'false'));
                    return spellInRange;")
					);

					if (!isInRange && !RotationFramework.Me.IsCast)
					{
						RotationLogger.Debug($"Force clearing verification for {_verification.Item1} on {_verification.Item2} because we're out of range");
						_verification = _emptyVerify;
					}
				}
			}
		}

		private static bool IsWaitingForSpellOnTarget(string spellName, ulong guid)
		{
			return _verification.Item1 == spellName && _verification.Item2 == guid;
		}

		private static bool IsWaitingOnTarget(ulong guid)
		{
			return _verification.Item2 == guid;
		}

		private static RotationSpell.VerificationType GetVerificationType()
		{
			return _verification.Item3;
		}

		private static void ClearVerificationOlderThan(uint seconds)
		{
			if (_verification.Item1 != _emptyVerify.Item1 && _verification.Item4.AddSeconds(seconds) < DateTime.Now)
			{
				RotationLogger.Debug($"Force clearing verification because spell could not be verified for {seconds} seconds");
				_verification = _emptyVerify;
			}
		}

		// it's also possible to create more "active" delegates, by confirming that the unit was *actually* healed, for example: 
		// https://wow.gamepedia.com/API_CombatTextSetActiveUnit => this is already set when casting a spell on a target - it "sticks" to the guid until changed
		// https://wow.gamepedia.com/COMBAT_TEXT_UPDATE can then be used to observed that something was successful
		private static void CreatePassiveEventDelegate(string delegatedEvent)
		{
			//we are already setting focus whenever we successfully cast a spell
			//RotationCombatUtil.SetFocusGuid(_verification.Item2);
			_delegateVerification = delegatedEvent;
		}

		private static void RegisterCombatLogClearer()
		{
			//combat log clearer for TBC
			Lua.LuaDoString(@"
            if not combatLogClearer then
                combatLogClearer = true;
                local f = CreateFrame(""Frame"", nil, UIParent); 
                f:SetScript(""OnUpdate"", CombatLogClearEntries);
            end
            ");
		}

		public static WoWUnit GetWoWObjectByLuaUnitId(string luaUnitId)
		{
			ulong guid = GetGUIDForLuaGUID(Lua.LuaDoString<string>($"return UnitGUID('{luaUnitId}')"));
			if (!string.IsNullOrWhiteSpace(luaUnitId))
				return ObjectManager.GetObjectWoWUnit().FirstOrDefault(o => o.Guid == guid);
			return null;
		}

		public static ulong GetGUIDForLuaGUID(string luaGuid)
		{
			ulong guid;
			ulong.TryParse(luaGuid.Replace("x", string.Empty), System.Globalization.NumberStyles.HexNumber, null, out guid);
			return guid;
		}
	}
}