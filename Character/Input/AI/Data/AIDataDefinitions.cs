using UnityEngine;

namespace BBBNexus
{
    public readonly struct NavigationContext
    {
        public readonly Vector3 DesiredWorldDirection;
        public readonly Vector3 TargetWorldDirection;
        public readonly float DistanceToTarget;
        public readonly bool HasValidTarget;
        public readonly bool NeedsJump; 

        public NavigationContext(Vector3 desiredDir, Vector3 targetDir, float dist, bool isValid, bool needsJump)
        {
            DesiredWorldDirection = desiredDir;
            TargetWorldDirection = targetDir;
            DistanceToTarget = dist;
            HasValidTarget = isValid;
            NeedsJump = needsJump;
        }
    }

    public readonly struct TacticalIntent
    {
        public readonly Vector2 MovementInput;
        public readonly Vector2 LookInput;
        public readonly bool WantsToAttack;
        public readonly bool WantsToAim;
        public readonly bool WantsToJump;
        public readonly bool WantsToDodge;
        public readonly bool WantsToRoll;

        public TacticalIntent(Vector2 moveInput, Vector2 lookInput, bool attack, bool aim, bool jump, bool dodge = false, bool roll = false)
        {
            MovementInput = moveInput;
            LookInput = lookInput;
            WantsToAttack = attack;
            WantsToAim = aim;
            WantsToJump = jump;
            WantsToDodge = dodge;
            WantsToRoll = roll;
        }
    }
}