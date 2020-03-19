using System;
using wManager.Wow.ObjectManager;

namespace CombatRotation.RotationFramework
{
	public class RotationStep
	{
		public readonly float Priority;
		private readonly RotationAction _action;
		private readonly Func<RotationAction, WoWUnit, bool> _predicate;
		private readonly Func<Func<WoWUnit, bool>, WoWUnit> _targetFinder;
		private readonly bool _forceCast = false;
		private readonly bool _checkRange = true;

		public RotationStep(RotationAction spell,
			float priority,
			Func<RotationAction, WoWUnit, bool> predicate,
			Func<Func<WoWUnit, bool>, WoWUnit> targetFinder,
			bool forceCast = false,
			bool checkRange = true)
		{
			_action = spell;
			Priority = priority;
			_predicate = predicate;
			_targetFinder = targetFinder;
			_forceCast = forceCast;
			_checkRange = checkRange;
		}

		public bool ExecuteStep(bool globalActive)
		{
			//can't execute this, because global is still active
			//can't execute this because we can't stop the current cast to execute this
			if ((globalActive && !_action.IgnoresGlobal()) || (RotationFramework.IsCast && !_forceCast))
			{
				return false;
			}

			//predicate is executed separately from targetfinder predicate
			//this way we can select one target, then check which spell to cast on the target
			//as opposed to finding a target to cast a specific spell on (not the desired result)
			Func<WoWUnit, bool> targetFinderPredicate = _checkRange ? (Func<WoWUnit, bool>) ((u) => u.GetDistance <= _action.Range()) : ((u) => true);

			var watch = System.Diagnostics.Stopwatch.StartNew();
			string spellName = "<noname>";
			if (_action.GetType() == typeof(RotationSpell))
			{
				RotationSpell spell = (RotationSpell) _action;
				spellName = spell.FullName();
			}

			WoWUnit target = _targetFinder(targetFinderPredicate);

			watch.Stop();
			RotationLogger.Trace($"({spellName}) targetFinder ({_targetFinder.Method.Name}) - {target?.Name}: {watch.ElapsedMilliseconds} ms");

			watch.Restart();
			if (target != null && _predicate(_action, target))
			{
				watch.Stop();
				RotationLogger.Trace($"({spellName}) predicate ({_targetFinder.Method.Name}): on {target.Name} {watch.ElapsedMilliseconds} ms");

				watch.Restart();
				var returnValue = _action.Execute(target, _forceCast);

				watch.Stop();
				RotationLogger.Trace($"action ({spellName}): {watch.ElapsedMilliseconds} ms");

				return returnValue;
			}

			return false;
		}
	}
}