using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;

namespace O21VehicleFramework
{
    public class WorkGiver_RepairVehicle : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.InteractionCell;
            }
        }

        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForGroup(ThingRequestGroup.Pawn);
            }
        }

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            Pawn vehicle = thing as Pawn;
            bool flag = vehicle != null && vehicle.def.HasComp(typeof(CompVehicle)) && vehicle.GetComp<CompVehicle>().repairToggle && HasRepairableDamage(vehicle);
            if (flag)
            {
                LocalTargetInfo target = vehicle;
                bool flag2 = pawn.CanReserveAndReach(target, PathEndMode.ClosestTouch, Danger.Deadly, 10, 1, null, forced) && FindRepairParts(pawn, vehicle, forced) != null;
                if (flag2)
                {
                    return true;
                }
            }
            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            Pawn vehicle = thing as Pawn;
            Thing parts = FindRepairParts(pawn, vehicle, forced);
            return new Job(JobDefOf_Vehicle.O21_JobDriver_RepairVehicle, vehicle, parts);
        }

        public static Thing FindRepairParts(Pawn pawn, Pawn vehicle, bool forced)
        {
            Thing result = null;

            Predicate<Thing> predicate = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserveAndReach(x, PathEndMode.ClosestTouch, Danger.Deadly, 10, 1, null, forced);
            IntVec3 position = vehicle.Position;
            Map map = vehicle.Map;
            ThingDef repairItem = vehicle.GetComp<CompVehicle>().Props.repairItem;
            if(repairItem == null)
            {
                repairItem = ThingDefOf.Steel;
            }
            List<Thing> searchSet = map.listerThings.ThingsOfDef(repairItem);
            PathEndMode pathEnd = PathEndMode.ClosestTouch;
            TraverseParms traverseParms = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false);
            Predicate<Thing> validator = predicate;
            result = GenClosest.ClosestThing_Global_Reachable(position, map, searchSet, pathEnd, traverseParms, 9999f, validator, null);

            return result;
        }

        public static bool HasRepairableDamage(Pawn vehicle)
        {
            if(vehicle.health.hediffSet.hediffs.Any(x => x.def.isBad))
            {
                return true;
            }
            return false;
        }
    }
}
