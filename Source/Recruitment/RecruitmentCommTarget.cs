using System.Linq;
using Recruitment.Logic;
using RimWorld;
using Verse;

namespace Recruitment
{
    /// <summary>
    /// The "broadcast a call for settlers" entry in a comms console's list of contacts (added by
    /// <see cref="HarmonyPatches"/>). Using it opens a negotiation-style dialog to choose radio or orbital comms.
    /// One instance per game, deep-saved by <see cref="GameComponent_Recruitment"/> so jobs can reference it.
    /// </summary>
    public class RecruitmentCommTarget : ICommunicable, IExposable, ILoadReferenceable
    {
        private static IOrderedEnumerable<RecruitmentMethodDef> CommsMethods => RecruitmentMethodDef.AllInOrder.Where(m => m.startKind == RecruitmentStartKind.CommsConsole).OrderBy(m => m.uiOrder);

        public string GetCallLabel()
        {
            return "Recruitment_Comms_CallLabel".Translate();
        }

        public string GetInfoText()
        {
            return "Recruitment_Comms_Info".Translate();
        }

        public Faction GetFaction()
        {
            return null;
        }

        public FloatMenuOption CommFloatMenuOption(Building_CommsConsole console, Pawn negotiator)
        {
            string label = "Recruitment_Comms_CallLabel".Translate();
            // Without any usable method the dialog would offer nothing, so say why up front.
            if (!CommsMethods.Any(m => m.ResearchFinished))
            {
                string research = CommsMethods.SelectMany(m => m.MissingResearch).Select(r => r.LabelCap.Resolve()).Distinct().ToCommaList();
                return new FloatMenuOption(label + " (" + "Recruitment_Reason_Research".Translate(research) + ")", null);
            }
            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, () =>
            {
                console.GiveUseCommsJob(negotiator, this);
            }, MenuOptionPriority.InitiateSocial), negotiator, console);
        }

        public void TryOpenComms(Pawn negotiator)
        {
            var dialog = new Dialog_Negotiation(negotiator, this, StartNode(negotiator), radioMode: true);
            dialog.soundAmbient = SoundDefOf.RadioComms_Ambience;
            Find.WindowStack.Add(dialog);
        }

        private DiaNode StartNode(Pawn negotiator)
        {
            var node = new DiaNode("Recruitment_Comms_Intro".Translate(negotiator.Named("PAWN")));
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            foreach (RecruitmentMethodDef method in CommsMethods)
            {
                var option = new DiaOption(RecruitmentFloatMenu.OptionLabel(method));
                AcceptanceReport report = component.CanStart(method, negotiator.Map, needsOriginLottery: true);
                if (!report.Accepted)
                {
                    option.Disable(report.Reason);
                }
                else if (ConfirmationRule.ShouldConfirm(method.Cost, RecruitmentMod.Settings.EffectiveConfirmThreshold))
                {
                    // The dialog already shows the cost; the confirmation step stays inside it.
                    option.link = ConfirmNode(method, negotiator, node);
                }
                else
                {
                    option.action = () => component.TryStart(method, negotiator.Map, null, negotiator);
                    option.resolveTree = true;
                }
                node.options.Add(option);
            }
            node.options.Add(new DiaOption("Recruitment_Comms_HangUp".Translate()) { resolveTree = true });
            return node;
        }

        private static DiaNode ConfirmNode(RecruitmentMethodDef method, Pawn negotiator, DiaNode back)
        {
            FloatRange days = method.Days;
            var node = new DiaNode("Recruitment_Confirm_Text".Translate(method.LabelCap, method.Cost, days.min.ToString("0.#"), days.max.ToString("0.#"), method.arrivalLabel));
            node.options.Add(new DiaOption("Confirm".Translate())
            {
                action = () => GameComponent_Recruitment.Instance.TryStart(method, negotiator.Map, null, negotiator),
                resolveTree = true,
            });
            node.options.Add(new DiaOption("GoBack".Translate()) { link = back });
            return node;
        }

        public string GetUniqueLoadID()
        {
            return "Recruitment_CommTarget";
        }

        public void ExposeData()
        {
        }
    }
}
