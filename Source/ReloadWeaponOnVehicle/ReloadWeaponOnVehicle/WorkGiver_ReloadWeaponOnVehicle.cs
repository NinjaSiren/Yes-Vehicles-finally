using RimWorld;
using Verse;
using Verse.AI;
using O21VehicleFramework;

namespace ReloadWeaponOnVehicle
{
    public class WorkGiver_ReloadWeaponOnVehicle : WorkGiver_Scanner
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
            Pawn vehicle = t as Pawn;
            bool flag = vehicle != null && vehicle.def.HasComp(typeof(CompVehicle));
            if (flag)
            {
                LocalTargetInfo target = vehicle;
                bool flag2 = pawn.CanReserveAndReach(target, PathEndMode.ClosestTouch, Danger.Deadly, 10, 1, null, forced);
                if (flag2)
                {
                    if (Startup.CombatExtendedLoaded)
                    {
                        if (Startup.helper.ammoNeeded(vehicle, vehicle.equipment.Primary) && !vehicle.IsForbidden(pawn.Faction))
                        {
                            return true;
                        }
                      }
                }
            }
            return false;
        }


    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn vehicle = t as Pawn;
            bool flag = vehicle != null && vehicle.def.HasComp(typeof(CompVehicle));
            if (flag)
            {
                if (Startup.CombatExtendedLoaded)
                {
                        return Startup.helper.returnJob(t, pawn, vehicle);
                }
            }
            return null;
        }
    }
}