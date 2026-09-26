using NUnit.Framework;
using ShortLegs.Core;

namespace ShortLegs.Tests
{
    public class ShrinkMatrixTests
    {
        [TestCase(0, 1.00f)]
        [TestCase(1, 0.75f)]
        [TestCase(2, 0.50f)]
        [TestCase(3, 0.25f)]
        [TestCase(4, 0.00f)]
        [TestCase(9, 0.00f)]
        public void LegScale_Drops25PercentPerLie(int lies, float expected) =>
            Assert.That(ShrinkMatrix.LegScale(lies), Is.EqualTo(expected).Within(1e-5f));

        [Test]
        public void Speed_IsBaseTimesLegScale() =>
            Assert.That(ShrinkMatrix.Speed(4f, 2), Is.EqualTo(2f).Within(1e-5f));

        [Test]
        public void NavigationLocks_FollowTheMatrix()
        {
            Assert.That(ShrinkMatrix.Caps(0).CanSprint, Is.True);
            Assert.That(ShrinkMatrix.Caps(1).CanSprint, Is.False);
            Assert.That(ShrinkMatrix.Caps(1).CanStepUp, Is.True);
            Assert.That(ShrinkMatrix.Caps(2).CanStepUp, Is.False);
            Assert.That(ShrinkMatrix.Caps(2).CanJump, Is.True);
            Assert.That(ShrinkMatrix.Caps(3).CanJump, Is.False);
            Assert.That(ShrinkMatrix.Caps(3).MustCrawl, Is.True);
        }

        [TestCase(0, 1.00f)]
        [TestCase(1, 1.15f)]
        [TestCase(3, 1.45f)]
        public void VoicePitch_Rises15PercentPerLie(int lies, float expected) =>
            Assert.That(ShrinkMatrix.VoicePitch(lies), Is.EqualTo(expected).Within(1e-5f));
    }
}
