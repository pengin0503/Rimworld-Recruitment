using System;
using Recruitment.Logic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Recruitment
{
    /// <summary>What <see cref="Dialog_StartRecruitment"/> shows and does for one way of starting a drive.</summary>
    public sealed class StartPlan
    {
        public RecruitmentMethodDef method;

        /// <summary>The colonist whose Social skill applies (the one talking, broadcasting or negotiating).</summary>
        public Pawn starter;

        /// <summary>The applicants' faction when it is fixed; null when one is drawn on arrival of the call.</summary>
        public Faction fixedOrigin;

        public string destinationLabel;

        /// <summary>Goodwill the start costs with <see cref="fixedOrigin"/> (direct requests); 0 for none.</summary>
        public int goodwillCost;

        /// <summary>When and from where the silver is taken, e.g. "from the caravan on arrival".</summary>
        public string paymentNote;

        /// <summary>The silver the payment is taken from.</summary>
        public Func<int> availableSilver;

        /// <summary>Whether the drive can start with the given premium tier.</summary>
        public Func<int, AcceptanceReport> check;

        /// <summary>Starts (or sets off to start) the drive with the given premium tier.</summary>
        public Action<int> start;
    }

    /// <summary>
    /// The one dialog every method starts from: what the drive is, what it costs, the silver premium, and how the
    /// starting colonist's Social skill affects the number of applicants. It also serves as the confirmation.
    /// </summary>
    public class Dialog_StartRecruitment : Window
    {
        private const float IconSize = 48f;

        /// <summary>The premium chosen last, offered again next time (per session, not saved).</summary>
        private static int lastTier;

        private readonly StartPlan plan;

        private int tier;

        // Checks count silver and factions; a few times a second is enough while the dialog is open.
        private readonly TimedCache<int, AcceptanceReport> reports = new TimedCache<int, AcceptanceReport>(RecruitmentUI.CacheSeconds, () => RecruitmentUI.Now);

        private readonly TimedCache<int, int> silver = new TimedCache<int, int>(RecruitmentUI.CacheSeconds, () => RecruitmentUI.Now);

        private readonly string chancesText;

        private readonly string socialText;

        // Fits every section plus a wrapped reason line at the default UI scale (content is not scrolled).
        public override Vector2 InitialSize => new Vector2(640f, 720f);

        public Dialog_StartRecruitment(StartPlan plan)
        {
            this.plan = plan;
            tier = PremiumTier.Clamp(lastTier);
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            doCloseX = true;
            int social = RecruitmentUtility.SocialLevel(plan.starter);
            socialText = plan.starter != null
                ? "Recruitment_Start_Starter".Translate(plan.starter.LabelShortCap, social).Resolve()
                : "Recruitment_Start_NoStarter".Translate().Resolve();
            chancesText = RecruitmentUI.ChancesText(RecruitmentMod.Settings.ApplicantChances(social));
        }

        public override void DoWindowContents(Rect inRect)
        {
            RecruitmentMethodDef method = plan.method;
            Rect iconRect = new Rect(inRect.x, inRect.y, IconSize, IconSize);
            GUI.DrawTexture(iconRect, method.Icon);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(iconRect.xMax + 10f, inRect.y + 8f, inRect.width - IconSize - 10f, 35f), method.LabelCap);
            Text.Font = GameFont.Small;

            var listing = new Listing_Standard();
            listing.Begin(new Rect(inRect.x, inRect.y + IconSize + 8f, inRect.width, inRect.height - IconSize - 8f - 45f));

            FloatRange days = method.Days;
            string origin = plan.fixedOrigin != null ? plan.fixedOrigin.Name : "Recruitment_Start_OriginDrawn".Translate().Resolve();
            listing.Label("Recruitment_Start_Origin".Translate(origin));
            listing.Label("Recruitment_Start_Destination".Translate(plan.destinationLabel.NullOrEmpty() ? "Recruitment_History_Unknown".Translate().Resolve() : plan.destinationLabel));
            listing.Label("Recruitment_Start_Arrival".Translate(method.arrivalLabel));
            listing.Label("Recruitment_Start_Days".Translate(days.min.ToString("0.#"), days.max.ToString("0.#")));

            Section(listing, "Recruitment_Start_SocialHeading");
            listing.Label(socialText);
            listing.Label(chancesText);

            Section(listing, "Recruitment_Start_PremiumHeading");
            for (int i = 0; i < PremiumTier.Count; i++)
            {
                string label = "Recruitment_Start_PremiumOption".Translate(("Recruitment_Premium_" + TierKey(i)).Translate(), PremiumTier.ExtraPercent(i), PremiumTier.ExtraCost(method.Cost, i), PremiumTier.Candidates(i));
                if (listing.RadioButton(label, tier == i, 0f, i == 0 ? null : "Recruitment_Start_PremiumDesc".Translate().Resolve()))
                {
                    tier = i;
                }
            }

            Section(listing, "Recruitment_Start_CostHeading");
            int baseCost = method.Cost;
            int extra = PremiumTier.ExtraCost(baseCost, tier);
            int total = PremiumTier.TotalCost(baseCost, tier);
            int available = silver.Get(0, plan.availableSilver);
            listing.Label("Recruitment_Start_CostLine".Translate(baseCost, extra, total));
            if (plan.goodwillCost > 0 && plan.fixedOrigin != null)
            {
                int goodwill = plan.fixedOrigin.PlayerGoodwill;
                listing.Label("Recruitment_Start_GoodwillLine".Translate(plan.goodwillCost, goodwill, DirectRequestRule.GoodwillAfter(goodwill, plan.goodwillCost)));
            }
            string remaining = available >= total ? (available - total).ToString() : "Recruitment_Start_NotEnough".Translate().Resolve().Colorize(ColorLibrary.RedReadable);
            listing.Label("Recruitment_Start_SilverLine".Translate(available, remaining));
            if (!plan.paymentNote.NullOrEmpty())
            {
                listing.Label(plan.paymentNote);
            }
            listing.Label("Recruitment_Start_NoRefund".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor));

            AcceptanceReport report = reports.Get(tier, () => plan.check(tier));
            if (!report.Accepted && !report.Reason.NullOrEmpty())
            {
                listing.Gap(6f);
                listing.Label(report.Reason.Colorize(ColorLibrary.RedReadable));
            }
            listing.End();

            Rect buttons = new Rect(inRect.x, inRect.yMax - 38f, inRect.width, 38f);
            if (Widgets.ButtonText(buttons.LeftPart(0.48f), "Recruitment_Start_Start".Translate(), active: report.Accepted))
            {
                // Re-check right away rather than trusting a cached result.
                AcceptanceReport now = plan.check(tier);
                if (now.Accepted)
                {
                    lastTier = tier;
                    Close();
                    plan.start(tier);
                }
                else if (!now.Reason.NullOrEmpty())
                {
                    Messages.Message(now.Reason, MessageTypeDefOf.RejectInput, historical: false);
                }
            }
            if (Widgets.ButtonText(buttons.RightPart(0.48f), "Cancel".Translate()))
            {
                Close();
            }
        }

        public static string TierKey(int tier)
        {
            switch (PremiumTier.Clamp(tier))
            {
                case 1:
                    return "Some";
                case 2:
                    return "More";
                case 3:
                    return "Max";
                default:
                    return "None";
            }
        }

        private static void Section(Listing_Standard listing, string key)
        {
            listing.Gap(8f);
            listing.Label(key.Translate().Resolve().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine(4f);
        }
    }
}
