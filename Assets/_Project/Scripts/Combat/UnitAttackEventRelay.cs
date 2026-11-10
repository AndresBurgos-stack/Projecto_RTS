using UnityEngine;

namespace ProjectoRTS.Combat
{
    /// <summary>
    /// Receives the Attack clip's animation event on the visual child that owns
    /// the Animator and forwards it to the UnitCombat on the unit root.
    /// Added at runtime by UnitCombat because the Animator is not on the root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitAttackEventRelay : MonoBehaviour
    {
        private UnitCombat _combat;

        public void Configure(UnitCombat combat)
        {
            _combat = combat;
        }

        // Animation events require a public method with this exact name.
        public void OnAttackHit()
        {
            if (_combat != null)
            {
                _combat.ApplyAttackHit();
            }
        }
    }
}
