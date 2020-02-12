using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using RimWorld;
using Verse;

namespace O21VehicleFramework
{
    public class DefModExt_RenderWeaponExt : DefModExtension
    {
        public bool alwaysRender = false;

        public bool renderOverVehicle = false;

        public bool weaponCardinalDirection = false;

        public Vector2 renderPosOffset = new Vector2(0, 0);
    }
}
