using Recruitment.Logic;
using Xunit;

namespace Recruitment.Tests
{
    public class PaymentPlanTests
    {
        [Fact]
        public void TakesFromStacksInOrderUntilPaid()
        {
            Assert.Equal(new[] { 30, 20, 0 }, PaymentPlan.Plan(new[] { 30, 40, 50 }, 50));
        }

        [Fact]
        public void ExactTotalIsEnough()
        {
            Assert.Equal(new[] { 10, 15 }, PaymentPlan.Plan(new[] { 10, 15 }, 25));
        }

        [Fact]
        public void InsufficientSilverGivesNull()
        {
            Assert.Null(PaymentPlan.Plan(new[] { 10, 15 }, 26));
        }

        [Fact]
        public void FreeCostTakesNothing()
        {
            Assert.Equal(new[] { 0, 0 }, PaymentPlan.Plan(new[] { 10, 15 }, 0));
            Assert.Equal(new int[0], PaymentPlan.Plan(new int[0], 0));
        }

        [Fact]
        public void NonPositiveStacksAreSkipped()
        {
            Assert.Equal(new[] { 0, 5 }, PaymentPlan.Plan(new[] { -3, 9 }, 5));
        }
    }
}
