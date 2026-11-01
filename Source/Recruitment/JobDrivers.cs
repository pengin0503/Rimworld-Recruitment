using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Recruitment
{
    /// <summary>Walk to a visiting trader and ask them to spread word of the colony. Target A: the trader.</summary>
    public class JobDriver_RequestRecruitmentFromTrader : JobDriver
    {
        private const int TalkTicks = 180;

        private Pawn Trader => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !((ITrader)Trader).CanTradeNow);
            AddFinishAction(condition =>
            {
                // A trader who walks off or stops trading ends the job without asking; say so instead of failing silently.
                if (condition == JobCondition.Incompletable)
                {
                    Messages.Message("Recruitment_TraderLeft".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil talk = Toils_General.Wait(TalkTicks, TargetIndex.A);
            talk.WithProgressBarToilDelay(TargetIndex.A);
            talk.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return talk;
            Toil start = ToilMaker.MakeToil("RequestRecruitment");
            start.initAction = () =>
            {
                RecruitmentMethodDef method = RecruitmentMethodDef.ForJob(job.def);
                Pawn trader = Trader;
                AcceptanceReport canTrade = pawn.CanTradeWith(trader.Faction, trader.TraderKind);
                if (!canTrade.Accepted)
                {
                    Messages.Message(canTrade.Reason.NullOrEmpty() ? "CannotTrade".Translate().Resolve() : canTrade.Reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
                // Job.count holds the premium chosen in the start dialog (-1, i.e. none, for jobs from 1.0 saves).
                GameComponent_Recruitment.Instance.TryStart(method, pawn.Map, trader.Faction, pawn, System.Math.Max(0, job.count));
            };
            start.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return start;
        }
    }

    /// <summary>
    /// Legacy: radio and orbital drives used to be started with this job from the console's menu. They now go
    /// through the comms console's own contact list (<see cref="RecruitmentCommTarget"/>); this stays so that
    /// saves with the job in progress still load and finish it. Target A: the console.
    /// </summary>
    public class JobDriver_BroadcastRecruitment : JobDriver
    {
        private const int BroadcastTicks = 300;

        private Building_CommsConsole Console => (Building_CommsConsole)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Console.CanUseCommsNow);
            AddFinishAction(condition =>
            {
                if (condition == JobCondition.Incompletable)
                {
                    Messages.Message("Recruitment_BroadcastInterrupted".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
            });
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.InteractionCell);
            Toil broadcast = Toils_General.Wait(BroadcastTicks, TargetIndex.A);
            broadcast.WithProgressBarToilDelay(TargetIndex.A);
            yield return broadcast;
            Toil start = ToilMaker.MakeToil("BroadcastRecruitment");
            start.initAction = () =>
            {
                RecruitmentMethodDef method = RecruitmentMethodDef.ForJob(job.def);
                GameComponent_Recruitment.Instance.TryStart(method, pawn.Map, null, pawn, premiumTier: 0);
            };
            start.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return start;
        }
    }

    /// <summary>
    /// Legacy: envoys used to walk off the map with this job. Kept so saves with the job in progress still load;
    /// <see cref="GameComponent_Recruitment"/> refunds such trips after loading, and the job ends at once.
    /// </summary>
    public class JobDriver_DepartAsEnvoy : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil end = ToilMaker.MakeToil("LegacyDepartAsEnvoy");
            end.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return end;
        }
    }
}
