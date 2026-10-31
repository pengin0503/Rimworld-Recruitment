using Recruitment.Logic;
using Xunit;
namespace Recruitment.Tests
{
    public class ApplicantLayoutTests
    {
        [Theory]
        [InlineData(40f, 24f, 190f)]
        [InlineData(400f, 24f, 438f)]
        [InlineData(400f, 60f, 474f)]
        public void EveryRowFitsMeasuredText(float description, float name, float expected)
        {
            Assert.Equal(expected, ApplicantLayout.RowHeight(description, name));
        }
    }
}
