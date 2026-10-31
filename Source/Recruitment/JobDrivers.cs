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
                GameComponent_Recruitment.Instance.TryStart(method, pawn.Map, trader.Faction, pawn);
            };
            start.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return start;
        }
    }

    /// <summary>Use a comms console to broadcast a call for settlers. Target A: the console.</summary>
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
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.InteractionCell);
            Toil broadcast = Toils_General.Wait(BroadcastTicks, TargetIndex.A);
            broadcast.WithProgressBarToilDelay(TargetIndex.A);
            yield return broadcast;
            Toil start = ToilMaker.MakeToil("BroadcastRecruitment");
            start.initAction = () =>
            {
                RecruitmentMethodDef method = RecruitmentMethodDef.ForJob(job.def);
                GameComponent_Recruitment.Instance.TryStart(method, pawn.Map, null, pawn);
            };
            start.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return start;
        }
    }

    /// <summary>Walk off the map as an envoy. Target A: the exit cell.</summary>
    public class JobDriver_DepartAsEnvoy : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            Toil leave = ToilMaker.MakeToil("DepartAsEnvoy");
            leave.initAction = () => GameComponent_Recruitment.Instance.Notify_EnvoyReachedExit(pawn);
            leave.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return leave;
        }
    }
}
