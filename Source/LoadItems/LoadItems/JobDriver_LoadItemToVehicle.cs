using System.Collections.Generic;

using System.IO;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LoadItems
{
    public class JobDriver_LoadItemToVehicle : JobDriver
    {
        private Thing thing => TargetThingA as Thing;
        private Pawn vehicle => TargetThingB as Pawn;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn pawn = this.pawn;
            LocalTargetInfo target = this.pawn;
            Job job = this.job;
            bool flag = !pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
            if (!flag)
            {
                bool flag2 = pawn.Reserve(this.thing, this.job, 10, 1, null, errorOnFailed);
                if (flag2)
                {
                    return true;
                }
            }
            return false;
        }

        public override string GetReport()
        {
            string text = DefDatabase<JobDef>.GetNamed("LoadItemToVehicle", true).reportString;
            text = text.Replace("TargetA", TargetThingA.def.label);
            text = text.Replace("TargetB", TargetThingB.def.label);
            return text;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil reserve = Toils_Reserve.Reserve(TargetIndex.A, 1, -1, null).FailOnDespawnedOrNull(TargetIndex.A);
            yield return reserve;
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return this.DetermineNumToHaul();
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, true, false);

            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);

            yield return Toils_General.Wait(25, TargetIndex.None);
            yield return this.PlaceTargetInCarrierInventory();
            yield break;
        }

        private Toil DetermineNumToHaul()
        {
            return new Toil
            {
                initAction = delegate ()
                {
                    int num = thing.stackCount;
                    if (this.pawn.carryTracker.CarriedThing != null)
                    {
                        num -= this.pawn.carryTracker.CarriedThing.stackCount;
                    }
                    if (num <= 0)
                    {
                        this.pawn.jobs.EndCurrentJob(JobCondition.Succeeded, true);
                    }
                    else
                    {
                        this.job.count = num;
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant,
                atomicWithPrevious = true
            };
        }

        private Toil PlaceTargetInCarrierInventory()
        {
            return new Toil
            {
                initAction = delegate ()
                {
                    Pawn_CarryTracker carryTracker = this.pawn.carryTracker;
                    Thing carriedThing = carryTracker.CarriedThing;
                    carryTracker.innerContainer.TryTransferToContainer(carriedThing, this.vehicle.inventory.innerContainer, carriedThing.stackCount, true);
                    this.pawn.Map.GetComponent<LoadItemsToVehicle>().itemsToBeLoaded.Remove(carriedThing);
                }
            };
        }
    }
}
