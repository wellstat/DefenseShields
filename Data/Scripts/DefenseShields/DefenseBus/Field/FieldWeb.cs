﻿using System;
using System.Collections.Generic;
using System.Linq;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DefenseSystems
{
    internal partial class Fields
    {
        #region Web Entities
        public void CleanWebEnts()
        {
            AuthenticatedCache.Clear();
            IgnoreCache.Clear();

            _protectEntsTmp.Clear();
            _protectEntsTmp.AddRange(ProtectedEntCache.Where(info => Bus.Tick - info.Value.LastTick > 180));
            foreach (var protectedEnt in _protectEntsTmp) ProtectedEntCache.Remove(protectedEnt.Key);

            _webEntsTmp.Clear();
            _webEntsTmp.AddRange(WebEnts.Where(info => Bus.Tick - info.Value.LastTick > 180));
            foreach (var webent in _webEntsTmp)
            {
                EntIntersectInfo removedEnt;
                WebEnts.TryRemove(webent.Key, out removedEnt);
                EnemyShields.Remove(webent.Key);
            }
        }


        public bool ResetEnts(MyEntity ent, uint tick)
        {
            var a = Bus.ActiveController;
            MyProtectors protectors;
            Session.Instance.GlobalProtect.TryGetValue(ent, out protectors);
            if (protectors == null)
            {
                protectors = Session.Instance.GlobalProtect[ent] = Session.ProtSets.Get();
                protectors.Init(a.LogicSlot, tick);
            }

            var grid = ent as MyCubeGrid;
            if (grid != null)
            {
                if (CustomCollision.CornerOrCenterInShield(grid, DetectMatrixOutsideInv, _resetEntCorners, true) == 0) return false;

                protectors.Controllers.Add(a);
                return true;
            }

            if (!CustomCollision.PointInShield(ent.PositionComp.WorldAABB.Center, DetectMatrixOutsideInv)) return false;
            protectors.Controllers.Add(a);
            return true;
        }

        public void WebEntities()
        {
            var a = Bus.ActiveController;
            PruneList.Clear();
            MyGamePruningStructure.GetTopMostEntitiesInBox(ref WebBox, PruneList);
            if (Missiles.Count > 0)
            {
                var missileBox = WebBox;
                foreach (var missile in Missiles)
                    if (missile.InScene && !missile.MarkedForClose && missileBox.Intersects(missile.PositionComp.WorldAABB)) PruneList.Add(missile);
            }
            var shieldsStartIndex = PruneList.Count;
            foreach (var eShield in EnemyShields) PruneList.Add(eShield);

            var disableVoxels = Session.Enforced.DisableVoxelSupport == 1 || Bus.ActiveModulator == null || Bus.ActiveModulator.ModSet.Settings.ModulateVoxels;
            var voxelFound = false;
            var shieldFound = false;
            var entChanged = false;
            var iMoving = Bus.SpineIsMoving;
            var tick = Session.Instance.Tick;

            _enablePhysics = false;
            for (int i = 0; i < PruneList.Count; i++)
            {
                var ent = PruneList[i];
                var entPhysics = ent.Physics;

                if (i < shieldsStartIndex)
                {
                    var voxel = ent as MyVoxelBase;
                    if (ent == null || (voxel == null && (entPhysics == null || ent.DefinitionId == null)) || (voxel != null && (!iMoving || !ShieldIsMobile || disableVoxels || voxel != voxel.RootVoxel))) continue;

                    bool quickReject;
                    if (_isServer) quickReject = ent is IMyEngineerToolBase || IgnoreCache.Contains(ent) || EnemyShields.Contains(ent) || FriendlyMissileCache.Contains(ent) || AuthenticatedCache.Contains(ent);
                    else quickReject = (!(ent is MyCubeGrid) && voxel == null && !(ent is IMyCharacter)) || IgnoreCache.Contains(ent) || EnemyShields.Contains(ent) || AuthenticatedCache.Contains(ent);

                    var floater = ent as IMyFloatingObject;
                    if (quickReject || floater != null && (!iMoving && Vector3.IsZero(entPhysics.LinearVelocity, 1e-2f)) || !WebSphere.Intersects(ent.PositionComp.WorldVolume)) continue;
                    if (voxel != null)
                    {
                        if (VoxelsToIntersect.ContainsKey(voxel)) VoxelsToIntersect[voxel]++;
                        else VoxelsToIntersect[voxel] = 1;
                        voxelFound = true;
                        entChanged = true;
                        _enablePhysics = true;
                        continue;
                    }
                }
                Ent relation;

                ProtectCache protectedEnt;
                EntIntersectInfo entInfo = null;
                ProtectedEntCache.TryGetValue(ent, out protectedEnt);

                var refreshInfo = false;
                if (protectedEnt == null)
                {
                    WebEnts.TryGetValue(ent, out entInfo);
                    if (entInfo != null)
                    {
                        var last = entInfo.LastTick;
                        var refresh = entInfo.RefreshTick;
                        var refreshTick = tick - last > 180 || (tick - last == 180 && tick - refresh >= 3600) || (tick - last == 1 && tick - refresh >= 60);
                        refreshInfo = refreshTick;
                        if (refreshInfo || entInfo.RefreshNow)
                        {
                            entInfo.RefreshTick = tick;
                            entInfo.Relation = EntType(ent);
                        }
                        relation = entInfo.Relation;
                        entInfo.LastTick = tick;
                    }
                    else relation = EntType(ent);
                }
                else
                {
                    var last = protectedEnt.LastTick;
                    var refresh = protectedEnt.RefreshTick;
                    var refreshTick = tick - last > 180 || (tick - last == 180 && tick - refresh >= 3600) || (tick - last == 1 && tick - refresh >= 60);
                    refreshInfo = refreshTick;
                    if (refreshInfo)
                    {
                        protectedEnt.RefreshTick = tick;
                        protectedEnt.PreviousRelation = protectedEnt.Relation;
                        protectedEnt.Relation = EntType(ent);
                    }
                    relation = protectedEnt.Relation;
                    protectedEnt.LastTick = tick;
                }
                switch (relation)
                {
                    case Ent.Authenticated:
                        continue;
                    case Ent.Ignore:
                    case Ent.Friendly:
                    case Ent.Protected:
                        if (relation == Ent.Protected)
                        {
                            if (protectedEnt == null) ProtectedEntCache[ent] = new ProtectCache(tick, tick, tick, relation, relation);
                            MyProtectors protectors;
                            Session.Instance.GlobalProtect.TryGetValue(ent, out protectors);
                            if (protectors == null)
                            {
                                protectors = Session.Instance.GlobalProtect[ent] = Session.ProtSets.Get();
                                protectors.Init(a.LogicSlot, tick);
                            }
                            if (protectors.Controllers.Contains(a)) continue;

                            protectors.Controllers.Add(a);
                            protectors.Controllers.ApplyAdditions();
                            continue;
                        }
                        IgnoreCache.Add(ent);
                        continue;
                }

                if (relation == Ent.Shielded) shieldFound = true;
                try
                {
                    if (entInfo != null)
                    {
                        var interestingEnts = relation == Ent.Floater || relation == Ent.EnemyGrid || relation == Ent.NobodyGrid || relation == Ent.Shielded;
                        if (entPhysics != null && entPhysics.IsMoving) entChanged = true;
                        else if (entInfo.Touched || (refreshInfo && interestingEnts && !ent.PositionComp.LocalAABB.Equals(entInfo.Box)))
                        {
                            entInfo.RefreshTick = tick;
                            entInfo.Box = ent.PositionComp.LocalAABB;
                            entChanged = true;
                        }

                        _enablePhysics = true;
                        if (refreshInfo)
                        {
                            if ((relation == Ent.EnemyGrid || relation == Ent.NobodyGrid) && entInfo.CacheBlockList.Count != (ent as MyCubeGrid).BlocksCount)
                            {
                                entInfo.RefreshNow = true;
                            }
                        }
                    }
                    else
                    {
                        if (relation == Ent.Other)
                        {
                            var entPast = -Vector3D.Normalize(entPhysics.LinearVelocity) * 6;
                            var entTestLoc = ent.PositionComp.WorldVolume.Center + entPast;
                            var centerStep = -Vector3D.Normalize(entTestLoc - DetectionCenter) * 2f;
                            var counterDrift = centerStep + entTestLoc;
                            if (CustomCollision.PointInShield(counterDrift, DetectMatrixOutsideInv))
                            {
                                FriendlyMissileCache.Add(ent);
                                continue;
                            }
                        }
                        entChanged = true;
                        _enablePhysics = true;
                        ProtectedEntCache.Remove(ent);
                        WebEnts.TryAdd(ent, new EntIntersectInfo(false, ent.PositionComp.LocalAABB, tick, tick, tick, relation));
                    }
                }
                catch (Exception ex) { Log.Line($"Exception in WebEntities entInfo: {ex}"); }
            }

            if (Session.Instance.LogStats) Session.Instance.Perf.WebEnts(WebEnts.Count);

            if (!_enablePhysics)
            {
                return;
            }

            ShieldMatrix = ShieldEnt.PositionComp.WorldMatrix;
            if ((_needPhysics && shieldFound) || !ShieldMatrix.EqualsFast(ref OldShieldMatrix))
            {
                OldShieldMatrix = ShieldMatrix;
                if (shieldFound)
                {
                    _needPhysics = false;
                    Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, PhysicsOutside);
                }
                else _needPhysics = true;
                if (voxelFound) Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, PhysicsOutsideLow);
            }

            if (Session.Instance.LogStats)
            {
                Session.Instance.Perf.WebPhysics();
                if (entChanged) Session.Instance.Perf.EntChanged();
            }

            if (iMoving || entChanged)
            {
                a.LastWokenTick = tick;
                Session.Instance.WebWrapper.Enqueue(this);
                Session.Instance.WebWrapperOn = true;
            }
        }
        #endregion

        #region Gather Entity Information
        public Ent EntType(MyEntity ent)
        {
            var a = Bus.ActiveController;
            if (ent is IMyFloatingObject)
            {
                if (CustomCollision.AllAabbInShield(ent.PositionComp.WorldAABB, DetectMatrixOutsideInv, _obbCorners)) return Ent.Ignore;
                return Ent.Floater;
            }

            var voxel = ent as MyVoxelBase;
            if (voxel != null && (Session.Enforced.DisableVoxelSupport == 1 || Bus.ActiveModulator == null || Bus.ActiveModulator.ModSet.Settings.ModulateVoxels || !ShieldIsMobile)) return Ent.Ignore;
            if (EntityBypass.Contains(ent)) return Ent.Ignore;

            var character = ent as IMyCharacter;
            if (character != null)
            {
                var dude = MyAPIGateway.Players.GetPlayerControllingEntity(ent)?.IdentityId;
                if (dude == null) return Ent.Ignore;
                var playerrelationship = a.MyCube.GetUserRelationToOwner((long)dude);
                if (playerrelationship == MyRelationsBetweenPlayerAndBlock.Owner || playerrelationship == MyRelationsBetweenPlayerAndBlock.FactionShare)
                {
                    var playerInShield = CustomCollision.PointInShield(ent.PositionComp.WorldAABB.Center, DetectMatrixOutsideInv);
                    return playerInShield ? Ent.Protected : Ent.Friendly;
                }

                if (character.IsDead) return Ent.Ignore;

                if (CustomCollision.NewObbPointsInShield(ent, DetectMatrixOutsideInv, _obbPoints) == 9)
                {
                    return Ent.EnemyInside;
                }
                return Ent.EnemyPlayer;
            }
            var grid = ent as MyCubeGrid;
            if (grid != null)
            {
                ModulateGrids = (Bus.ActiveModulator != null && Bus.ActiveModulator.ModSet.Settings.ModulateGrids) || Session.Enforced.DisableEntityBarrier == 1;
                ModulatorGridComponent modComp;
                grid.Components.TryGet(out modComp);
                if (!string.IsNullOrEmpty(modComp?.ModulationPassword) && modComp.ModulationPassword == a.Controller.CustomData)
                {
                    var collection = modComp.Modulator?.Bus?.ActiveController != null ? modComp.Modulator.Bus.ActiveController.Bus.SubGrids : modComp.SubGrids;
                    foreach (var subGrid in collection)
                    {
                        if (ShieldEnt.PositionComp.WorldVolume.Intersects(grid.PositionComp.WorldVolume))
                        {
                            if (CustomCollision.CornerOrCenterInShield(grid, DetectMatrixOutsideInv, _resetEntCorners) > 0) return Ent.Protected;
                            AuthenticatedCache.Add(subGrid);
                        }
                        else AuthenticatedCache.Add(subGrid);
                    }
                    return Ent.Authenticated;
                }
                var bigOwners = grid.BigOwners;
                var bigOwnersCnt = bigOwners.Count;
                if (CustomCollision.AllAabbInShield(ent.PositionComp.WorldAABB, DetectMatrixOutsideInv, _obbCorners)) return Ent.Protected;
                if (!ModulateGrids && bigOwnersCnt == 0) return Ent.NobodyGrid;
                var enemy = !ModulateGrids && GridEnemy(grid, bigOwners);
                if (!enemy)
                {
                    if (Bus.SubGrids.Contains(grid)) return Ent.Protected;
                    var pointsInShield = CustomCollision.NewObbPointsInShield(grid, DetectMatrixOutsideInv, _obbPoints);
                    return pointsInShield > 0 ? Ent.Protected : Ent.Friendly;
                }

                Bus otherBus;
                grid.Components.TryGet(out otherBus);
                if (otherBus?.ActiveController != null && otherBus.ActiveController.NotFailed)
                {
                    var otherField = otherBus.ActiveController.Bus.Field;
                    var shieldEntity = a.MyCube.Parent;
                    otherField.EnemyShields.Add(shieldEntity);
                    return Ent.Shielded;
                }
                return Ent.EnemyGrid;
            }

            if (ent is IMyMeteor || (ent.DefinitionId.HasValue && ent.DefinitionId.Value.TypeId == Session.Instance.MissileObj)) return Ent.Other;
            if (voxel != null && ShieldIsMobile) return Ent.VoxelBase;
            return 0;
        }

        public bool GridEnemy(MyCubeGrid grid, List<long> owners = null)
        {
            if (owners == null) owners = grid.BigOwners;
            if (owners.Count == 0) return true;
            var relationship = Bus.ActiveController.MyCube.GetUserRelationToOwner(owners[0]);
            var enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
            return enemy;
        }
        #endregion
    }
}