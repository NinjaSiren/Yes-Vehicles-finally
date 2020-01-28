using System;

using System.IO;
using Harmony;
using UnityEngine;
using Verse;

namespace LoadItems
{
    public class Startup : Mod
    {
        public Startup(ModContentPack content) : base(content)
        {
            Startup.HarmonyInstance = HarmonyInstance.Create("YesVehicle.LoadItems");
        }

        public static HarmonyInstance HarmonyInstance;
    }
}
