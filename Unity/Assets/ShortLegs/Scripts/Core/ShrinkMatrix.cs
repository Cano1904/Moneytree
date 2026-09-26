using System;

namespace ShortLegs.Core
{
    /// <summary>What a character can still do at a given number of lies.</summary>
    public readonly struct LocomotionCaps
    {
        public readonly float LegScale;
        public readonly float SpeedMultiplier;
        public readonly bool CanSprint;
        public readonly bool CanStepUp;
        public readonly bool CanJump;
        public readonly bool MustCrawl;
        public readonly float VoicePitch;

        public LocomotionCaps(int lies)
        {
            LegScale = ShrinkMatrix.LegScale(lies);
            SpeedMultiplier = LegScale;
            CanSprint = ShrinkMatrix.CanSprint(lies);
            CanStepUp = ShrinkMatrix.CanStepUp(lies);
            CanJump = ShrinkMatrix.CanJump(lies);
            MustCrawl = ShrinkMatrix.MustCrawl(lies);
            VoicePitch = ShrinkMatrix.VoicePitch(lies);
        }
    }

    /// <summary>
    /// The Deception-Shrink Matrix (GDD §5.2). Pure math shared by server validation and client
    /// presentation so both always agree.
    /// </summary>
    public static class ShrinkMatrix
    {
        public const float ShrinkPerLie = 0.25f;
        public const float PitchPerLie = 0.15f;
        public const int SprintLockLies = 1;
        public const int StepLockLies = 2;
        public const int CrawlLies = 3;

        public static float LegScale(int lies) => Math.Max(0f, 1f - ShrinkPerLie * Math.Max(0, lies));

        /// <summary>Speed = Base_Speed * Current_Leg_Scale.</summary>
        public static float Speed(float baseSpeed, int lies) => baseSpeed * LegScale(lies);

        public static bool CanSprint(int lies) => lies < SprintLockLies;
        public static bool CanStepUp(int lies) => lies < StepLockLies;
        public static bool CanJump(int lies) => lies < CrawlLies;
        public static bool MustCrawl(int lies) => lies >= CrawlLies;

        /// <summary>+15% voice frequency per lie.</summary>
        public static float VoicePitch(int lies) => 1f + PitchPerLie * Math.Max(0, lies);

        public static bool IsExposed(int lies, int maxLiesAllowed) => lies >= maxLiesAllowed;

        public static LocomotionCaps Caps(int lies) => new LocomotionCaps(lies);
    }
}
