using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using RimWorld;
using Verse;

namespace O21VehicleFramework
{
    public class CompProperties_VehicleSpawner : CompProperties
    {
        public float assemblyTime = 20f; //In seconds
        public string useVerb = "Assemble {0}";
        public PawnKindDef vehicleToSpawn = null;
        public EffecterDef workEffect = null;

        public CompProperties_VehicleSpawner()
        {
            compClass = typeof(CompVehicleSpawner);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
            if(workEffect == null)
            {
                workEffect = EffecterDefOf.ConstructMetal;
            }
        }
    }
}