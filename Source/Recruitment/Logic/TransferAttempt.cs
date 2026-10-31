using System;

namespace Recruitment.Logic
{
    /// <summary>A transfer commits only when the recipient owns the pawn. Preparation also runs inside recovery.</summary>
    public static class TransferAttempt
    {
        public static bool Run(Action prepare, Action transfer, Func<bool> completed, Action recover, Action<Exception> report)
        {
            bool transferStarted = false;
            try
            {
                prepare();
                transferStarted = true;
                transfer();
                if (completed()) return true;
            }
            catch (Exception error)
            {
                // A worker may throw after spawning or handing the pawn to a pod. Never steal it back.
                if (transferStarted && completed())
                {
                    report(error);
                    return true;
                }
                recover();
                report(error);
                return false;
            }
            recover();
            return false;
        }
    }
}
