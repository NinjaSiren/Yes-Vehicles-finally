using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using RimWorld;
using Verse;

namespace O21VehicleFramework
{
    [StaticConstructorOnStartup]
    public static class TexCommandVehicle
    {
        public static readonly Texture2D Refuel = ContentFinder<Texture2D>.Get("UI/Commands/O21Refuel", true);
        public static readonly Texture2D Repair = ContentFinder<Texture2D>.Get("UI/Commands/O21Repair", true);
    }
}
