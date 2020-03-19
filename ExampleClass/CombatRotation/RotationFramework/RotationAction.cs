using wManager.Wow.ObjectManager;

namespace CombatRotation.RotationFramework
{
    public interface RotationAction
    {
        bool Execute(WoWUnit target, bool force = false);

        float Range();

        bool IgnoresGlobal();
    }
}

