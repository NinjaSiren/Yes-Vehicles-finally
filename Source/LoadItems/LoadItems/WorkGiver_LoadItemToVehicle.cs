using RimWorld;

using System.IO;
using Verse;
using Verse.AI;
using O21VehicleFramework;
using System.Linq;
using System.Collections.Generic;

namespace LoadItems
{
    public class WorkGiver_LoadItemToVehicle : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get 
            {
                return ThingRequest.ForGroup(ThingRequestGroup.Pawn);
            }
        }  

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            bool flag = pawn != null && pawn.Faction == Faction.OfPlayer && !pawn.def.HasComp(typeof(CompVehicle));
            if (flag)
            {
                var comp = pawn.Map.GetComponent<LoadItemsToVehicle>();
                if (comp != null)
                {
                    if (comp.itemsToBeLoaded != null)
                    {
                        if (comp.itemsToBeLoaded.Count > 0)
                        {
                            foreach (KeyValuePair<Thing, Pawn> entry in comp.itemsToBeLoaded)
                            {
                                LocalTargetInfo target = entry.Key;
                                if (pawn.CanReserveAndReach(target, PathEndMode.ClosestTouch, Danger.Deadly, 10, 1, null, forced))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }
    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            foreach (KeyValuePair<Thing, Pawn> entry in pawn.Map.GetComponent<LoadItemsToVehicle>().itemsToBeLoaded)
            {
                LocalTargetInfo target = entry.Key;
                if (pawn.CanReserveAndReach(target, PathEndMode.ClosestTouch, Danger.Deadly, 10, 1, null, forced))
                {
                    return new Job(DefDatabase<JobDef>.GetNamed("LoadItemToVehicle"), entry.Key, entry.Value);
                }
            }
            return new Job(JobDefOf.Goto, pawn.Position.x + 1);
        }

    }
}