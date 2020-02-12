﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;

namespace O21VehicleFramework
{
    public class CVPathFinder
    {
        internal struct CostNode
        {
            public int index;

            public int cost;

            public CostNode(int index, int cost)
            {
                this.index = index;
                this.cost = cost;
            }
        }

        private struct CVPathFinderNodeFast
        {
            public int knownCost;

            public int heuristicCost;

            public int parentIndex;

            public int costNodeCost;

            public ushort status;
        }

        internal class CostNodeComparer : IComparer<CVPathFinder.CostNode>
        {
            public int Compare(CVPathFinder.CostNode a, CVPathFinder.CostNode b)
            {
                return a.cost.CompareTo(b.cost);
            }
        }

        private Map map;

        private FastPriorityQueue<CVPathFinder.CostNode> openList;

        private CVPathFinder.CVPathFinderNodeFast[] calcGrid;

        private ushort statusOpenValue = 1;

        private ushort statusClosedValue = 2;

        private RegionCostCalculatorWrapper regionCostCalculator;

        private int mapSizeX;

        private int mapSizeZ;

        private PathGrid pathGrid;

        private Building[] edificeGrid;

        private CellIndices cellIndices;

        private List<int> disallowedCornerIndices = new List<int>(4);

        public const int DefaultMoveTicksCardinal = 13;

        private const int DefaultMoveTicksDiagonal = 18;

        private const int SearchLimit = 160000;

        private static readonly int[] Directions = new int[]
        {
            0,
            1,
            0,
            -1,
            1,
            1,
            -1,
            -1,
            -1,
            0,
            1,
            0,
            -1,
            1,
            1,
            -1
        };

        private const int Cost_DoorToBash = 300;

        private const int Cost_BlockedWall = 70;

        private const float Cost_BlockedWallPerHitPoint = 0.11f;

        public const int Cost_OutsideAllowedArea = 600;

        private const int Cost_PawnCollision = 175;

        private const int NodesToOpenBeforeRegionBasedPathing = 2000;

        private const float NonRegionBasedHeuristicStrengthAnimal = 1.75f;

        private static readonly SimpleCurve NonRegionBasedHeuristicStrengthHuman_DistanceCurve = new SimpleCurve
        {
            {
                new CurvePoint(40f, 1f),
                true
            },
            {
                new CurvePoint(120f, 2.8f),
                true
            }
        };

        private static readonly SimpleCurve RegionHeuristicWeightByNodesOpened = new SimpleCurve
        {
            {
                new CurvePoint(0f, 5f),
                true
            },
            {
                new CurvePoint(4000f, 5f),
                true
            },
            {
                new CurvePoint(5000f, 500f),
                true
            }
        };

        public CVPathFinder(Map map)
        {
            this.map = map;
            this.mapSizeX = map.Size.x;
            this.mapSizeZ = map.Size.z;
            this.calcGrid = new CVPathFinder.CVPathFinderNodeFast[this.mapSizeX * this.mapSizeZ];
            this.openList = new FastPriorityQueue<CVPathFinder.CostNode>(new CVPathFinder.CostNodeComparer());
            this.regionCostCalculator = new RegionCostCalculatorWrapper(map);
        }

        //public PawnPath FindPath(IntVec3 start, LocalTargetInfo dest, Pawn pawn, PathEndMode peMode = PathEndMode.OnCell)
        //{
        //    bool flag = false;
        //    if (pawn != null && pawn.CurJob != null && pawn.CurJob.canBash)
        //    {
        //        flag = true;
        //    }
        //    Danger maxDanger = Danger.Deadly;
        //    bool canBash = flag;
        //    return this.FindPath(start, dest, TraverseParms.For(pawn, maxDanger, TraverseMode.ByPawn, canBash), peMode);
        //}

        public PawnPath FindPath(IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode peMode = PathEndMode.OnCell)
        {
            //Walk through Walls Mode
            if (DebugSettings.pathThroughWalls)
            {
                traverseParms.mode = TraverseMode.PassAllDestroyableThings;
            }
            //Check if there's a pawn to path for
            Pawn pawn = traverseParms.pawn;
            if (pawn != null && pawn.Map != this.map)
            {
                Log.Error(string.Concat(new object[]
                {
                    "Tried to FindPath for pawn which is spawned in another map. His map CVPathFinder should have been used, not this one. pawn=",
                    pawn,
                    " pawn.Map=",
                    pawn.Map,
                    " map=",
                    this.map
                }));
                return PawnPath.NotFound;
            }
            //Make sure the start is valid
            if (!start.IsValid)
            {
                Log.Error(string.Concat(new object[]
                {
                    "Tried to FindPath with invalid start ",
                    start,
                    ", pawn= ",
                    pawn
                }));
                return PawnPath.NotFound;
            }
            //Make sure the end is valid
            if (!dest.IsValid)
            {
                Log.Error(string.Concat(new object[]
                {
                    "Tried to FindPath with invalid dest ",
                    dest,
                    ", pawn= ",
                    pawn
                }));
                return PawnPath.NotFound;
            }
            //Check if it's possible to get to the destination
            Log.Error(traverseParms.mode.ToString());
            if (traverseParms.mode == TraverseMode.ByPawn)
            {
                if (!pawn.CanReach(dest, peMode, Danger.Deadly, traverseParms.canBash, traverseParms.mode))
                {
                    return PawnPath.NotFound;
                }
            }
            else if (!this.map.reachability.CanReach(start, dest, peMode, traverseParms))
            {
                return PawnPath.NotFound;
            }
            Log.Error("a");
            this.PfProfilerBeginSample(string.Concat(new object[]
            {
                "FindPath for ",
                pawn,
                " from ",
                start,
                " to ",
                dest,
                (!dest.HasThing) ? string.Empty : (" at " + dest.Cell)
            }));
            this.cellIndices = this.map.cellIndices;
            this.pathGrid = this.map.pathGrid;
            this.edificeGrid = this.map.edificeGrid.InnerArray;
            int x = dest.Cell.x;
            int z = dest.Cell.z;
            int num = this.cellIndices.CellToIndex(start);
            int num2 = this.cellIndices.CellToIndex(dest.Cell);
            ByteGrid byteGrid = (pawn == null) ? null : pawn.GetAvoidGrid();
            bool flag = traverseParms.mode == TraverseMode.PassAllDestroyableThings;
            bool flag2 = traverseParms.mode != TraverseMode.NoPassClosedDoorsOrWater && traverseParms.mode != TraverseMode.PassAllDestroyableThingsNotWater;
            bool flag3 = !flag;
            CellRect cellRect = this.CalculateDestinationRect(dest, peMode);
            bool flag4 = cellRect.Width == 1 && cellRect.Height == 1;
            int[] array = this.map.pathGrid.pathGrid;
            EdificeGrid edificeGrid = this.map.edificeGrid;
            int num3 = 0;
            int num4 = 0;
            Area allowedArea = this.GetAllowedArea(pawn);
            bool flag5 = pawn != null && PawnUtility.ShouldCollideWithPawns(pawn);
            bool flag6 = true && DebugViewSettings.drawPaths;
            bool flag7 = !flag && start.GetRegion(this.map, RegionType.Set_Passable) != null;
            bool flag8 = !flag || !flag3;
            bool flag9 = false;
            int num5 = 0;
            int num6 = 0;
            float num7 = this.DetermineHeuristicStrength(pawn, start, dest);
            int num8;
            int num9;
            Log.Error("b");
            if (pawn != null)
            {
                num8 = pawn.TicksPerMoveCardinal;
                num9 = pawn.TicksPerMoveDiagonal;
            }
            else
            {
                num8 = 13;
                num9 = 18;
            }
            this.CalculateAndAddDisallowedCorners(traverseParms, peMode, cellRect);
            this.InitStatusesAndPushStartNode(ref num, start);
            while (true)
            {
                Log.Error("c");
                this.PfProfilerBeginSample("Open cell");
                if (this.openList.Count <= 0)
                {
                    break;
                }
                num5 += this.openList.Count;
                num6++;
                CVPathFinder.CostNode costNode = this.openList.Pop();
                num = costNode.index;
                if (costNode.cost != this.calcGrid[num].costNodeCost)
                {
                    Log.Error("d");
                    this.PfProfilerEndSample();
                }
                else if (this.calcGrid[num].status == this.statusClosedValue)
                {
                    Log.Error("e");
                    this.PfProfilerEndSample();
                }
                else
                {
                    Log.Error("f");
                    IntVec3 c = this.cellIndices.IndexToCell(num);
                    int x2 = c.x;
                    int z2 = c.z;
                    if (flag6)
                    {
                        this.DebugFlash(c, (float)this.calcGrid[num].knownCost / 1500f, this.calcGrid[num].knownCost.ToString());
                    }
                    if (flag4)
                    {
                        if (num == num2)
                        {
                            goto Block_27;
                        }
                    }
                    else if (cellRect.Contains(c) && !this.disallowedCornerIndices.Contains(num))
                    {
                        goto Block_29;
                    }
                    if (num3 > 160000)
                    {
                        goto Block_30;
                    }
                    this.PfProfilerEndSample();
                    this.PfProfilerBeginSample("Neighbor consideration");
                    for (int i = 0; i < 8; i++)
                    {
                        uint num10 = (uint)(x2 + CVPathFinder.Directions[i]);
                        uint num11 = (uint)(z2 + CVPathFinder.Directions[i + 8]);
                        if ((ulong)num10 < (ulong)((long)this.mapSizeX) && (ulong)num11 < (ulong)((long)this.mapSizeZ))
                        {
                            int num12 = (int)num10;
                            int num13 = (int)num11;
                            int num14 = this.cellIndices.CellToIndex(num12, num13);
                            if (this.calcGrid[num14].status != this.statusClosedValue || flag9)
                            {
                                int num15 = 0;
                                bool flag10 = false;
                                if (flag2 || !new IntVec3(num12, 0, num13).GetTerrain(this.map).HasTag("Water") || (pawn.GetComp<CompVehicle>() != null && pawn.GetComp<CompVehicle>().Props.vehicleType == VehicleType.Amphibious))
                                {
                                    if (!this.pathGrid.WalkableFast(num14))
                                    {
                                        if (!flag)
                                        {
                                            if (flag6)
                                            {
                                                this.DebugFlash(new IntVec3(num12, 0, num13), 0.22f, "walk");
                                            }
                                            goto IL_C2E;
                                        }
                                        flag10 = true;
                                        num15 += 70;
                                        Building building = edificeGrid[num14];
                                        if (building == null)
                                        {
                                            goto IL_C2E;
                                        }
                                        if (!CVPathFinder.IsDestroyable(building))
                                        {
                                            goto IL_C2E;
                                        }
                                        num15 += (int)((float)building.HitPoints * 0.11f);
                                    }
                                    if (i > 3)
                                    {
                                        switch (i)
                                        {
                                            case 4:
                                                if (this.BlocksDiagonalMovement(num - this.mapSizeX))
                                                {
                                                    if (flag8)
                                                    {
                                                        if (flag6)
                                                        {
                                                            this.DebugFlash(new IntVec3(x2, 0, z2 - 1), 0.9f, "corn");
                                                        }
                                                        goto IL_C2E;
                                                    }
                                                    num15 += 70;
                                                }
                                                if (this.BlocksDiagonalMovement(num + 1))
                                                {
                                                    if (flag8)
                                                    {
                                                        if (flag6)
                                                        {
                                                            this.DebugFlash(new IntVec3(x2 + 1, 0, z2), 0.9f, "corn");
                                                        }
                                                        goto IL_C2E;
                                                    }
                                                    num15 += 70;
                                                }
                                                break;
                                            case 5:
                                                if (this.BlocksDiagonalMovement(num + this.mapSizeX))
                                                {
                                                    if (flag8)
                                                    {
                                                        if (flag6)
                                                        {
                                                            this.DebugFlash(new IntVec3(x2, 0, z2 + 1), 0.9f, "corn");
                                                        }
                                                        goto IL_C2E;
                                                    }
                                                    num15 += 70;
                                                }
                                                if (this.BlocksDiagonalMovement(num + 1))
                                                {
                                                    if (flag8)
                                                    {
                                                        if (flag6)
                                                        {
                                                            this.DebugFlash(new IntVec3(x2 + 1, 0, z2), 0.9f, "corn");
                                                        }
                                                        goto IL_C2E;
                                                    }
                                                    num15 += 70;
                                                }
                                                break;
                                            case 6:
                                                if (this.BlocksDiagonalMovement(num + this.mapSizeX))
                                                {
                                                    if (flag8)
                                                    {
                                                        if (flag6)
                                                        {
                                                            this.DebugFlash(new IntVec3(x2, 0, z2 + 1), 0.9f, "corn");
                                                        }
                                                        goto IL_C2E;
                                                    }
                                                    num15 += 70;
                                                }
                                                if (this.BlocksDiagonalMovement(num - 1))
                                                {
                                                    if (flag8)
                                                    {
                                                        if (flag6)
                                                        {
                                                            this.DebugFlash(new IntVec3(x2 - 1, 0, z2), 0.9f, "corn");
                                                        }
                                                        goto IL_C2E;
                                                    }
                                                    num15 += 70;
                                                }
                                                break;
                                            case 7:
                                                if (this.BlocksDiagonalMovement(num - this.mapSizeX))
                                                {
                                                    if (flag8)
                                                    {
                                                        if (flag6)
                                                        {
                                                            this.DebugFlash(new IntVec3(x2, 0, z2 - 1), 0.9f, "corn");
                                                        }
                                                        goto IL_C2E;
                                                    }
                                                    num15 += 70;
                                                }
                                                if (this.BlocksDiagonalMovement(num - 1))
                                                {
                                                    if (flag8)
                                                    {
                                                        if (flag6)
                                                        {
                                                            this.DebugFlash(new IntVec3(x2 - 1, 0, z2), 0.9f, "corn");
                                                        }
                                                        goto IL_C2E;
                                                    }
                                                    num15 += 70;
                                                }
                                                break;
                                        }
                                    }
                                    Log.Error("g");
                                    int num16 = (i <= 3) ? num8 : num9;
                                    num16 += num15;
                                    if (!flag10)
                                    {
                                        num16 += array[num14];
                                    }
                                    if (byteGrid != null)
                                    {
                                        num16 += (int)(byteGrid[num14] * 8);
                                    }
                                    if (allowedArea != null && !allowedArea[num14])
                                    {
                                        num16 += 600;
                                    }
                                    if (flag5 && PawnUtility.AnyPawnBlockingPathAt(new IntVec3(num12, 0, num13), pawn, false, false))
                                    {
                                        num16 += 175;
                                    }
                                    Log.Error("h");
                                    Building building2 = this.edificeGrid[num14];
                                    if (building2 != null)
                                    {
                                        this.PfProfilerBeginSample("Edifices");
                                        int buildingCost = CVPathFinder.GetBuildingCost(building2, traverseParms, pawn);
                                        if (buildingCost == 2147483647)
                                        {
                                            this.PfProfilerEndSample();
                                            goto IL_C2E;
                                        }
                                        num16 += buildingCost;
                                        this.PfProfilerEndSample();
                                    }
                                    int num17 = num16 + this.calcGrid[num].knownCost;
                                    ushort status = this.calcGrid[num14].status;
                                    if (status == this.statusClosedValue || status == this.statusOpenValue)
                                    {
                                        int num18 = 0;
                                        if (status == this.statusClosedValue)
                                        {
                                            num18 = num8;
                                        }
                                        if (this.calcGrid[num14].knownCost <= num17 + num18)
                                        {
                                            goto IL_C2E;
                                        }
                                    }
                                    if (status != this.statusClosedValue && status != this.statusOpenValue)
                                    {
                                        if (flag9)
                                        {
                                            this.calcGrid[num14].heuristicCost = Mathf.RoundToInt((float)this.regionCostCalculator.GetPathCostFromDestToRegion(num14) * CVPathFinder.RegionHeuristicWeightByNodesOpened.Evaluate((float)num4));
                                        }
                                        else
                                        {
                                            int dx = Math.Abs(num12 - x);
                                            int dz = Math.Abs(num13 - z);
                                            int num19 = GenMath.OctileDistance(dx, dz, num8, num9);
                                            this.calcGrid[num14].heuristicCost = Mathf.RoundToInt((float)num19 * num7);
                                        }
                                    }
                                    int num20 = num17 + this.calcGrid[num14].heuristicCost;
                                    this.calcGrid[num14].parentIndex = num;
                                    this.calcGrid[num14].knownCost = num17;
                                    this.calcGrid[num14].status = this.statusOpenValue;
                                    this.calcGrid[num14].costNodeCost = num20;
                                    num4++;
                                    this.openList.Push(new CVPathFinder.CostNode(num14, num20));
                                }
                            }
                        }
                    IL_C2E:;
                    }
                    this.PfProfilerEndSample();
                    num3++;
                    this.calcGrid[num].status = this.statusClosedValue;
                    if (num4 >= 2000 && flag7 && !flag9)
                    {
                        flag9 = true;
                        this.regionCostCalculator.Init(cellRect, traverseParms, num8, num9, byteGrid, allowedArea, this.disallowedCornerIndices);
                        this.InitStatusesAndPushStartNode(ref num, start);
                        num4 = 0;
                        num3 = 0;
                    }
                }
            }
            string text = (pawn == null || pawn.CurJob == null) ? "null" : pawn.CurJob.ToString();
            string text2 = (pawn == null || pawn.Faction == null) ? "null" : pawn.Faction.ToString();
            Log.Warning(string.Concat(new object[]
            {
                pawn,
                " pathing from ",
                start,
                " to ",
                dest,
                " ran out of cells to process.\nJob:",
                text,
                "\nFaction: ",
                text2
            }));
            this.DebugDrawRichData();
            this.PfProfilerEndSample();
            return PawnPath.NotFound;
        Block_27:
            this.PfProfilerEndSample();
            PawnPath result = this.FinalizedPath(num);
            this.PfProfilerEndSample();
            return result;
        Block_29:
            this.PfProfilerEndSample();
            PawnPath result2 = this.FinalizedPath(num);
            this.PfProfilerEndSample();
            return result2;
        Block_30:
            Log.Warning(string.Concat(new object[]
            {
                pawn,
                " pathing from ",
                start,
                " to ",
                dest,
                " hit search limit of ",
                160000,
                " cells."
            }));
            this.DebugDrawRichData();
            this.PfProfilerEndSample();
            return PawnPath.NotFound;
        }

        public static int GetBuildingCost(Building b, TraverseParms traverseParms, Pawn pawn)
        {
            Building_Door building_Door = b as Building_Door;
            if (building_Door != null)
            {
                switch (traverseParms.mode)
                {
                    case TraverseMode.ByPawn:
                        if (!traverseParms.canBash && building_Door.IsForbiddenToPass(pawn))
                        {
                            if (DebugViewSettings.drawPaths)
                            {
                                CVPathFinder.DebugFlash(b.Position, b.Map, 0.77f, "forbid");
                            }
                            return 2147483647;
                        }
                        if (!building_Door.FreePassage)
                        {
                            if (building_Door.PawnCanOpen(pawn))
                            {
                                return building_Door.TicksToOpenNow;
                            }
                            if (traverseParms.canBash)
                            {
                                return 300;
                            }
                            if (DebugViewSettings.drawPaths)
                            {
                                CVPathFinder.DebugFlash(b.Position, b.Map, 0.34f, "cant pass");
                            }
                            return 2147483647;
                        }
                        break;
                    case TraverseMode.NoPassClosedDoors:
                    case TraverseMode.NoPassClosedDoorsOrWater:
                        if (!building_Door.FreePassage)
                        {
                            return 2147483647;
                        }
                        break;
                }
            }
            else if (pawn != null)
            {
                return (int)b.PathFindCostFor(pawn);
            }
            return 0;
        }

        public static bool IsDestroyable(Thing th)
        {
            return th.def.useHitPoints && th.def.destroyable;
        }

        private bool BlocksDiagonalMovement(int x, int z)
        {
            return CVPathFinder.BlocksDiagonalMovement(x, z, this.map);
        }

        private bool BlocksDiagonalMovement(int index)
        {
            return CVPathFinder.BlocksDiagonalMovement(index, this.map);
        }

        public static bool BlocksDiagonalMovement(int x, int z, Map map)
        {
            return CVPathFinder.BlocksDiagonalMovement(map.cellIndices.CellToIndex(x, z), map);
        }

        public static bool BlocksDiagonalMovement(int index, Map map)
        {
            return !map.pathGrid.WalkableFast(index) || map.edificeGrid[index] is Building_Door;
        }

        private void DebugFlash(IntVec3 c, float colorPct, string str)
        {
            CVPathFinder.DebugFlash(c, this.map, colorPct, str);
        }

        private static void DebugFlash(IntVec3 c, Map map, float colorPct, string str)
        {
            map.debugDrawer.FlashCell(c, colorPct, str, 50);
        }

        private PawnPath FinalizedPath(int finalIndex)
        {
            PawnPath emptyPawnPath = this.map.pawnPathPool.GetEmptyPawnPath();
            int num = finalIndex;
            while (true)
            {
                CVPathFinder.CVPathFinderNodeFast pathFinderNodeFast = this.calcGrid[num];
                int parentIndex = pathFinderNodeFast.parentIndex;
                emptyPawnPath.AddNode(this.map.cellIndices.IndexToCell(num));
                if (num == parentIndex)
                {
                    break;
                }
                num = parentIndex;
            }
            emptyPawnPath.SetupFound((float)this.calcGrid[finalIndex].knownCost);
            return emptyPawnPath;
        }

        private void InitStatusesAndPushStartNode(ref int curIndex, IntVec3 start)
        {
            this.statusOpenValue += 2;
            this.statusClosedValue += 2;
            if (this.statusClosedValue >= 65435)
            {
                this.ResetStatuses();
            }
            curIndex = this.cellIndices.CellToIndex(start);
            this.calcGrid[curIndex].knownCost = 0;
            this.calcGrid[curIndex].heuristicCost = 0;
            this.calcGrid[curIndex].costNodeCost = 0;
            this.calcGrid[curIndex].parentIndex = curIndex;
            this.calcGrid[curIndex].status = this.statusOpenValue;
            this.openList.Clear();
            this.openList.Push(new CVPathFinder.CostNode(curIndex, 0));
        }

        private void ResetStatuses()
        {
            int num = this.calcGrid.Length;
            for (int i = 0; i < num; i++)
            {
                this.calcGrid[i].status = 0;
            }
            this.statusOpenValue = 1;
            this.statusClosedValue = 2;
        }

        [Conditional("PFPROFILE")]
        private void PfProfilerBeginSample(string s)
        {
        }

        [Conditional("PFPROFILE")]
        private void PfProfilerEndSample()
        {
        }

        private void DebugDrawRichData()
        {
            if (DebugViewSettings.drawPaths)
            {
                while (this.openList.Count > 0)
                {
                    int index = this.openList.Pop().index;
                    IntVec3 c = new IntVec3(index % this.mapSizeX, 0, index / this.mapSizeX);
                    this.map.debugDrawer.FlashCell(c, 0f, "open", 50);
                }
            }
        }

        private float DetermineHeuristicStrength(Pawn pawn, IntVec3 start, LocalTargetInfo dest)
        {
            if (pawn != null && pawn.RaceProps.Animal)
            {
                return 1.75f;
            }
            float lengthHorizontal = (start - dest.Cell).LengthHorizontal;
            return (float)Mathf.RoundToInt(CVPathFinder.NonRegionBasedHeuristicStrengthHuman_DistanceCurve.Evaluate(lengthHorizontal));
        }

        private CellRect CalculateDestinationRect(LocalTargetInfo dest, PathEndMode peMode)
        {
            CellRect result;
            if (!dest.HasThing || peMode == PathEndMode.OnCell)
            {
                result = CellRect.SingleCell(dest.Cell);
            }
            else
            {
                result = dest.Thing.OccupiedRect();
            }
            if (peMode == PathEndMode.Touch)
            {
                result = result.ExpandedBy(1);
            }
            return result;
        }

        private Area GetAllowedArea(Pawn pawn)
        {
            if (pawn != null && pawn.playerSettings != null && !pawn.Drafted && ForbidUtility.CaresAboutForbidden(pawn, true))
            {
                Area area = pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap;
                if (area != null && area.TrueCount <= 0)
                {
                    area = null;
                }
                return area;
            }
            return null;
        }

        private void CalculateAndAddDisallowedCorners(TraverseParms traverseParms, PathEndMode peMode, CellRect destinationRect)
        {
            this.disallowedCornerIndices.Clear();
            if (peMode == PathEndMode.Touch)
            {
                int minX = destinationRect.minX;
                int minZ = destinationRect.minZ;
                int maxX = destinationRect.maxX;
                int maxZ = destinationRect.maxZ;
                if (!this.IsCornerTouchAllowed(minX + 1, minZ + 1, minX + 1, minZ, minX, minZ + 1))
                {
                    this.disallowedCornerIndices.Add(this.map.cellIndices.CellToIndex(minX, minZ));
                }
                if (!this.IsCornerTouchAllowed(minX + 1, maxZ - 1, minX + 1, maxZ, minX, maxZ - 1))
                {
                    this.disallowedCornerIndices.Add(this.map.cellIndices.CellToIndex(minX, maxZ));
                }
                if (!this.IsCornerTouchAllowed(maxX - 1, maxZ - 1, maxX - 1, maxZ, maxX, maxZ - 1))
                {
                    this.disallowedCornerIndices.Add(this.map.cellIndices.CellToIndex(maxX, maxZ));
                }
                if (!this.IsCornerTouchAllowed(maxX - 1, minZ + 1, maxX - 1, minZ, maxX, minZ + 1))
                {
                    this.disallowedCornerIndices.Add(this.map.cellIndices.CellToIndex(maxX, minZ));
                }
            }
        }

        private bool IsCornerTouchAllowed(int cornerX, int cornerZ, int adjCardinal1X, int adjCardinal1Z, int adjCardinal2X, int adjCardinal2Z)
        {
            return TouchPathEndModeUtility.IsCornerTouchAllowed(cornerX, cornerZ, adjCardinal1X, adjCardinal1Z, adjCardinal2X, adjCardinal2Z, this.map);
        }
    }
}
