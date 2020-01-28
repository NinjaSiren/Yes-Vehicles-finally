using System;

using System.IO;
using System.Linq;
using System.Reflection;
using Harmony;
using RimWorld;
using Verse;

namespace LoadItems
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            Startup.HarmonyInstance.PatchAll();
        }
    }
}
