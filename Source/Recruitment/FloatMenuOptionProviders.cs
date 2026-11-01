using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Recruitment
{
    internal static class RecruitmentFloatMenu
    {
        public static string OptionLabel(RecruitmentMethodDef method)
        {
            FloatRange days = method.Days;
            return "Recruitment_FloatMenu_Start".Translate(method.LabelCap, method.Cost, days.min.ToString("0.#"), days.max.ToString("0.#"));
        }

        public static FloatMenuOption Disabled(RecruitmentMethodDef method, string reason)
        {
            return new FloatMenuOption(OptionLabel(method) + ": " + reason, null);
        }
    }

    /// <summary>"Ask to spread word of the colony" on a visiting trader.</summary>
    public class FloatMenuOptionProvider_RecruitViaTrader : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            RecruitmentMethodDef method = RecruitmentDefOf.Recruitment_Caravan;
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            if (component == null || clickedPawn.TraderKind == null || !((ITrader)clickedPawn).CanTradeNow)
            {
                yield break;
            }
            Pawn negotiator = context.FirstSelectedPawn;
            if (!negotiator.CanReach(clickedPawn, PathEndMode.Touch, Danger.Deadly))
            {
                yield return RecruitmentFloatMenu.Disabled(method, "NoPath".Translate().CapitalizeFirst());
                yield break;
            }
            AcceptanceReport canTrade = negotiator.CanTradeWith(clickedPawn.Faction, clickedPawn.TraderKind);
            if (!canTrade.Accepted)
            {
                yield return RecruitmentFloatMenu.Disabled(method, canTrade.Reason.NullOrEmpty() ? "MissingTitleAbility".Translate().CapitalizeFirst().Resolve() : canTrade.Reason);
                yield break;
            }
            AcceptanceReport canStart = component.CanStart(method, context.map, needsOriginLottery: false);
            if (!canStart.Accepted)
            {
                yield return RecruitmentFloatMenu.Disabled(method, canStart.Reason);
                yield break;
            }
            yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(RecruitmentFloatMenu.OptionLabel(method),
                () => RecruitmentUI.OpenForTrader(method, negotiator, clickedPawn),
                MenuOptionPriority.InitiateSocial, null, clickedPawn), negotiator, clickedPawn);
        }
    }
}
