using System;
using wManager.Wow.ObjectManager;

namespace CombatRotation.RotationFramework
{
	class RotationRawAction : RotationAction
	{
		private readonly Action _rotationAction;
		private readonly float _actionRange;
		private readonly bool _ignoresGlobal;

		public RotationRawAction(Action rotationAction, float actionRange, bool ignoresGlobal = false)
		{
			_rotationAction = rotationAction;
			_actionRange = actionRange;
			_ignoresGlobal = ignoresGlobal;
		}

		public bool Execute(WoWUnit target, bool force = false)
		{
			_rotationAction.Invoke();
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