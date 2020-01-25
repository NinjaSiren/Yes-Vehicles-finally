using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;


namespace ReloadWeaponOnVehicle
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            if (ModLister.HasActiveModWithName("Combat Extended"))
            {
                helper = new CEhelper();
                CombatExtendedLoaded = true;
                Log.Message("[Yes, Vehicles, Finally!] Combat Extended found. The reload feature is enabled for vehicles.");
            }
        }
        public static CEhelper helper;
        public static bool CombatExtendedLoaded;
    }
}