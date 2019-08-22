using Vehicle_Lights;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Vehicle_Lights
{
    public class VehicleLights : Pawn
    {
        public enum LightMode
        {
            Automatic,
            ForcedOn,
            ForcedOff
        }

        public const int updatePeriodInTicks = 60;

        public int nextUpdateTick;

        public Thing light;

        public bool lightIsOn;

        public LightMode lightMode;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            this.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                nextUpdateTick = Find.get_TickManager().get_TicksGame() + Rand.Range(0, 60);
            }
        }

        public override void ExposeData()
        {
            this.ExposeData();
            Scribe_References.Look<Thing>(ref light, "light", false);
            Scribe_Values.Look<bool>(ref lightIsOn, "lightIsOn", false, false);
            Scribe_Values.Look<LightMode>(ref lightMode, "lightMode", LightMode.Automatic, false);
        }

        public override void Tick()
        {
            this.Tick();
            if (lightIsOn || Find.get_TickManager().get_TicksGame() >= nextUpdateTick)
            {
                nextUpdateTick = Find.get_TickManager().get_TicksGame() + 60;
                RefreshLightState();
            }
        }

        public void RefreshLightState()
        {
            if (ComputeLightState())
            {
                SwitchOnLight();
            }
            else
            {
                SwitchOffLight();
            }
        }

        public bool ComputeLightState()
        {
            if (this.get_Wearer() == null || this.get_Wearer().get_Dead() || this.get_Wearer().get_Downed() || !RestUtility.Awake(this.get_Wearer()))
            {
                return false;
            }
            if (lightMode == LightMode.ForcedOn)
            {
                return true;
            }
            if (lightMode == LightMode.ForcedOff)
            {
                return false;
            }
            if (this.get_Wearer().get_Map() != null && ((GridsUtility.Roofed(this.get_Wearer().get_Position(), this.get_Wearer().get_Map()) && (int)this.get_Wearer().get_Map().glowGrid.PsychGlowAt(this.get_Wearer().get_Position()) <= 1) || (!GridsUtility.Roofed(this.get_Wearer().get_Position(), this.get_Wearer().get_Map()) && (int)this.get_Wearer().get_Map().glowGrid.PsychGlowAt(this.get_Wearer().get_Position()) < 2)))
            {
                return true;
            }
            return false;
        }

        public void SwitchOnLight()
        {
            IntVec3 val = IntVec3Utility.ToIntVec3(this.get_Wearer().get_DrawPos());
            if (!ThingUtility.DestroyedOrNull(light) && val != light.get_Position())
            {
                SwitchOffLight();
            }
            if (ThingUtility.DestroyedOrNull(light) && GridsUtility.GetFirstThing(val, this.get_Wearer().get_Map(), Util_VehicleLights.MiningLightDef) == null)
            {
                light = GenSpawn.Spawn(Util_VehicleLights.VehicleLightDef, val, this.get_Wearer().get_Map(), 0);
            }
            lightIsOn = true;
        }

        public void SwitchOffLight()
        {
            if (!ThingUtility.DestroyedOrNull(light))
            {
                light.Destroy(0);
                light = null;
            }
            lightIsOn = false;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            IEnumerable<Gizmo> wornGizmos = this.GetWornGizmos();
            IEnumerable<Gizmo> gizmos = this.GetGizmos();
            if (gizmos != null)
            {
                return gizmos.Concat(wornGizmos);
            }
            return wornGizmos;
        }

        public override IEnumerable<Gizmo> GetWornGizmos()
        {
            IList<Gizmo> list = new List<Gizmo>();
            int num = 700000101;
            Command_Action val = new Command_Action();
            switch (lightMode)
            {
                case LightMode.Automatic:
                    val.icon = ContentFinder<Texture2D>.Get("Ui/Commands/CommandButton_LigthModeAutomatic", true);
                    val.defaultLabel = "Ligth: automatic.";
                    break;
                case LightMode.ForcedOn:
                    val.icon = ContentFinder<Texture2D>.Get("Ui/Commands/CommandButton_LigthModeForcedOn", true);
                    val.defaultLabel = "Ligth: on.";
                    break;
                case LightMode.ForcedOff:
                    val.icon = ContentFinder<Texture2D>.Get("Ui/Commands/CommandButton_LigthModeForcedOff", true);
                    val.defaultLabel = "Ligth: off.";
                    break;
            }
            val.defaultDesc = "Switch mode.";
            val.activateSound = SoundDef.Named("Click");
            val.action = SwitchLigthMode;
            val.groupKey = num + 1;
            list.Add(val);
            return list;
        }

        public void SwitchLigthMode()
        {
            switch (lightMode)
            {
                case LightMode.Automatic:
                    lightMode = LightMode.ForcedOn;
                    break;
                case LightMode.ForcedOn:
                    lightMode = LightMode.ForcedOff;
                    break;
                case LightMode.ForcedOff:
                    lightMode = LightMode.Automatic;
                    break;
            }
            RefreshLightState();
        }

        public VehicleLights()
        {
        }
    }
}
