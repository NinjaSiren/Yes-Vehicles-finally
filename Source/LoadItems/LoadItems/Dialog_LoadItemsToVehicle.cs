using System;

using System.IO;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;
using RimWorld.Planet;
using System.Text;
using Verse.Sound;
using O21VehicleFramework;
using RimWorld;
using System.Linq;

namespace LoadItems
{
    public class Dialog_LoadItemsToVehicle : Window
    {
        public Dialog_LoadItemsToVehicle(Pawn vehicle)
        {
            this.vehicle = vehicle;
            this.map = this.vehicle.Map;
            this.onClosed = onClosed;
            this.mapAboutToBeRemoved = mapAboutToBeRemoved;
            this.canChooseRoute = (!reform || !map.retainedCaravanData.HasDestinationTile);
            this.closeOnAccept = !reform;
            this.closeOnCancel = !reform;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(1024f, (float)UI.screenHeight);
            }
        }
        protected override float Margin
        {
            get
            {
                return 0f;
            }
        }

        private float MassCapacity
        {
            get
            {
                return this.vehicle.GetComp<CompVehicle>().Props.cargoCapacity; ;
            }
        }

        private int MaxPassengerSeats
        {
            get
            {
                return this.vehicle.GetComp<CompVehicle>().Props.roles.Where(r => r.label.ToLower() == "passenger").Select(m => m.slots).FirstOrDefault();
            }
        }

        private int CurrentPassengerCount
        {
            get
            {
                return (from x in this.transferables
                        where x.AnyThing.def.race != null && x.AnyThing.def.race.Humanlike && x.CountToTransfer > 0
                        select x).Count<TransferableOneWay>();
            }
        }

        private string TransportersLabel
        {
            get
            {
                return this.vehicle.Label;
            }
        }
        private string TransportersLabelCap
        {
            get
            {
                return this.vehicle.Label.CapitalizeFirst();
            }
        }

        private float MassUsage
        {
            get
            {
                bool flag = this.massUsageDirty;
                if (flag)
                {
                    this.massUsageDirty = false;
                    //float one = 0;
                    //try
                    //{
                    //    float one = this.vehicle.GetDirectlyHeldThings().Sum((Thing c) => (float)c.stackCount /* /c.def.BaseMass);
                    //}
                    //catch
                    //{
                    //    foreach (var item in this.vehicle.GetDirectlyHeldThings())
                    //    {
                    //        Log.Message(item.def.defName + " - " + item.stackCount.ToString());
                    //    }
                    //    
                    //    return CollectionsMassCalculator.MassUsageTransferables(this.transferables, //this.IgnoreInventoryMode, true, false);
                    //}
                    this.cachedMassUsage = CollectionsMassCalculator.MassUsageTransferables(this.transferables, this.IgnoreInventoryMode, true, false);

                }
                return this.cachedMassUsage;
            }
        }

        private bool ShowCancelButton
        {
            get
            {
                if (!this.mapAboutToBeRemoved)
                {
                    return true;
                }
                bool flag = false;
                for (int i = 0; i < this.transferables.Count; i++)
                {
                    Pawn pawn = this.transferables[i].AnyThing as Pawn;
                    if (pawn != null && pawn.IsColonist && !pawn.Downed)
                    {
                        flag = true;
                        break;
                    }
                }
                return !flag;
            }
        }
        private IgnorePawnsInventoryMode IgnoreInventoryMode
        {
            get
            {
                return  IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload;
            }
        }

        public bool AutoStripSpawnedCorpses { get; private set; }

        public override void PostOpen()
        {
            base.PostOpen();
            this.choosingRoute = false;
            if (!this.thisWindowInstanceEverOpened)
            {
                this.thisWindowInstanceEverOpened = true;
                this.CalculateAndRecacheTransferables();
                PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.FormCaravan, KnowledgeAmount.Total);
            }
        }
        public override void PostClose()
        {
            base.PostClose();
            if (this.onClosed != null && !this.choosingRoute)
            {
                this.onClosed();
            }
        }
        public override void DoWindowContents(Rect inRect)
        {
            Rect rect = new Rect(0f, 0f, inRect.width, 35f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "LoadToVehicle".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            float num = this.lastMassFlashTime;
            Rect rect2 = new Rect(12f, 35f, inRect.width - 24f, 40f);

            Dialog_LoadItemsToVehicle.tabsList.Clear();
            //Dialog_LoadItemsToVehicle.tabsList.Add(new TabRecord("PawnsTab".Translate(), delegate ()
            //{
            //    this.tab = Dialog_LoadItemsToVehicle.Tab.Pawns;
            //}, this.tab == Dialog_LoadItemsToVehicle.Tab.Pawns));
            Dialog_LoadItemsToVehicle.tabsList.Add(new TabRecord("ItemsTab".Translate(), delegate ()
            {
                this.tab = Dialog_LoadItemsToVehicle.Tab.Items;
            }, this.tab == Dialog_LoadItemsToVehicle.Tab.Items));
            inRect.yMin += 119f;
            Widgets.DrawMenuSection(inRect);
            TabDrawer.DrawTabs(inRect, Dialog_LoadItemsToVehicle.tabsList, 200f);
            Dialog_LoadItemsToVehicle.tabsList.Clear();
            inRect = inRect.ContractedBy(17f);
            inRect.height += 17f;
            GUI.BeginGroup(inRect);
            Rect rect3 = inRect.AtZero();
            this.DoBottomButtons(rect3);
            Rect inRect2 = rect3;
            inRect2.yMax -= 76f;
            bool flag = false;

            Dialog_LoadItemsToVehicle.Tab tab = Dialog_LoadItemsToVehicle.Tab.Items;
            if (tab == Dialog_LoadItemsToVehicle.Tab.Items)
            {
                this.itemsTransfer.OnGUI(inRect2, out flag);                
            }
            //else if (tab == Dialog_LoadItemsToVehicle.Tab.Pawns)
            //{
            //    this.pawnsTransfer.OnGUI(inRect2, out flag);
            //}
            if (flag)
            {
                this.CountToTransferChanged();
            }
            GUI.EndGroup();
        }


        private void CalculateAndRecacheTransferables()
        {
            this.transferables = new List<TransferableOneWay>();
            this.AddItemsToTransferables();
            IEnumerable<TransferableOneWay> enumerable = null;
            string text = null;
            string text2 = null;
            string text3 = "FormCaravanColonyThingCountTip".Translate();
            bool flag = true;
            IgnorePawnsInventoryMode ignorePawnsInventoryMode = (IgnorePawnsInventoryMode)1;
            bool flag2 = true;
            Func<float> func = () => this.MassCapacity - this.MassUsage;
            int tile = this.map.Tile;
            this.pawnsTransfer = new TransferableOneWayWidget(enumerable, text, text2, text3, flag, ignorePawnsInventoryMode, flag2, func, 0f, false, tile, true, true, true, false, true, false, false);
            CaravanUIUtility.AddPawnsSections(this.pawnsTransfer, this.transferables);
            enumerable = from x in this.transferables
                         where x.ThingDef.category != ThingCategory.Pawn
                         select x;
            text3 = null;
            text2 = null;
            text = "FormCaravanColonyThingCountTip".Translate();
            flag2 = true;
            ignorePawnsInventoryMode = (IgnorePawnsInventoryMode)1;
            flag = true;
            func = (() => this.MassCapacity - this.MassUsage);
            tile = this.map.Tile;
            this.itemsTransfer = new TransferableOneWayWidget(enumerable, text3, text2, text, flag2, ignorePawnsInventoryMode, flag, func, 0f, false, tile, true, false, false, true, false, true, false);
            this.CountToTransferChanged();
        }
        public override bool CausesMessageBackground()
        {
            return true;
        }
 
        private void AddToTransferables(Thing t, bool setToTransferMax = false)
        {
            TransferableOneWay transferableOneWay = TransferableUtility.TransferableMatching<TransferableOneWay>(t, this.transferables, TransferAsOneMode.PodsOrCaravanPacking);
            if (transferableOneWay == null)
            {
                transferableOneWay = new TransferableOneWay();
                this.transferables.Add(transferableOneWay);
            }
            transferableOneWay.things.Add(t);
            if (setToTransferMax)
            {
                transferableOneWay.AdjustTo(transferableOneWay.CountToTransfer + t.stackCount);
            }
        }
        private void DoBottomButtons(Rect rect)
        {
            Rect rect2 = new Rect(rect.width / 2f - this.BottomButtonSize.x / 2f, rect.height - 55f - 17f, this.BottomButtonSize.x, this.BottomButtonSize.y);

            if (Widgets.ButtonText(rect2, "AcceptButton".Translate(), true, false, true))
            {
                    if (this.TryAccept())
                    {
                        SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                        this.Close(false);
                    }
            }
            Rect rect3 = new Rect(rect2.x - 10f - this.BottomButtonSize.x, rect2.y, this.BottomButtonSize.x, this.BottomButtonSize.y);
            if (Widgets.ButtonText(rect3, "ResetButton".Translate(), true, false, true))
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera(null);
                this.CalculateAndRecacheTransferables();
            }
            if (this.ShowCancelButton)
            {
                Rect rect4 = new Rect(rect2.xMax + 10f, rect2.y, this.BottomButtonSize.x, this.BottomButtonSize.y);
                if (Widgets.ButtonText(rect4, "CancelButton".Translate(), true, false, true))
                {
                    this.Close(true);
                }
            }
        }

        private bool TryAccept()
        {
            List<Pawn> onevehicle = new List<Pawn>
                        {
                            this.vehicle
                        };
            this.transferables.RemoveAll((TransferableOneWay x) => x.AnyThing is Pawn);
            for (int k = 0; k < this.transferables.Count; k++)
            {
                if (this.transferables[k].things.Any((Thing x) => !x.Spawned))
                {
                    this.transferables[k].things.SortBy((Thing x) => x.Spawned);
                }
            }
            for (int l = 0; l < this.transferables.Count; l++)
            {
                if (!(this.transferables[l].AnyThing is Corpse))
                {
                    if (this.transferables[l].CountToTransfer > 0)
                    {
                        foreach (Thing thing in this.transferables[l].things)
                        {
                            Thing item = thing.TryMakeMinified();
                            if (!this.vehicle.Map.GetComponent<LoadItemsToVehicle>().itemsToBeLoaded.ContainsKey(item))
                            {
                                this.vehicle.Map.GetComponent<LoadItemsToVehicle>().itemsToBeLoaded.Add(item, this.vehicle);
                            }
                            else
                            {
                                Messages.Message(TranslatorFormattedStringExtensions.Translate("YouAlreadyOrderedToLoadThing", thing.Label), MessageTypeDefOf.NeutralEvent, true);
                            }

                        }
                    }
                }
            }
            for (int m = 0; m < this.transferables.Count; m++)
            {
                if (this.transferables[m].AnyThing is Corpse)
                {
                    if (this.transferables[m].CountToTransfer > 0)
                    {
                        foreach (Thing thing in this.transferables[m].things)
                        {
                            Corpse corpse = thing as Corpse;
                            if (corpse != null && corpse.Spawned)
                            {
                                corpse.Strip();
                            }
                            Thing item = thing.SplitOff(this.transferables[m].CountToTransfer);
                            if (!this.vehicle.Map.GetComponent<LoadItemsToVehicle>().itemsToBeLoaded.ContainsKey(item))
                            { 
                                this.vehicle.Map.GetComponent<LoadItemsToVehicle>().itemsToBeLoaded.Add(item, this.vehicle);
                            }
                            else
                            {
                                Messages.Message(TranslatorFormattedStringExtensions.Translate("YouAlreadyOrderedToLoadThing", thing.Label), MessageTypeDefOf.NeutralEvent, true);
                            }

                        }
                    }
                }
            }
            return true;
        }


        private void AddItemsToTransferables()
        {
            List<Thing> list = CaravanFormingUtility.AllReachableColonyItems(this.map, this.reform, this.reform, this.reform);
            for (int i = 0; i < list.Count; i++)
            {
                this.AddToTransferables(list[i], false);
            }
        }
        private void TryAddCorpseInventoryAndGearToTransferables(Thing potentiallyCorpse)
        {
            Corpse corpse = potentiallyCorpse as Corpse;
            if (corpse != null)
            {
                this.AddCorpseInventoryAndGearToTransferables(corpse);
            }
        }
        private void AddCorpseInventoryAndGearToTransferables(Corpse corpse)
        {
            Pawn_InventoryTracker inventory = corpse.InnerPawn.inventory;
            Pawn_ApparelTracker apparel = corpse.InnerPawn.apparel;
            Pawn_EquipmentTracker equipment = corpse.InnerPawn.equipment;
            for (int i = 0; i < inventory.innerContainer.Count; i++)
            {
                this.AddToTransferables(inventory.innerContainer[i], false);
            }
            if (apparel != null)
            {
                List<Apparel> wornApparel = apparel.WornApparel;
                for (int j = 0; j < wornApparel.Count; j++)
                {
                    this.AddToTransferables(wornApparel[j], false);
                }
            }
            if (equipment != null)
            {
                List<ThingWithComps> allEquipmentListForReading = equipment.AllEquipmentListForReading;
                for (int k = 0; k < allEquipmentListForReading.Count; k++)
                {
                    this.AddToTransferables(allEquipmentListForReading[k], false);
                }
            }
        }
        private void RemoveCarriedItemFromTransferablesOrDrop(Thing carried, Pawn carrier, List<TransferableOneWay> transferables)
        {
            TransferableOneWay transferableOneWay = TransferableUtility.TransferableMatchingDesperate(carried, transferables, TransferAsOneMode.PodsOrCaravanPacking);
            int num;
            if (transferableOneWay == null)
            {
                num = carried.stackCount;
            }
            else if (transferableOneWay.CountToTransfer >= carried.stackCount)
            {
                transferableOneWay.AdjustBy(-carried.stackCount);
                transferableOneWay.things.Remove(carried);
                num = 0;
            }
            else
            {
                num = carried.stackCount - transferableOneWay.CountToTransfer;
                transferableOneWay.AdjustTo(0);
            }
            if (num > 0)
            {
                Thing thing = carried.SplitOff(num);
                if (carrier.SpawnedOrAnyParentSpawned)
                {
                    GenPlace.TryPlaceThing(thing, carrier.PositionHeld, carrier.MapHeld, ThingPlaceMode.Near, null, null);
                }
                else
                {
                    thing.Destroy(DestroyMode.Vanish);
                }
            }
        }
        private void FlashMass()
        {
            this.lastMassFlashTime = Time.time;
        }
        public static bool CanListInventorySeparately(Pawn p)
        {
            return p.Faction == Faction.OfPlayer || p.HostFaction == Faction.OfPlayer;
        }
        private void SetToSendEverything()
        {
            for (int i = 0; i < this.transferables.Count; i++)
            {
                this.transferables[i].AdjustTo(this.transferables[i].GetMaximumToTransfer());
            }
            this.CountToTransferChanged();
        }
        private void CountToTransferChanged()
        {
            this.massUsageDirty = true;
            this.massCapacityDirty = true;
            this.tilesPerDayDirty = true;
            this.daysWorthOfFoodDirty = true;
            this.foragedFoodPerDayDirty = true;
            this.visibilityDirty = true;
            this.ticksToArriveDirty = true;
        }
        public static List<Pawn> AllSendablePawns(Map map, bool reform)
        {
            return CaravanFormingUtility.AllSendablePawns(map, true, reform, reform, reform);
        }

        private Pawn vehicle;
        private Map map;
        private bool reform;
        private Action onClosed;
        private bool canChooseRoute;
        private bool mapAboutToBeRemoved;
        public bool choosingRoute;
        private bool thisWindowInstanceEverOpened;
        public List<TransferableOneWay> transferables;
        private TransferableOneWayWidget pawnsTransfer;
        private TransferableOneWayWidget itemsTransfer;
        private Dialog_LoadItemsToVehicle.Tab tab;
        private float lastMassFlashTime = -9999f;
        private int startingTile = -1;
        private int destinationTile = -1;
        private bool massUsageDirty = true;
        private float cachedMassUsage;
        private bool massCapacityDirty = true;
        private float cachedMassCapacity;
        private string cachedMassCapacityExplanation;
        private bool tilesPerDayDirty = true;
        private float cachedTilesPerDay;
        private string cachedTilesPerDayExplanation;
        private bool daysWorthOfFoodDirty = true;
        private Pair<float, float> cachedDaysWorthOfFood;
        private bool foragedFoodPerDayDirty = true;
        private Pair<ThingDef, float> cachedForagedFoodPerDay;
        private string cachedForagedFoodPerDayExplanation;
        private bool visibilityDirty = true;
        private float cachedVisibility;
        private string cachedVisibilityExplanation;
        private bool ticksToArriveDirty = true;
        private int cachedTicksToArrive;
        private const float TitleRectHeight = 35f;
        private const float BottomAreaHeight = 55f;
        private readonly Vector2 BottomButtonSize = new Vector2(160f, 40f);
        private const float MaxDaysWorthOfFoodToShowWarningDialog = 5f;
        private static List<TabRecord> tabsList = new List<TabRecord>();
        private static List<Thing> tmpPackingSpots = new List<Thing>();
        private enum Tab
        {
            Pawns,
            Items
        }
    }
}