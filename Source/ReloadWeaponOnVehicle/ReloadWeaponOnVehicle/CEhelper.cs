using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using Verse.AI;
using CombatExtended;
using O21VehicleFramework;
using UnityEngine;

namespace ReloadWeaponOnVehicle
{
    public class CEhelper
    {
        public CEhelper()
        {

        }

        public CompAmmoUser compReloader(Pawn pawn)
        {
            if (pawn != null)
                return pawn.equipment.Primary.TryGetComp<CompAmmoUser>();
            return null;
        }

        public int getDefaultDuration(Pawn pawn, Pawn vehicle)
        {
            return Mathf.CeilToInt(vehicle.equipment.Primary.TryGetComp<CompAmmoUser>().Props.reloadTime.SecondsToTicks() / pawn.GetStatValue(CE_StatDefOf.ReloadSpeed));
        }
        public bool ammoNeeded(Pawn vehicle, ThingWithComps thingWithComps)
        {
            CompAmmoUser comp = vehicle.equipment.Primary.TryGetComp<CompAmmoUser>();
            if (comp != null)
            {
                if (comp.HasMagazine && !(comp.CurMagCount < comp.Props.magazineSize || comp.SelectedAmmo != comp.CurrentAmmo))
                {
                    return false;
                }
                if (!comp.UseAmmo)
                {
                    return true;
                }
                Thing ammo = GenClosest.ClosestThingReachable(vehicle.Position, vehicle.Map,
                                ThingRequest.ForDef(comp.SelectedAmmo),
                                PathEndMode.ClosestTouch,
                                TraverseParms.For(vehicle, Danger.Deadly, TraverseMode.ByPawn),
                                80,
                                x => !x.IsForbidden(vehicle) && vehicle.CanReserve(x));
                return ammo != null;
            }
            return false;
        }

        public Job returnJob(Thing t, Pawn pawn, Pawn vehicle)
        {
            CompAmmoUser comp = vehicle.equipment.Primary.TryGetComp<CompAmmoUser>();
            if (vehicle == null || comp == null) return null;
            
            if (!comp.UseAmmo)
                
            {
                return new Job(DefDatabase<JobDef>.GetNamed("ReloadWeaponOnVehicle", true), t, null);
            }
            
            // Iterate through all possible ammo types for NPC's to find whichever is //available, /startingwit /currently /selected
            Thing ammo = null;
            var ammoTypes = new List<AmmoDef>();
            try
            {
                foreach (var a in comp.Props.ammoSet.ammoTypes)
                {
                    if (a.ammo != null)
                        ammoTypes.Add(a.ammo);
                }
            }
            catch
            {

            }            
            var index = ammoTypes.IndexOf(comp.SelectedAmmo);
            for (int i = 0; i < ammoTypes.Count; i++)
            {
                index = (index + i) % ammoTypes.Count;
                ammo = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
                                ThingRequest.ForDef(ammoTypes[index]),
                                PathEndMode.ClosestTouch,
                                TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn),
                                80,
                                x => !x.IsForbidden(pawn) && pawn.CanReserve(x));
                if (ammo != null || pawn.Faction == Faction.OfPlayer)
                    break;
            }
            if (ammo == null)
                return null;
            
            // Update selected ammo if necessary
            if (ammo.def != comp.SelectedAmmo)
                comp.SelectedAmmo = ammo.def as AmmoDef;
            
            // Create the actual job
            int amountNeeded = comp.Props.magazineSize;
            if (comp.CurrentAmmo == comp.SelectedAmmo) amountNeeded -= comp.CurMagCount;
            return new Job(DefDatabase<JobDef>.GetNamed("ReloadWeaponOnVehicle"), t, ammo) { count = Mathf.Min(amountNeeded, ammo.stackCount) };
        }
        public Thing getAmmo(Pawn pawn, List<AmmoDef> ammoTypes, int index)
        {
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
                                    ThingRequest.ForDef(ammoTypes[index]),
                                    PathEndMode.ClosestTouch,
                                    TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn),
                                    80,
                                    x => !x.IsForbidden(pawn) && pawn.CanReserve(x));
        }

        public int getAmount(ThingWithComps thingWithComps)
        {
            CompAmmoUser comp = thingWithComps.TryGetComp<CompAmmoUser>();
            int amountNeeded = comp.Props.magazineSize;
            if (comp.CurrentAmmo == comp.SelectedAmmo) amountNeeded -= comp.CurMagCount;
            return amountNeeded;
        }

        public bool UseAmmo(ThingWithComps thingWithComps)
        {
            CompAmmoUser comp = thingWithComps.TryGetComp<CompAmmoUser>();
            if (comp.UseAmmo)
            {
                return true;
            }
            return false;
        }

    }
}