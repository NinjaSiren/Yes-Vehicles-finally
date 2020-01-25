using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace ReloadWeaponOnVehicle
{
    public class JobDriver_ReloadWeaponOnVehicle : JobDriver
    {
        #region Fields
        protected Pawn Deliveree => (Pawn)this.job.targetA.Thing;
        protected Thing PartUsed => this.job.targetB.Thing;

        #endregion Fields

        #region Properties
        private string errorBase => this.GetType().Assembly.GetName().Name + " :: " + this.GetType().Name + " :: ";

        private Pawn vehicle => TargetThingA as Pawn;
        private ThingWithComps ammo => TargetThingB as ThingWithComps;


        #endregion
         
        #region Methods

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn pawn = this.pawn;
            LocalTargetInfo target = this.Deliveree;
            Job job = this.job;
            bool flag = !pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
            if (!flag)
            {
                int num = this.pawn.Map.reservationManager.CanReserveStack(this.pawn, this.PartUsed, 10, null, false);

                bool flag2 = pawn.Reserve(this.PartUsed, this.job, 10, 1, null, errorOnFailed);
                if (flag2)
                {
                    return true;
                }
            }
            return false;
        }

        public override string GetReport()
        {
            string text = DefDatabase<JobDef>.GetNamed("ReloadWeaponOnVehicle", true).reportString;
            string vehicleType = vehicle.def.label.Translate();
            text = text.Replace("TurretType", vehicleType);
            text = text.Replace("TargetA", TargetThingA.def.label);
            if (Startup.helper.compReloader(vehicle).UseAmmo)
                text = text.Replace("TargetB", TargetThingB.def.label);
            else
                text = text.Replace("TargetB", "CE_ReloadingGenericAmmo".Translate());
            return text;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Error checking/input validation.
            if (vehicle == null)
            {
                Log.Error(string.Concat(errorBase, "TargetThingA isn't a vehicle"));
                yield return null;
            }
           	if (Startup.helper.compReloader(vehicle) == null)
            {
                Log.Error(string.Concat(errorBase, "TargetThingA (vehicle) is missing it's CompAmmoUser."));
                yield return null;
            }
            if (Startup.helper.compReloader(vehicle).UseAmmo && ammo == null)
            {
                Log.Error(string.Concat(errorBase, "TargetThingB is either null or not an AmmoThing."));
                yield return null;
            }

            // Set fail condition on turret.
            if (pawn.Faction != Faction.OfPlayer)
                this.FailOnDestroyedOrNull(TargetIndex.A);
            else
                this.FailOnDestroyedNullOrForbidden(TargetIndex.A);

            if (Startup.helper.compReloader(vehicle).UseAmmo)
            {
                // Perform ammo system specific activities, failure condition and hauling
                if (pawn.Faction != Faction.OfPlayer)
                {
                    ammo.SetForbidden(false, false);
                    this.FailOnDestroyedOrNull(TargetIndex.B);
                }
                else
                {
                    this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
                }

                // Haul ammo
                yield return Toils_Reserve.Reserve(TargetIndex.B, 1);
                yield return Toils_Goto.GotoCell(ammo.Position, PathEndMode.Touch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B);
                yield return Toils_Goto.GotoCell(vehicle.Position, PathEndMode.Touch);
                //yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.A, null, false);
            } else
            {
                // If ammo system is turned off we just need to go to the turret.
                yield return Toils_Goto.GotoCell(vehicle.Position, PathEndMode.Touch);
            }

            // Wait in place
            Toil waitToil = new Toil() { actor = pawn };
            waitToil.initAction = delegate
            {
                // Initial relaod process activities.
                //vehicle.isReloading = true;
                waitToil.actor.pather.StopDead();
                if (Startup.helper.compReloader(vehicle).ShouldThrowMote)
                    MoteMaker.ThrowText(vehicle.Position.ToVector3Shifted(), vehicle.Map, string.Format("CE_ReloadingVehicleMote".Translate(), TargetThingA.LabelCapNoCount));
                Thing newAmmo;
                Startup.helper.compReloader(vehicle).TryUnload(out newAmmo);
                if (newAmmo?.CanStackWith(ammo) ?? false)
                    pawn.carryTracker.TryStartCarry(newAmmo, Mathf.Min(newAmmo.stackCount, Startup.helper.compReloader(vehicle).Props.magazineSize - ammo.stackCount));
            };
            waitToil.defaultCompleteMode = ToilCompleteMode.Delay;
            waitToil.defaultDuration = Startup.helper.getDefaultDuration(pawn, vehicle);

            yield return waitToil.WithProgressBarToilDelay(TargetIndex.A);

            //Actual reloader
            Toil reloadToil = new Toil();
            reloadToil.defaultCompleteMode = ToilCompleteMode.Instant;
            reloadToil.initAction = delegate
            {
                Startup.helper.compReloader(vehicle).LoadAmmo(ammo);
                //vehicle.isReloading = false;
            };
            yield return reloadToil;
        }
        #endregion Methods
    }
}
