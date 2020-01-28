using System;

using System.IO;
using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace LoadItems
{
    public class LoadItemsToVehicle : MapComponent
    {
        public LoadItemsToVehicle(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look<Thing, Pawn>(ref this.itemsToBeLoaded, "itemsToBeLoaded", LookMode.Reference, LookMode.Reference);

            //Using Scribe_Values with a Thing reference li. Use Scribe_References or Scribe_Deep instead.

        }

        public Dictionary<Thing, Pawn> itemsToBeLoaded = new Dictionary<Thing, Pawn>();
    }
}
