using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using RimWorld;
using Verse;

namespace O21VehicleFramework
{
    [Flags]
    public enum HandlingTypeFlags
    {
        None = 0,
        Movement = 1,
        Manipulation = 2,
        Weapons = 4
    }
}