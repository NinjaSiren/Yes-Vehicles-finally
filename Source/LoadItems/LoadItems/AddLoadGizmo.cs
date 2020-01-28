using System;

using System.IO;
using System.Collections.Generic;
using Harmony;
using RimWorld;
using Verse;
using O21VehicleFramework;
using UnityEngine;

namespace LoadItems
{
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_GetGizmos
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            bool flag = __instance != null && __instance.def.HasComp(typeof(CompVehicle));
            if (flag)
            {
                Command_Action command_Action = new Command_Action
                {
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/LoadTransporter", true),
                    defaultLabel = "LoadItems".Translate(),
                    defaultDesc = "LoadItemsToVehicle".Translate(),
                    activateSound = SoundDef.Named("Click"),
                    action = delegate ()
                    {
                        Find.WindowStack.Add(new Dialog_LoadItemsToVehicle(__instance));
                    }

                };
                __result = CollectionExtensions.Add<Gizmo>(__result, command_Action);
            }
        }
    }
}