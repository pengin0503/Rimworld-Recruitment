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
