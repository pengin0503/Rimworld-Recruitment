namespace Recruitment.Logic
{
    public static class ConfirmationRule
    {
        /// <summary>
        /// Whether to ask before spending <paramref name="cost"/> silver. A threshold of 0 always asks;
        /// a negative threshold never asks.
        /// </summary>
        public static bool ShouldConfirm(int cost, int threshold)
        {
            return threshold >= 0 && cost >= threshold;
        }
    }
}
