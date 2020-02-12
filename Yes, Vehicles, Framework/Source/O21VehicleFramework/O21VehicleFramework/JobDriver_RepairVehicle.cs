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
    public class JobDriver_RepairVehicle : JobDriver
    {
        protected Pawn Deliveree => (Pawn)this.job.targetA.Thing;

        protected Thing PartUsed => this.job.targetB.Thing;

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

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnAggroMentalState(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            Toil reserveParts = null;
            reserveParts = ReserveParts(TargetIndex.B).FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return reserveParts;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return PickupParts(TargetIndex.B).FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveParts, TargetIndex.B, TargetIndex.None, true, null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            int duration = (int)(1f / this.pawn.GetStatValue(StatDefOf.ConstructionSpeed, true) * 600f);
            EffecterDef effect = DefDatabase<EffecterDef>.AllDefs.FirstOrDefault((EffecterDef ed) => ed.defName == "Repair");
            yield return Toils_General.Wait(duration, TargetIndex.None).FailOnCannotTouch(TargetIndex.A, PathEndMode.ClosestTouch).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f).WithEffect(effect, TargetIndex.A);
            yield return FinalizeRepairs(this.Deliveree, reserveParts);
            yield break;
        }

        public static Toil ReserveParts(TargetIndex ind)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                Thing thing = curJob.GetTarget(ind).Thing;
                int num = actor.Map.reservationManager.CanReserveStack(actor, thing, 10, null, false);
                bool flag = num <= 0 || !actor.Reserve(thing, curJob, 10, 1, null, true);
                if (flag)
                {
                    toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable, true);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.atomicWithPrevious = true;
            return toil;
        }

        public static Toil PickupParts(TargetIndex ind)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                Thing thing = curJob.GetTarget(ind).Thing;
                int num = 1;
                bool flag = actor.carryTracker.CarriedThing != null;
                if (flag)
                {
                    num -= actor.carryTracker.CarriedThing.stackCount;
                }
                int num2 = Mathf.Min(actor.Map.reservationManager.CanReserveStack(actor, thing, 10, null, false), num);
                bool flag2 = num2 > 0;
                if (flag2)
                {
                    actor.carryTracker.TryStartCarry(thing, num2, true);
                }
                curJob.count = num - num2;
                bool spawned = thing.Spawned;
                if (spawned)
                {
                    toil.actor.Map.reservationManager.Release(thing, actor, curJob);
                }
                curJob.SetTarget(ind, actor.carryTracker.CarriedThing);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private static Toil FinalizeRepairs(Pawn vehicle, Toil jumpToIfFailed)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                float num = (vehicle.kindDef.combatPower <= 10000f) ? vehicle.kindDef.combatPower : 300f;
                float statValue = actor.GetStatValue(StatDefOf.ConstructSuccessChance, true);
                bool flag = Rand.Chance(statValue);
                if (flag)
                {
                    actor.skills.Learn(SkillDefOf.Construction, num * 0.5f, false);
                    actor.skills.Learn(SkillDefOf.Intellectual, num * 0.5f, false);
                    List<BodyPartRecord> damage = GetPartsToApplyOn(vehicle).ToList();
                    if(damage != null)
                    {
                        vehicle.health.RestorePart(damage.RandomElement());
                        Thing thing = actor.CurJob.targetB.Thing;
                        bool flag2 = !thing.Destroyed;
                        if (flag2)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                        }
                    }
                }
                else
                {
                    actor.skills.Learn(SkillDefOf.Construction, num * 0.25f, false);
                    actor.skills.Learn(SkillDefOf.Intellectual, num * 0.25f, false);
                    MoteMaker.ThrowText((actor.DrawPos + vehicle.DrawPos) / 2, actor.Map, "Repair Failed" + statValue.ToStringPercent(), 8f);
                    Thing thing = actor.CurJob.targetB.Thing;
                    bool flag3 = !thing.Destroyed;
                    if (flag3)
                    {
                        thing.Destroy(DestroyMode.Vanish);
                    }
                    actor.jobs.curDriver.JumpToToil(jumpToIfFailed);
                }
            };
            return toil;
        }

        public static IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn)
        {
            var records = new List<BodyPartRecord>();

            var brokenParts = pawn.health.hediffSet.hediffs.FindAll(x => x is Hediff_Injury);
            if (brokenParts != null && brokenParts.Count > 0)
            {
                foreach (var brokenPart in brokenParts)
                {
                    if (brokenPart.Part != null)
                    {
                        if (!records.Contains(brokenPart.Part))
                        {
                            records.Add(brokenPart.Part);
                        }
                    }
                }
            }
            return records.AsEnumerable();
        }
    }
}
