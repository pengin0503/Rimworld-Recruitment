using System.Linq;
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
        /// <summary>The methods started from a comms console (radio, orbital), in menu order.</summary>
        public static IOrderedEnumerable<RecruitmentMethodDef> CommsMethods => RecruitmentMethodDef.AllInOrder.Where(m => m.startKind == RecruitmentStartKind.CommsConsole).OrderBy(m => m.uiOrder);

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
                node.options.Add(MethodOption(method, negotiator, component.CanStart(method, negotiator.Map, needsOriginLottery: true), faction: null));
            }
            node.options.Add(new DiaOption("Recruitment_Comms_HangUp".Translate()) { resolveTree = true });
            return node;
        }

        /// <summary>
        /// One comms method in a negotiation dialog. Choosing it closes the call and opens the start dialog, which
        /// shows the cost and the premium and serves as the confirmation. <paramref name="report"/> is checked
        /// without a premium, so a disabled option means even the plain method is unavailable.
        /// </summary>
        public static DiaOption MethodOption(RecruitmentMethodDef method, Pawn negotiator, AcceptanceReport report, Faction faction)
        {
            var option = new DiaOption(RecruitmentFloatMenu.OptionLabel(method));
            if (!report.Accepted)
            {
                option.Disable(report.Reason);
                return option;
            }
            option.action = () => RecruitmentUI.OpenForComms(method, negotiator, faction);
            option.resolveTree = true;
            return option;
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
