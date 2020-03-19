using System;
using System.Collections.Generic;
using System.Linq;
using robotManager.Helpful;
using wManager.Wow.Helpers;

namespace CombatRotation.RotationFramework
{
	public class RotationSpellbook
	{
		private static List<PlayerSpell> _playerSpells = new List<PlayerSpell>();
		private static DateTime _lastUpdate = DateTime.MinValue;
		private static readonly string Locale = Lua.LuaDoString<string>("return GetLocale()");

		private static readonly Dictionary<string, string> LocaleToRank = new Dictionary<string, string>
		{
			{"enGB", "Rank"},
			{"enUS", "Rank"},
			{"deDE", "Rang"},
			{"frFr", "Rang"},
			{"ruRU", "Уровень"}
		};

		public RotationSpellbook()
		{
			EventsLuaWithArgs.OnEventsLuaStringWithArgs += LuaEventHandler;
			_playerSpells.AddRange(GetSpellsFromLua());
			_playerSpells.AddRange(GetSpellsFromLua("BOOKTYPE_PET"));
			foreach (var playerSpell in _playerSpells)
			{
				RotationLogger.Debug($"Fightclass framework found in spellbook: {playerSpell.Name} Rank {playerSpell.Rank}");
			}
		}

		~RotationSpellbook()
		{
			EventsLuaWithArgs.OnEventsLuaStringWithArgs -= LuaEventHandler;
		}

		private void LuaEventHandler(string id, List<string> args)
		{
			if (id == "LEARNED_SPELL_IN_TAB")
			{
				RotationLogger.Debug($"Updating known spells because of {id}");
				SpellUpdateHandler();
			}

			if (id == "PET_BAR_UPDATE" && _lastUpdate.AddSeconds(1) < DateTime.Now)
			{
				_lastUpdate = DateTime.Now;
				RotationLogger.Debug($"Updating known spells because of {id}");
				SpellUpdateHandler();
			}
		}

		public static string RankString => LocaleToRank[Locale];

		public static bool IsKnown(string spellName, uint rank = 1)
		{
			return _playerSpells.Any(spell => spell.Name == spellName && spell.Rank >= rank);
		}

		public static PlayerSpell Get(string spellName, uint rank = 0)
		{
			if (rank != 0)
			{
				return _playerSpells.FirstOrDefault(spell => spell.Name == spellName && spell.Rank == rank);
			}

			return _playerSpells.Where(spell => spell.Name == spellName).OrderBy(p => p.Rank).LastOrDefault();
		}

		private void SpellUpdateHandler()
		{
			_playerSpells.Clear();
			_playerSpells.AddRange(GetSpellsFromLua());
			_playerSpells.AddRange(GetSpellsFromLua("BOOKTYPE_PET"));
		}

		private List<PlayerSpell> GetSpellsFromLua(string bookType = "BOOKTYPE_SPELL")
		{
			//first cata version
			string luaString = Usefuls.WowVersion >= 13164 ? GetRankStringCata(bookType) : GetRankStringTbc(bookType);

			List<string> spellsAsLua = Lua.LuaDoString<string>(luaString).Split(';').ToList();
			return spellsAsLua
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.Select(spellString =>
				{
					var rank = convertSafely(spellString, 1, 1);
					var castTime = convertSafely(spellString, 2, 0);
					var minRange = convertSafely(spellString, 3, 0);
					var maxRange = convertSafely(spellString, 4, 5);

					return new PlayerSpell
					{
						Name = spellString.Split('+')[0],
						Rank = rank,
						CastTime = castTime,
						MinRange = minRange,
						MaxRange = maxRange
					};
				})
				.ToList();
		}

		private static uint convertSafely(string spellString, int index, uint fallback)
		{
			var returnValue = fallback;
			try
			{
				returnValue = Convert.ToUInt32(spellString.Split('+')[index]);
			}
			catch (Exception e)
			{
				Logging.WriteError($"Error converting {spellString}");
			}

			return returnValue;
		}

		private string GetRankStringTbc(string bookType = "BOOKTYPE_SPELL")
		{
			return $@"
	        local knownSpells = """"
			local function round(n)
			    return n % 1 >= 0.5 and math.ceil(n) or math.floor(n)
			end

	        
	        local i = 1;
	        while true do
	            local spellName, spellRank = GetSpellName(i, {bookType});
	            
	            if not spellName then
	                break;
	            end

	            local _, _, currentRankString = string.find(spellRank, "" (%d+)$"");
	            local currentRank = tonumber(currentRankString);
	            local castTime, minRange, maxRange, spellId = 0, 0, 0, 0;

	            if (string.find(spellRank, "" (%d+)$"")) then
					-- name, rank, icon, cost, isFunnel, powerType, castTime, minRange, maxRange
	                _, _, _, _, _, _, castTime, minRange, maxRange = GetSpellInfo(spellName .. ""("" .. spellRank .. "")"");
	            end
	            
	            knownSpells = knownSpells .. spellName .. ""+"" .. (currentRank and currentRank or '1') .. ""+"" .. castTime .. ""+"" .. round(minRange) .. ""+"" .. round(maxRange) .. "";""            

	            i = i + 1;
	        end
	        return knownSpells;";
		}

		private string GetRankStringCata(string bookType = "BOOKTYPE_SPELL")
		{
			return $@"
	        local knownSpells = """"
			local function round(n)
			    return n % 1 >= 0.5 and math.ceil(n) or math.floor(n)
			end

	        
	        local i = 1;
	        while true do
	            local spellName, spellRank = GetSpellBookItemName(i, {bookType});
	            local skillType, special = GetSpellBookItemInfo(i, {bookType});
	            
	            if not spellName then
	                break;
	            end

	            local _, _, currentRankString = string.find(spellRank, "" (%d+)$"");
	            local currentRank = tonumber(currentRankString);
	            local castTime, minRange, maxRange, spellId = 0, 0, 0, 0;

	            if (skillType == ""SPELL"") then
	                _, _, _, castTime, minRange, maxRange = GetSpellInfo(special);
					spellId = special;
	            end
	            
	            knownSpells = knownSpells .. spellName .. ""+"" .. (currentRank and currentRank or '1') .. ""+"" .. castTime .. ""+"" .. round(minRange) .. ""+"" .. round(maxRange) .. "";""            

	            i = i + 1;
	        end
	        return knownSpells;";
		}

		public class PlayerSpell
		{
			public string Name;
			public uint CastTime;
			public uint MinRange;
			public uint MaxRange;
			public uint Rank = 1;
		}
	}
}