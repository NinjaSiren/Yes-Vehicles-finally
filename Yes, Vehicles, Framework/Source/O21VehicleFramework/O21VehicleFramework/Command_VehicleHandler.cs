using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using RimWorld;
using Verse;

namespace O21VehicleFramework
{
    public class Command_VehicleHandler : Command
    {
        public Action action;

        public override float GetWidth(float maxWidth) => NewWidth(maxWidth);

        public float NewWidth(float maxWidth)
        {
            return base.GetWidth(maxWidth) * 0.75f;
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            action();
        }
    }
}