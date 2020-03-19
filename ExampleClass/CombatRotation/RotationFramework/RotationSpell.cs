using wManager.Wow.Class;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace CombatRotation.RotationFramework
{
	public class RotationSpell : RotationAction
	{
		public Spell Spell;
		private readonly string _name;
		private readonly uint? _rank;
		private readonly bool _ignoresGlobal = false;

		public RotationSpell(string name, uint? rank = null, bool ignoresGlobal = false, VerificationType type = VerificationType.CAST_RESULT)
		{
			Spell = new Spell(name);
			_name = Spell.NameInGame;
			_rank = rank;
			_ignoresGlobal = ignoresGlobal;
			Verification = type;
		}

		public bool NotEnoughMana()
		{
			return Lua.LuaDoString<bool>($@"return select(2, IsUsableSpell(""{FullName()}""))");
		}

		public bool IsUsable()
		{
			return Lua.LuaDoString<bool>($@"return IsUsableSpell(""{FullName()}"")");
		}

		public bool CanCast()
		{
			return Lua.LuaDoString<bool>($@"
            local spellCooldown = 0;
            local start, duration, enabled = GetSpellCooldown(""{_name}"");
            if enabled == 1 and start > 0 and duration > 0 then
                spellCooldown = duration - (GetTime() - start)
            elseif enabled == 0 then
                spellCooldown = 1000000.0;
            end

            return (IsUsableSpell(""{FullName()}"") and spellCooldown == 0)");
		}

		public float GetCooldown()
		{
			string luaString = $@"
            local start, duration, enabled = GetSpellCooldown(""{_name}"");
            if enabled == 1 and start > 0 and duration > 0 then
                return duration - (GetTime() - start)
            elseif enabled == 0 then
                return 10000000.0;
            end
            return 0;";
			return Lua.LuaDoString<float>(luaString);
		}

		public string FullName()
		{
			return _rank != null ? ($"{_name}({RotationSpellbook.RankString} {_rank})") : _name;
		}

		public bool IsKnown()
		{
			return RotationSpellbook.IsKnown(_name, _rank ?? 1);
		}

		public VerificationType Verification { get; }

		public override int GetHashCode()
		{
			return _name.GetHashCode() + _rank.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			RotationSpell otherObj = (RotationSpell) obj;
			return _name.Equals(otherObj?._name) && _rank == otherObj?._rank;
		}

		public float CastTime()
		{
			return RotationSpellbook.Get(_name, _rank ?? 0)?.CastTime ?? 0;
		}

		public bool Execute(WoWUnit target, bool force = false)
		{
			return RotationCombatUtil.CastSpell(this, target, force);
		}

		public float Range()
		{
			return Spell.MaxRange;
		}

		public bool IgnoresGlobal()
		{
			return _ignoresGlobal;
		}

		public enum VerificationType
		{
			CAST_RESULT,
			CAST_SUCCESS,
			AURA,
			NONE
		}
	}
}