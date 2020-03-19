using System.Collections.Generic;
using wManager.Wow.Helpers;

namespace CombatRotation.RotationFramework
{
	public class RotationEventHandler
	{
		private static string _luaPlayerGuid;
		private static string _playerName = RotationFramework.Me.Name;

		public static void Start()
		{
			_luaPlayerGuid = Lua.LuaDoString<string>("return UnitGUID('player');");
			EventsLuaWithArgs.OnEventsLuaStringWithArgs += CombatLogEventHandler;
			EventsLuaWithArgs.OnEventsLuaStringWithArgs += RotationSpellVerifier.NotifyForDelegate;
		}

		public static void Stop()
		{
			EventsLuaWithArgs.OnEventsLuaStringWithArgs -= RotationSpellVerifier.NotifyForDelegate;
			EventsLuaWithArgs.OnEventsLuaStringWithArgs -= CombatLogEventHandler;
		}

		private static void CombatLogEventHandler(string id, List<string> args)
		{
			if (id == "PLAYER_DEAD")
			{
				RotationSpellVerifier.ForceClearVerification();
			}

			if (id == "COMBAT_LOG_EVENT_UNFILTERED")
			{
				RotationSpellVerifier.NotifyCombatLog(args);
			}

			if (id == "UNIT_SPELLCAST_FAILED" || id == "UNIT_SPELLCAST_INTERRUPTED" || id == "UNIT_SPELLCAST_FAILED_QUIET")
			{
				string luaUnitId = args[0];
				string spellName = args[1];
				if (luaUnitId == "player" && RotationSpellVerifier.IsSpellWaitingForVerification(spellName))
				{
					RotationSpellVerifier.ForceClearVerification(spellName);
				}
			}

			if (id == "UNIT_SPELLCAST_SUCCEEDED" || id == "UNIT_SPELLCAST_SENT")
			{
				string luaUnitId = args[0];
				string spellName = args[1];
				// we're creating a fake combat log event to notify that a spell has successfully finished casting
				// SPELL_CAST_SUCCESS otherwise only fires for instant spells
				if (luaUnitId == "player" && RotationSpellVerifier.IsSpellWaitingForVerification(spellName))
				{
					List<string> combatLogEvent = new List<string>
					{
						"0", "SPELL_CAST_SUCCESS", _luaPlayerGuid, _playerName,
						"0x0000000000000000", "0x0000000000000000", "nil", "0x0000000000000000", "0", spellName, "0x00"
					};
					RotationSpellVerifier.NotifyCombatLog(combatLogEvent);
				}
			}

			//fake combat log event to clear help with force clearing verifications
			if (id == "UNIT_COMBAT")
			{
				List<string> combatLogEvent = new List<string>
				{
					"0", "NONE", _luaPlayerGuid, _playerName, "0x0000000000000000", "0x0000000000000000", "nil", "0x0000000000000000"
				};
				RotationSpellVerifier.NotifyCombatLog(combatLogEvent);
			}

			if (id == "COMBAT_TEXT_UPDATE")
			{
			}

			// this error is not found through casting or combatlog events because it's caused by the client checking IsSpellInRange when using CastSpellByName
			// we could technically execute this check ourselves in CombatLogUtil but usually the client-side range check (memory based GetDistance) is enough) and cheaper!
			// therefore we're listening to error messages and executing this check lazily
			if (id == "UI_ERROR_MESSAGE" && (args[0] == "Out of range." || args[0] == "You are too far away!"))
			{
				RotationSpellVerifier.ClearIfOutOfRange();
			}
		}
	}
}