using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Recruitment
{
    /// <summary>Adds the recruitment broadcast to every comms console's list of contacts.</summary>
    [HarmonyPatch(typeof(Building_CommsConsole), nameof(Building_CommsConsole.GetCommTargets))]
    internal static class Patch_CommsConsole_GetCommTargets
    {
        private static void Postfix(ref IEnumerable<ICommunicable> __result)
        {
            RecruitmentCommTarget target = GameComponent_Recruitment.Instance?.CommTarget;
            if (target != null)
            {
                __result = __result.Concat(Gen.YieldSingle<ICommunicable>(target));
            }
        }
    }

    /// <summary>
    /// Adds "ask for settlers" to the first page of a faction's comms dialog, next to the game's own requests
    /// (trade caravans, military aid). It leads to a page that offers each comms method for that faction.
    /// </summary>
    [HarmonyPatch(typeof(FactionDialogMaker), nameof(FactionDialogMaker.FactionDialogFor))]
    internal static class Patch_FactionDialogMaker_FactionDialogFor
    {
        private static void Postfix(DiaNode __result, Pawn negotiator, Faction faction)
        {
            try
            {
                // The game only offers its requests from a colony map; do the same.
                if (__result == null || negotiator?.Map == null || !negotiator.Map.IsPlayerHome || !RecruitmentUtility.IsDirectRequestCandidate(faction))
                {
                    return;
                }
                DiaOption option = DirectRequestOption(negotiator, faction);
                if (option != null)
                {
                    __result.options.Insert(DisconnectIndex(__result), option);
                }
            }
            catch (Exception e)
            {
                // Never break the game's own dialog because of this option.
                Log.ErrorOnce($"[Recruitment] Could not add the settler request to the comms dialog: {e}", 0x52434644);
            }
        }

        private static DiaOption DirectRequestOption(Pawn negotiator, Faction faction)
        {
            GameComponent_Recruitment component = GameComponent_Recruitment.Instance;
            if (component == null)
            {
                return null;
            }
            var option = new DiaOption("Recruitment_Direct_Option".Translate(RecruitmentUtility.DirectGoodwillCost(faction)));
            AcceptanceReport report = component.CanAskFaction(faction);
            if (negotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
            {
                // Same rule and wording as the game's own social requests.
                option.Disable("WorkTypeDisablesOption".Translate(SkillDefOf.Social.label));
                return option;
            }
            if (!report.Accepted)
            {
                option.Disable(report.Reason.NullOrEmpty() ? "Recruitment_Reason_SettlementUnwilling".Translate(faction.Name).Resolve() : report.Reason);
                return option;
            }
            var node = new DiaNode("Recruitment_Direct_Choose".Translate(faction.leader?.LabelShort ?? faction.Name, faction.Name));
            foreach (RecruitmentMethodDef method in RecruitmentCommTarget.CommsMethods)
            {
                node.options.Add(RecruitmentCommTarget.MethodOption(method, negotiator, component.CanRequestDirect(method, negotiator.Map, faction, premiumTier: 0), faction));
            }
            node.options.Add(new DiaOption("GoBack".Translate()) { linkLateBind = () => FactionDialogMaker.FactionDialogFor(negotiator, faction) });
            option.link = node;
            return option;
        }

        /// <summary>The game adds "(Disconnect)" last: an option that only closes the dialog. Insert before it.</summary>
        private static int DisconnectIndex(DiaNode node)
        {
            for (int i = node.options.Count - 1; i >= 0; i--)
            {
                DiaOption o = node.options[i];
                if (o.resolveTree && o.action == null && o.link == null && o.linkLateBind == null)
                {
                    return i;
                }
            }
            return node.options.Count;
        }
    }

    /// <summary>
    /// When a caravan is about to leave for a settlement an envoy could ask, but does not carry the silver the
    /// request costs, ask the player first. Runs before the game's own warnings (food, social skill).
    /// </summary>
    [HarmonyPatch(typeof(Dialog_FormCaravan), "TrySend")]
    internal static class Patch_FormCaravan_TrySend
    {
        private static readonly AccessTools.FieldRef<Dialog_FormCaravan, PlanetTile> DestinationTile = AccessTools.FieldRefAccess<Dialog_FormCaravan, PlanetTile>("destinationTile");

        private static readonly MethodInfo TrySend = AccessTools.Method(typeof(Dialog_FormCaravan), "TrySend");

        /// <summary>Set while re-running TrySend after the player confirmed, so the warning is not shown twice.</summary>
        private static bool confirmed;

        private static bool Prefix(Dialog_FormCaravan __instance)
        {
            if (confirmed)
            {
                return true;
            }
            string warning;
            try
            {
                warning = RecruitmentUtility.DepartureWarning(DestinationTile(__instance), __instance.transferables);
            }
            catch (Exception e)
            {
                // Never block forming a caravan because of this check.
                Log.ErrorOnce($"[Recruitment] Departure check failed: {e}", 0x52434456);
                return true;
            }
            if (warning == null)
            {
                return true;
            }
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(warning, () =>
            {
                confirmed = true;
                try
                {
                    TrySend.Invoke(__instance, null);
                }
                finally
                {
                    confirmed = false;
                }
            }));
            return false;
        }
    }
}
