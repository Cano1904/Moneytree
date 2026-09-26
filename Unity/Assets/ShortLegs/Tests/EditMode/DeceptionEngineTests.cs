using System.Collections.Generic;
using NUnit.Framework;
using ShortLegs.Core;

namespace ShortLegs.Tests
{
    public class DeceptionEngineTests
    {
        private const int Liar = 2, Honest = 1;
        private const int Study = 10, Library = 11, Kitchen = 12;
        private const int MoneyTree = 50;

        private static Clue ClueOf(string id, params FactClaim[] facts)
        {
            var c = new Clue { Id = id, DisplayName = id, IsTrueClue = true };
            foreach (var f in facts) c.Facts.Add(new EvidenceFact(f));
            return c;
        }

        private static DeceptionEngine NewEngine(bool auto = true) => new DeceptionEngine(new List<Clue>
        {
            ClueOf("bootprint", new FactClaim(Liar, Predicate.WasIn, Study, 23)),
            ClueOf("glove", new FactClaim(Liar, Predicate.Touched, MoneyTree, 23)),
            new Clue { Id = "decoy", IsTrueClue = false },
        }, auto);

        [Test]
        public void LieAgainstUndiscoveredEvidence_IsLatent_NotPunished()
        {
            var e = NewEngine();
            var r = e.SubmitStatement(new Statement(Liar, new FactClaim(Liar, Predicate.WasIn, Library, 23), 0), out var d);
            Assert.That(d.HasValue, Is.False);
            Assert.That(r.IsLatent, Is.True);
        }

        [Test]
        public void DiscoveringTheClue_ExposesTheLatentLie()
        {
            var e = NewEngine();
            e.SubmitStatement(new Statement(Liar, new FactClaim(Liar, Predicate.WasIn, Library, 23), 0), out _);
            var exposed = e.DiscoverClue("bootprint");
            Assert.That(exposed, Has.Count.EqualTo(1));
            Assert.That(exposed[0].SpeakerId, Is.EqualTo(Liar));
            Assert.That(exposed[0].Record.WasFramed, Is.False);
        }

        [Test]
        public void LieAgainstDiscoveredEvidence_IsImmediate()
        {
            var e = NewEngine();
            e.DiscoverClue("glove");
            e.SubmitStatement(new Statement(Liar, new FactClaim(FactClaim.AnySubject, Predicate.Touched, MoneyTree, 23, negated: true), 0), out var d);
            Assert.That(d.HasValue, Is.True, "\"Nobody touched the Money Tree\" contradicts the glove");
        }

        [Test]
        public void TruthAndUnknowns_AreNeverLies()
        {
            var e = NewEngine();
            e.DiscoverClue("bootprint");
            e.DiscoverClue("glove");
            e.SubmitStatement(new Statement(Liar, new FactClaim(Liar, Predicate.WasIn, Study, 23), 0), out var truth);
            e.SubmitStatement(new Statement(Honest, new FactClaim(Honest, Predicate.WasIn, Kitchen, 23), 0), out var unknown);
            e.SubmitStatement(new Statement(Honest, new FactClaim(Liar, Predicate.WasIn, Study, 22), 0), out var otherTime);
            Assert.That(truth.HasValue || unknown.HasValue || otherTime.HasValue, Is.False);
        }

        [Test]
        public void EachStatement_ShrinksAtMostOnce()
        {
            var e = NewEngine();
            e.SubmitStatement(new Statement(Liar, new FactClaim(Liar, Predicate.WasIn, Library, 23), 0), out _);
            Assert.That(e.DiscoverClue("bootprint"), Has.Count.EqualTo(1));
            Assert.That(e.DiscoverClue("glove"), Is.Empty);
            Assert.That(e.DiscoverClue("bootprint"), Is.Empty, "rediscovery is a no-op");
        }

        [Test]
        public void PlantedClue_FramesAnHonestPlayer_AndDoesNotCountAsTrueClue()
        {
            var e = NewEngine();
            e.SubmitStatement(new Statement(Honest, new FactClaim(Honest, Predicate.WasIn, Kitchen, 23), 0), out _);
            var planted = ClueOf("planted_1", new FactClaim(Honest, Predicate.WasIn, Study, 23));
            planted.IsPlanted = true;
            e.RegisterClue(planted);
            Assert.That(e.Log[0].IsLatent, Is.True);

            var exposed = e.DiscoverClue("planted_1");
            Assert.That(exposed, Has.Count.EqualTo(1));
            Assert.That(exposed[0].Record.WasFramed, Is.True);
            Assert.That(e.DiscoveredTrueClueCount, Is.EqualTo(0));
        }

        [Test]
        public void StoryMode_RequiresPresentingTheRightClue()
        {
            var e = NewEngine(auto: false);
            var r = e.SubmitStatement(new Statement(Liar, new FactClaim(Liar, Predicate.WasIn, Library, 23), 0), out _);
            Assert.That(e.DiscoverClue("bootprint"), Is.Empty);
            e.DiscoverClue("glove");

            Assert.That(e.PresentEvidence(r.Index, "glove", out _), Is.False, "wrong clue");
            Assert.That(e.PresentEvidence(r.Index, "decoy", out _), Is.False, "undiscovered clue");
            Assert.That(e.PresentEvidence(r.Index, "bootprint", out var d), Is.True);
            Assert.That(d.ClueId, Is.EqualTo("bootprint"));
            Assert.That(e.PresentEvidence(r.Index, "bootprint", out _), Is.False, "already exposed");
        }
    }
}
