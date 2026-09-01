// Freeze-period move: Boards cluster.
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TD
{
    public sealed partial class TDGameManager : MonoBehaviour
    {
        private void HandleRadialTowerSelected(Vector2Int cell, TDTowerKind kind)
        {
            if (!IsBuildWindowOpen())
            {
                DenyRadialAction("Build is disabled during combat. Wait for prep phase.");
                return;
            }

            if (!IsTowerUnlocked(kind))
            {
                DenyRadialAction($"{GetTowerKindLabel(kind)} is not in the active formation.");
                return;
            }

            var cost = TDTower.GetBuildCost(kind);
            if (_defenseBudget < cost)
            {
                DenyRadialAction($"Insufficient defense budget. {GetTowerKindLabel(kind)} needs {cost}.");
                return;
            }

            // The site could have been taken between opening the menu and
            // confirming (debug builds, automation probes) — never stack towers.
            if (_gridMap == null || !_gridMap.IsBuildable(cell))
            {
                DenyRadialAction("Build site is no longer available.");
                return;
            }

            _defenseBudget -= cost;
            _budgetSpentOnBuilds += cost;
            _gridMap.SetTower(cell, true);
            var tower = SpawnTower(cell, kind);
            SelectTowerForUi(tower);
            _builtTowerCount++;
            _selectedTowerKind = kind;
            PushTacticalEvent($"Build: {GetTowerKindLabel(kind)} at {cell.x},{cell.y} (-{cost})", 4.2f);
            SetStatus($"Built {GetTowerKindLabel(kind)} (-{cost} budget)");
            PlaySfxTone("tower_build", 420f, 0.10f, 0.55f, true);
            AdvanceTutorial(TDFirstRunTutorialStep.BuildTower);
        }

        private void BuildBoard()
        {
            var boardRoot = new GameObject("Board").transform;
            boardRoot.SetParent(transform, false);

            var mapId = _campaignRoute?.level?.mapId ?? "grayline_junction";
            ConfigureActiveLanePaths(mapId);
            StartAmbienceForMap(mapId);
            var roadPaths = _activeLanePaths.Values
                .Where(path => path != null && path.Count > 1)
                .Distinct()
                .ToArray();
            _gridMap = new TDGridMap(
                GridWidth,
                GridHeight,
                CellSize,
                GetPathCellsForMap(mapId),
                boardRoot,
                mapId,
                roadPaths);
            BuildExamScenarioDevice(boardRoot);
        }

        private void RefreshUnlockedTowerKinds()
        {
            _availableTowerKinds.Clear();
            _unlockedTowerKinds.Clear();
            var currentLevel = _campaignRoute?.level?.levelIndex ?? DefaultCampaignLevelIndex;
            _availableTowerKinds.AddRange(GetTowerKindsUnlockedAtLevel(currentLevel));
            _activeResonanceDoctrine = _campaignRoute?.level == null
                ? TDResonanceDoctrine.Adaptive
                : TDCampaignProgression.GetResonanceDoctrine(currentLevel);
            if (!IsDoctrineAvailableForLevel(currentLevel))
            {
                _activeResonanceDoctrine = TDResonanceDoctrine.Adaptive;
            }

            if (_campaignRoute?.level != null)
            {
                var savedIds = TDCampaignProgression.GetTowerLoadout(currentLevel);
                for (var i = 0; i < savedIds.Length && _unlockedTowerKinds.Count < TDCampaignProgression.MaxFormationTowers; i++)
                {
                    if (!TDTower.TryParseTowerId(savedIds[i], out var kind) ||
                        !_availableTowerKinds.Contains(kind) ||
                        _unlockedTowerKinds.Contains(kind))
                    {
                        continue;
                    }

                    _unlockedTowerKinds.Add(kind);
                }
            }

            if (_unlockedTowerKinds.Count == 0)
            {
                BuildAutoFitFormation(
                    currentLevel,
                    _availableTowerKinds,
                    out var fittedTowers,
                    out var fittedDoctrine);
                for (var i = 0; i < fittedTowers.Count && i < TDCampaignProgression.MaxFormationTowers; i++)
                {
                    _unlockedTowerKinds.Add(fittedTowers[i]);
                }

                _activeResonanceDoctrine = fittedDoctrine;
            }

            if (_unlockedTowerKinds.Count == 0)
            {
                _unlockedTowerKinds.Add(TDTowerKind.RailLancer);
            }

            if (!_unlockedTowerKinds.Contains(_selectedTowerKind))
            {
                _selectedTowerKind = _unlockedTowerKinds[0];
            }
        }

        private void TryPlaceTowerAtCursor()
        {
            if (!IsBuildWindowOpen())
            {
                SetStatus("Build is disabled during combat. Wait for prep phase.");
                return;
            }

            // If radial menu is open and player clicks outside, close it.
            if (_radialTowerMenu != null && _radialTowerMenu.IsVisible)
            {
                _radialTowerMenu.Hide();
                return;
            }

            var mouse = TDInputCompat.MousePosition;
            mouse.z = -_mainCamera.transform.position.z;
            var world = _mainCamera.ScreenToWorldPoint(mouse);
            world.z = 0f;

            if (!_gridMap.TryWorldToCell(world, out var cell))
            {
                return;
            }

            // Clicking an existing tower selects it for upgrade.
            if (TryGetTowerUnderCursor(world, out var existingTower))
            {
                SelectTowerForUi(existingTower);
                SetStatus($"Selected {existingTower.DisplayName}.");
                return;
            }

            if (!_gridMap.IsBuildable(cell))
            {
                return;
            }

            // Show the radial tower selection menu at this build site.
            if (_radialTowerMenu != null && _unlockedTowerKinds.Count > 0)
            {
                var kinds = new TDTowerKind[_unlockedTowerKinds.Count];
                var costs = new int[_unlockedTowerKinds.Count];
                var unlocked = new bool[_unlockedTowerKinds.Count];
                for (var i = 0; i < _unlockedTowerKinds.Count; i++)
                {
                    kinds[i] = _unlockedTowerKinds[i];
                    costs[i] = TDTower.GetBuildCost(kinds[i]);
                    unlocked[i] = true;
                }

                _radialTowerMenu.Show(mouse, cell, world, kinds, costs, _defenseBudget, unlocked);
                PlaySfxTone("ui_hover", 620f, 0.045f, 0.22f, true);
            }
        }

        private void TryUpgradeTowerAtCursor()
        {
            var mouse = TDInputCompat.MousePosition;
            mouse.z = -_mainCamera.transform.position.z;
            var world = _mainCamera.ScreenToWorldPoint(mouse);
            world.z = 0f;

            if (!TryGetTowerUnderCursor(world, out var tower))
            {
                return;
            }

            SelectTowerForUi(tower);
            TryUpgradeTower(tower, _selectedUpgradeBranch);
        }

        private void TrySellTower(TDTower tower)
        {
            if (tower == null || tower.gameObject == null)
            {
                SetStatus("Select a tower before selling.");
                return;
            }

            if (!IsBuildWindowOpen())
            {
                SetStatus("Sell is disabled during combat. Wait for prep phase.");
                PlaySfxTone("ui_deny", 180f, 0.08f, 0.30f, true);
                return;
            }

            var displayName = tower.DisplayName;
            var cell = tower.GridCell;
            // Meta line B (Field Salvage) raises the refund ratio above the
            // 60% base without touching TDTower's constant.
            var refund = Mathf.FloorToInt(
                tower.TotalInvested * TDMetaUpgradeSystem.GetSellRefundRatio(GetMetaRank(TDMetaUpgradeSystem.UpgradeLine.B)));

            if (_selectedTowerForUi == tower)
            {
                _selectedTowerForUi = null;
            }

            if (_hoveredTower == tower)
            {
                _hoveredTower = null;
            }

            if (_lastHoverSfxTower == tower)
            {
                _lastHoverSfxTower = null;
            }

            UpdateTowerTooltip(null);
            UnregisterSalvageDerrick(tower);
            TDBlockerWagon.RetractFor(tower);
            _gridMap?.SetTower(cell, false);
            _builtTowerCount = Mathf.Max(0, _builtTowerCount - 1);
            _defenseBudget += refund;
            Destroy(tower.gameObject);
            PushTacticalEvent($"Sell: {displayName} (+{refund})", 4.2f);
            SetStatus($"Sold {displayName} (+{refund} budget)");
            PlaySfxTone("tower_sell", 320f, 0.12f, 0.45f, false);
        }

        private void TryUpgradeTower(TDTower tower, TDTowerUpgradeBranch branch)
        {
            if (!IsBuildWindowOpen())
            {
                SetStatus("Upgrade is disabled during combat. Wait for prep phase.");
                return;
            }

            if (tower == null)
            {
                SetStatus("Select a tower before upgrading.");
                return;
            }

            if (!tower.CanUpgrade)
            {
                SetStatus("Tower is already at max tier.");
                return;
            }

            var upgradeCost = tower.GetUpgradeCost(branch);
            if (_defenseBudget < upgradeCost)
            {
                SetStatus($"Insufficient defense budget. Upgrade needs {upgradeCost}.");
                return;
            }

            if (!tower.ApplyUpgrade(branch))
            {
                SetStatus("Upgrade failed.");
                return;
            }

            RecordTowerCodexObservation(
                tower.Kind,
                branch == TDTowerUpgradeBranch.Damage
                    ? TDTowerCodexObservation.DamageBranch
                    : TDTowerCodexObservation.UtilityBranch);
            _defenseBudget -= upgradeCost;
            _budgetSpentOnUpgrades += upgradeCost;
            _upgradesPurchased++;
            RecordTowerUpgradeForAnalytics(tower, upgradeCost);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
            TrackP135TowerUpgrade(tower);
#endif
            SelectTowerForUi(tower);
            PushTacticalEvent($"Upgrade: {tower.DisplayName} {GetUpgradeBranchLabel(branch)} ({tower.SpecializationLabel}) (-{upgradeCost})", 4.6f);
            SetStatus($"Upgraded {tower.DisplayName} [{GetUpgradeBranchLabel(branch)}] {tower.SpecializationLabel} (-{upgradeCost} budget)");
            var upgradeFrequency = 500f + ((int)tower.Kind * 34f) + (branch == TDTowerUpgradeBranch.Utility ? 42f : 0f);
            PlaySfxTone($"tower_upgrade_{tower.Kind.ToString().ToLowerInvariant()}", upgradeFrequency, 0.12f, 0.60f, true);
            AdvanceTutorial(TDFirstRunTutorialStep.UpgradeTower);
        }

        private bool TryGetTowerUnderCursor(Vector3 world, out TDTower tower)
        {
            tower = null;
            var hit = Physics2D.OverlapPoint(world);
            if (hit == null)
            {
                return false;
            }

            tower = hit.GetComponent<TDTower>() ?? hit.GetComponentInParent<TDTower>();
            return tower != null;
        }

        private void TrySpawnBlockerWagon(TDTower tower)
        {
            // Barricades park their wagon on the nearest track point of the
            // default route; one wagon per segment key (later builds on the
            // same segment are no-ops).
            if (tower == null || tower.Kind != TDTowerKind.RailBarricade)
            {
                return;
            }

            var points = _gridMap?.PathWorldPoints;
            if (points == null || points.Count == 0)
            {
                return;
            }

            var bestIndex = -1;
            var bestSqr = float.MaxValue;
            var origin = tower.transform.position;
            for (var i = 0; i < points.Count; i++)
            {
                var sqr = (points[i] - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                TDBlockerWagon.SpawnFor(this, tower, points[bestIndex], $"seg_{bestIndex}");
            }
        }

        private TDTower SpawnTower(Vector2Int cell, TDTowerKind kind)
        {
            var towerObject = new GameObject($"Tower_{cell.x}_{cell.y}");
            towerObject.transform.position = _gridMap.CellToBuildWorld(cell);
            towerObject.transform.localScale = Vector3.one;
            towerObject.transform.SetParent(transform, true);

            var collider = towerObject.AddComponent<BoxCollider2D>();
            collider.size = GetTowerColliderSize(kind);
            collider.offset = GetTowerColliderOffset(kind);

            var tower = towerObject.AddComponent<TDTower>();
            tower.Initialize(this, kind, cell);
            RegisterTowerForAnalytics(tower);
            RegisterSalvageDerrick(tower);
            TrySpawnBlockerWagon(tower);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || TD_AUTOMATION
            TrackP135TowerBuilt(tower);
#endif
            RecordTowerCodexObservation(kind, TDTowerCodexObservation.Built);
            return tower;
        }

    }
}
