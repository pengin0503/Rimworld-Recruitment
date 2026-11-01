using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class ApplicantScoreTests
    {
        private static readonly ScoreWeights Default = ScoreWeights.Default;

        [Fact]
        public void DefaultsMatchTheSpec()
        {
            Assert.Equal(2f, Default.MinorPassion);
            Assert.Equal(4f, Default.MajorPassion);
            Assert.Equal(5f, Default.IncapablePenalty);
            Assert.Equal(5f, Default.HealthPenalty);
        }

        [Fact]
        public void OnlyTheSixBestSkillsCount()
        {
            int[] levels = { 1, 2, 3, 4, 5, 6, 7, 8 };
            var applicant = new ApplicantFacts(levels, new int[levels.Length], 0, 0);
            Assert.Equal(3 + 4 + 5 + 6 + 7 + 8, ApplicantScore.Score(applicant, Default));
        }

        [Fact]
        public void FewerThanSixSkillsAreAllCounted()
        {
            var applicant = new ApplicantFacts(new[] { 10, 4 }, new[] { 0, 0 }, 0, 0);
            Assert.Equal(14f, ApplicantScore.Score(applicant, Default));
        }

        [Fact]
        public void PassionsOnAnySkillAddTheirBonus()
        {
            // The passion on the lowest skill (not among the best six) still counts.
            int[] levels = { 9, 9, 9, 9, 9, 9, 1 };
            int[] passions = { 1, 2, 0, 0, 0, 0, 2 };
            var applicant = new ApplicantFacts(levels, passions, 0, 0);
            Assert.Equal(54f + 2f + 4f + 4f, ApplicantScore.Score(applicant, Default));
        }

        [Fact]
        public void UnknownPassionsAreIgnored()
        {
            // Mods can add passion values beyond Major; they neither help nor hurt.
            var applicant = new ApplicantFacts(new[] { 5 }, new[] { 7 }, 0, 0);
            Assert.Equal(5f, ApplicantScore.Score(applicant, Default));
        }

        [Fact]
        public void IncapabilitiesAndHealthProblemsArePenalised()
        {
            var applicant = new ApplicantFacts(new[] { 10 }, new[] { 0 }, 2, 3);
            Assert.Equal(10f - 10f - 15f, ApplicantScore.Score(applicant, Default));
        }

        [Fact]
        public void WeightsComeFromTheSettings()
        {
            var weights = new ScoreWeights(1f, 10f, 0f, 2f);
            var applicant = new ApplicantFacts(new[] { 3, 3 }, new[] { 1, 2 }, 4, 1);
            Assert.Equal(6f + 1f + 10f - 0f - 2f, ApplicantScore.Score(applicant, weights));
        }

        [Fact]
        public void BestPicksTheHighestScore()
        {
            Assert.Equal(2, ApplicantScore.Best(new[] { 3f, -1f, 8f, 7f }));
        }

        [Fact]
        public void TiesGoToTheFirstCandidate()
        {
            Assert.Equal(1, ApplicantScore.Best(new[] { 1f, 5f, 5f }));
        }

        [Fact]
        public void NoCandidatesGiveMinusOne()
        {
            Assert.Equal(-1, ApplicantScore.Best(new float[0]));
        }
    }
}
