using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using RimWorld;
using Verse;

namespace O21VehicleFramework
{
    [DefOf]
    public static class JobDefOf_Vehicle
    {
        static JobDefOf_Vehicle()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(JobDefOf_Vehicle));
        }
        
        public static JobDef O21_JobDriver_RepairVehicle;
    }
}
