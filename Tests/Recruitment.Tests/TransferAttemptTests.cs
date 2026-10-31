using System;
using Recruitment.Logic;
using Xunit;
namespace Recruitment.Tests
{
    public class TransferAttemptTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ExceptionsRetainPawnAndCanRetry(bool preparationFails)
        {
            bool held = true, delivered = false;
            Action prepare = () => { if (preparationFails) throw new InvalidOperationException(); };
            Action transfer = () => { held = false; throw new InvalidOperationException(); };
            Assert.False(TransferAttempt.Run(prepare, transfer, () => delivered, () => held = true, _ => { }));
            Assert.True(held);
            Assert.True(TransferAttempt.Run(() => { }, () => { held = false; delivered = true; }, () => delivered, () => held = true, _ => { }));
            Assert.True(delivered);
            Assert.False(held);
        }
        [Fact]
        public void PreparationFailureCannotCommitOnPreExistingOwnership()
        {
            bool recovered = false, transferred = false;
            Assert.False(TransferAttempt.Run(() => throw new InvalidOperationException(),
                () => transferred = true, () => true, () => recovered = true, _ => { }));
            Assert.True(recovered);
            Assert.False(transferred);
        }

        [Fact]
        public void ExceptionAfterExternalOwnershipDoesNotRecoverOrDuplicateDelivery()
        {
            bool delivered = false, recovered = false;
            Assert.True(TransferAttempt.Run(() => { }, () => { delivered = true; throw new InvalidOperationException(); },
                () => delivered, () => recovered = true, _ => { }));
            Assert.False(recovered);
        }
        [Fact]
        public void UnsuccessfulNonThrowingTransferAlsoRecovers()
        {
            bool held = false;
            Assert.False(TransferAttempt.Run(() => { }, () => { }, () => false, () => held = true, _ => { }));
            Assert.True(held);
        }
        [Fact]
        public void SecondPawnFailureDoesNotLoseFirstOrThird()
        {
            bool[] delivered = {false, false, false};
            bool[] held = {true, true, true};
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                TransferAttempt.Run(() => { }, () => { held[index] = false; if(index == 1) throw new InvalidOperationException(); delivered[index] = true; },
                    () => delivered[index], () => held[index] = true, _ => { });
            }
            Assert.Equal(new[] {true, false, true}, delivered);
            Assert.Equal(new[] {false, true, false}, held);
        }
    }
}
