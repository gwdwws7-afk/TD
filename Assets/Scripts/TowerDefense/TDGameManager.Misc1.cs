// Freeze-period move: Misc1 cluster.
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
        private string GetEnemyDisplayName(string enemyId)
        {
            if (!string.IsNullOrWhiteSpace(enemyId) && _enemyCatalog.TryGetValue(enemyId, out var entry) && !string.IsNullOrWhiteSpace(entry.displayName))
            {
                return entry.displayName;
            }

            return string.IsNullOrWhiteSpace(enemyId) ? "Unknown" : enemyId.Replace('_', ' ');
        }

        private static string GetCompactTowerLabel(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => "Rail",
                TDTowerKind.CinderMortar => "Mortar",
                TDTowerKind.FrostCoil => "Frost",
                TDTowerKind.ArcWelder => "Arc",
                TDTowerKind.SiegeDrill => "Siege",
                TDTowerKind.EmberFlak => "Flak",
                TDTowerKind.ResonanceBeacon => "Beacon",
                TDTowerKind.GravSnare => "Snare",
                _ => kind.ToString()
            };
        }

        private static string GetLocalizedCompactTowerLabel(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => "轨枪",
                TDTowerKind.CinderMortar => "迫击炮",
                TDTowerKind.FrostCoil => "霜冻",
                TDTowerKind.ArcWelder => "电弧",
                TDTowerKind.SiegeDrill => "钻机",
                TDTowerKind.EmberFlak => "高射炮",
                TDTowerKind.ResonanceBeacon => "信标",
                TDTowerKind.GravSnare => "重力阱",
                _ => kind.ToString()
            };
        }

        private string GetSelectedTowerSlotLabel()
        {
            var slot = _unlockedTowerKinds.IndexOf(_selectedTowerKind);
            return slot >= 0 ? $"[{slot + 1}]" : "[?]";
        }

        private void TryUpgradeSelectedTowerFromUi(TDTowerUpgradeBranch branch)
        {
            var tower = GetUiFocusedTower();
            if (tower == null)
            {
                SetStatus("Select a tower before upgrading.");
                return;
            }

            _selectedTowerForUi = tower;
            _selectedUpgradeBranch = branch;
            TryUpgradeTower(tower, branch);
        }

        private TDTower GetUiFocusedTower()
        {
            if (_selectedTowerForUi != null && _selectedTowerForUi.gameObject != null)
            {
                return _selectedTowerForUi;
            }

            return _hoveredTower != null && _hoveredTower.gameObject != null ? _hoveredTower : null;
        }

        private void SelectTowerForUi(TDTower tower)
        {
            if (tower == null)
            {
                return;
            }

            var previous = _selectedTowerForUi;
            _selectedTowerForUi = tower;
            if (previous != null && previous != tower)
            {
                previous.Readability?.SetInteractionState(previous == _hoveredTower, false);
            }

            tower.Readability?.SetInteractionState(tower == _hoveredTower, true);
        }

        private bool IsPointerOverBattleUi()
        {
            return UseRuntimeBattleUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void EnsureUiEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        private IEnumerator SelectUiNextFrame(Selectable selectable)
        {
            yield return null;
            if (EventSystem.current != null && selectable != null && selectable.gameObject.activeInHierarchy && selectable.interactable)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }

        private void EnsureGamepadFocus()
        {
            // While the gamepad virtual cursor owns the board, focus navigation
            // must stay out of the way — a focused Selectable would turn South
            // into an accidental submit on top of the virtual click.
            if (_gamepadCursorMode || !TDInputCompat.GetGamepadNavigationDown() || EventSystem.current == null)
            {
                return;
            }

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy)
            {
                var selectable = selected.GetComponent<Selectable>();
                if (selectable != null && selectable.interactable)
                {
                    return;
                }
            }

            Selectable fallback = null;
            if (_uiStartWaveButton != null && _uiStartWaveButton.gameObject.activeInHierarchy &&
                _uiStartWaveButton.interactable)
            {
                fallback = _uiStartWaveButton;
            }

            if (fallback != null)
            {
                EventSystem.current.SetSelectedGameObject(fallback.gameObject);
            }
        }

        private Sprite LoadUiSprite(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            if (_uiSpriteCache.TryGetValue(resourcePath, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = TDUiP132Art.LoadVirtualSprite(resourcePath);
            sprite ??= Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                    sprite.name = $"P11 UI {texture.name}";
                }
            }

            if (sprite != null)
            {
                _uiSpriteCache[resourcePath] = sprite;
            }

            return sprite;
        }

        private static TDUiTextRole ResolveUiTextRole(int requestedSize)
        {
            if (requestedSize >= 20)
            {
                return TDUiTextRole.ScreenTitle;
            }

            if (requestedSize >= 16)
            {
                return TDUiTextRole.SectionTitle;
            }

            if (requestedSize >= 13)
            {
                return TDUiTextRole.PanelTitle;
            }

            if (requestedSize >= 12)
            {
                return TDUiTextRole.Metric;
            }

            return requestedSize >= 11 ? TDUiTextRole.Body : TDUiTextRole.Caption;
        }

        private static int GetUiRoleFontSize(TDUiTextRole role)
        {
            return role switch
            {
                TDUiTextRole.ScreenTitle => 20,
                TDUiTextRole.SectionTitle => 17,
                TDUiTextRole.PanelTitle => 15,
                TDUiTextRole.Metric => 13,
                TDUiTextRole.Body => 12,
                _ => 11
            };
        }

        private void UpdateRoutePreview()
        {
            if (!_debugRoutePreviewVisible || _gameOver || _missionBoardOpen || !_isInPrepPhase ||
                _currentWaveDefinition == null || _activeLanePaths.Count == 0)
            {
                HideRoutePreview();
                return;
            }

            var lanes = BuildWavePreviewLaneKeys(_currentWaveDefinition);
            if (lanes.Count == 0)
            {
                HideRoutePreview();
                return;
            }

            EnsureRoutePreviewRoot();
            var lanePressure = BuildWaveLanePressureMap(_currentWaveDefinition);
            var maxPressure = 1;
            for (var i = 0; i < lanes.Count; i++)
            {
                maxPressure = Mathf.Max(maxPressure, GetLanePressure(lanePressure, lanes[i]));
            }

            var visible = 0;
            for (var i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                if (!_activeLanePaths.TryGetValue(lane, out var path) || path == null || path.Count <= 1)
                {
                    continue;
                }

                var line = GetOrCreateRoutePreviewLine(visible);
                ConfigureRoutePreviewLine(line, path, visible, GetLanePressure(lanePressure, lane), maxPressure);
                visible++;
            }

            for (var i = visible; i < _routePreviewLines.Count; i++)
            {
                if (_routePreviewLines[i] != null)
                {
                    _routePreviewLines[i].gameObject.SetActive(false);
                }
            }
        }

        private void HideRoutePreview()
        {
            for (var i = 0; i < _routePreviewLines.Count; i++)
            {
                if (_routePreviewLines[i] != null)
                {
                    _routePreviewLines[i].gameObject.SetActive(false);
                }
            }
        }

        private void EnsureRoutePreviewRoot()
        {
            if (_routePreviewRoot != null)
            {
                return;
            }

            var root = new GameObject("RoutePreview");
            root.transform.SetParent(transform, false);
            _routePreviewRoot = root.transform;
        }

        private LineRenderer GetOrCreateRoutePreviewLine(int index)
        {
            while (_routePreviewLines.Count <= index)
            {
                EnsureRoutePreviewRoot();
                var lineObject = new GameObject($"RoutePreview_{_routePreviewLines.Count:00}");
                lineObject.transform.SetParent(_routePreviewRoot, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = false;
                line.numCapVertices = 4;
                line.numCornerVertices = 3;
                line.textureMode = LineTextureMode.Stretch;
                line.alignment = LineAlignment.View;
                line.sharedMaterial = GetRoutePreviewMaterial();
                line.sortingOrder = 8;
                _routePreviewLines.Add(line);
            }

            return _routePreviewLines[index];
        }

        private void ConfigureRoutePreviewLine(LineRenderer line, IReadOnlyList<Vector3> path, int laneIndex, int pressure, int maxPressure)
        {
            if (line == null || path == null || path.Count <= 1)
            {
                return;
            }

            line.gameObject.SetActive(true);
            line.positionCount = path.Count;
            for (var i = 0; i < path.Count; i++)
            {
                var point = path[i];
                point.z = -0.05f;
                line.SetPosition(i, point);
            }

            var color = GetRoutePreviewColor(laneIndex);
            var pressureT = Mathf.Clamp01(pressure / (float)Mathf.Max(1, maxPressure));
            color.a = Mathf.Lerp(color.a * 0.44f, 0.42f, pressureT);
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.48f);
            line.startWidth = Mathf.Lerp(0.035f, 0.075f, pressureT);
            line.endWidth = Mathf.Lerp(0.024f, 0.052f, pressureT);
        }

        private Color GetRoutePreviewColor(int index)
        {
            if (_colorblindMarkersEnabled)
            {
                return (index % 4) switch
                {
                    0 => new Color(1f, 0.76f, 0.16f, 0.52f),
                    1 => new Color(0.20f, 0.72f, 1f, 0.48f),
                    2 => new Color(0.24f, 0.94f, 0.58f, 0.46f),
                    _ => new Color(0.94f, 0.42f, 0.82f, 0.48f)
                };
            }

            return (index % 4) switch
            {
                0 => new Color(0.98f, 0.82f, 0.32f, 0.42f),
                1 => new Color(0.44f, 0.88f, 1f, 0.38f),
                2 => new Color(0.66f, 1f, 0.62f, 0.36f),
                _ => new Color(1f, 0.56f, 0.38f, 0.38f)
            };
        }

        private Material GetRoutePreviewMaterial()
        {
            if (_routePreviewMaterial != null)
            {
                return _routePreviewMaterial;
            }

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default");
            if (shader == null)
            {
                return null;
            }

            _routePreviewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return _routePreviewMaterial;
        }

        private void ConfigureCamera()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                _mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            _mainCamera.orthographic = true;
            var safeAspect = Mathf.Max(1f, Screen.width / (float)Mathf.Max(1, Screen.height));
            var widthFitSize = 8.5f / safeAspect;
            _mainCamera.orthographicSize = Mathf.Max(4.8f, widthFitSize);
            _mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = new Color(0.035f, 0.038f, 0.040f);
        }

        private static void ConfigureLoopSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
        }

        private void RefreshLoadError()
        {
            var errors = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(_campaignError))
            {
                errors.Add(_campaignError);
            }

            if (!string.IsNullOrWhiteSpace(_enemyCatalogError))
            {
                errors.Add(_enemyCatalogError);
            }

            if (!string.IsNullOrWhiteSpace(_waveError))
            {
                errors.Add(_waveError);
            }

            if (errors.Count > 0)
            {
                _loadError = string.Join(" | ", errors);
                return;
            }

            _loadError = string.Empty;
        }

        private bool IsOpeningWaveBuildRequired()
        {
            return _wave <= 1 && _wavesCleared == 0 && _builtTowerCount <= 0;
        }

        private string GetCampaignHudLabel()
        {
            if (_campaignRoute?.level == null)
            {
                return "Campaign route: fallback wave set (single-map mode)";
            }

            var level = _campaignRoute.level;
            var mapLabel = _campaignRoute.map != null && !string.IsNullOrWhiteSpace(_campaignRoute.map.displayName)
                ? _campaignRoute.map.displayName
                : level.mapId;

            return $"Campaign L{level.levelIndex:00}/{_campaignRoute.totalLevels:00}  {level.levelId}  Map {mapLabel}";
        }

        private string GetCompactCampaignHudLabel()
        {
            if (_campaignRoute?.level == null)
            {
                return "SKIRMISH";
            }

            var level = _campaignRoute.level;
            var mapLabel = _campaignRoute.map != null && !string.IsNullOrWhiteSpace(_campaignRoute.map.displayName)
                ? _campaignRoute.map.displayName
                : level.mapId;
            var cleanMap = string.IsNullOrWhiteSpace(mapLabel) ? "MAP" : mapLabel.ToUpperInvariant();
            return $"L{level.levelIndex:00}  {cleanMap}";
        }

        private void TrySellSelectedTowerFromUi()
        {
            TrySellTower(GetUiFocusedTower());
        }

        private void RestartCurrentScene()
        {
            // Reset the current level in-place (no scene reload — avoids blue screen).
            if (_pauseMenu != null && _pauseMenu.IsVisible) HandlePauseResume();
            EnterLevelInPlace();
        }

        /// <summary>
        /// Show the loading screen, yield one frame to render it, then reload the scene.
        /// The loadingVerb controls the text ("DEPLOYING" / "RESTARTING" / etc.).
        /// The levelLabel shows the target level name (null = use current).
        /// </summary>
        private void LoadingTransition(string loadingVerb, string levelLabel)
        {
            var label = levelLabel;
            if (string.IsNullOrEmpty(label) && _campaignRoute?.level != null)
            {
                var map = _campaignRoute.map;
                label = map != null && !string.IsNullOrWhiteSpace(map.displayName)
                    ? map.displayName
                    : _campaignRoute.level.mapId;
                label = $"L{_campaignRoute.level.levelIndex:00}  {label}";
            }

            if (_loadingScreen != null)
            {
                StartCoroutine(LoadingTransitionRoutine(loadingVerb, label));
            }
            else
            {
                DoSceneReload();
            }
        }

        private IEnumerator LoadingTransitionRoutine(string loadingVerb, string label)
        {
            _loadingScreen.Show(label, loadingVerb);
            // Yield twice: once to ensure the Canvas renders the overlay,
            // once more for safety margin before the synchronous LoadScene blocks.
            yield return null;
            yield return null;
            DoSceneReload();
        }

        private void DoSceneReload()
        {
            Time.timeScale = Mathf.Max(1f, _lastActivePlaybackSpeed);
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }

            SceneManager.LoadScene(scene.name);
        }

        private static string GetTowerKindLabel(TDTowerKind kind)
        {
            return TDTower.GetDisplayName(kind);
        }

        private static string GetUpgradeBranchLabel(TDTowerUpgradeBranch branch)
        {
            return branch == TDTowerUpgradeBranch.Damage ? "Damage" : "Utility";
        }

        private static Vector2 GetTowerColliderSize(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => new Vector2(0.44f, 0.44f),
                TDTowerKind.CinderMortar => new Vector2(0.48f, 0.48f),
                TDTowerKind.FrostCoil => new Vector2(0.42f, 0.42f),
                TDTowerKind.ArcWelder => new Vector2(0.44f, 0.44f),
                TDTowerKind.SiegeDrill => new Vector2(0.47f, 0.47f),
                TDTowerKind.EmberFlak => new Vector2(0.43f, 0.43f),
                TDTowerKind.ResonanceBeacon => new Vector2(0.46f, 0.46f),
                TDTowerKind.GravSnare => new Vector2(0.48f, 0.48f),
                _ => new Vector2(0.45f, 0.45f)
            };
        }

        private static Vector2 GetTowerColliderOffset(TDTowerKind kind)
        {
            return kind switch
            {
                TDTowerKind.RailLancer => new Vector2(0f, -0.04f),
                TDTowerKind.CinderMortar => new Vector2(0f, -0.03f),
                TDTowerKind.FrostCoil => new Vector2(0f, -0.02f),
                TDTowerKind.ArcWelder => new Vector2(0f, -0.03f),
                TDTowerKind.SiegeDrill => new Vector2(0f, -0.03f),
                TDTowerKind.EmberFlak => new Vector2(0f, -0.03f),
                TDTowerKind.ResonanceBeacon => new Vector2(0f, -0.03f),
                TDTowerKind.GravSnare => new Vector2(0f, -0.03f),
                _ => Vector2.zero
            };
        }

        private static Vector3 GetEnemyShadowOffset(string enemyId)
        {
            return enemyId switch
            {
                "skitter_runner" => new Vector3(0f, -0.21f, 0f),
                "carapace_brute" => new Vector3(0f, -0.19f, 0f),
                "ash_swarm" => new Vector3(0f, -0.18f, 0f),
                "plated_spore" => new Vector3(0f, -0.20f, 0f),
                "burrow_sapper" => new Vector3(0f, -0.20f, 0f),
                "ember_leech" => new Vector3(0f, -0.20f, 0f),
                "spore_carrier" => new Vector3(0f, -0.20f, 0f),
                "rail_warden" => new Vector3(0f, -0.20f, 0f),
                "cinder_glider" => new Vector3(0f, -0.21f, 0f),
                "husk_titan" => new Vector3(0f, -0.18f, 0f),
                "echo_mimic" => new Vector3(0f, -0.20f, 0f),
                "furnace_matriarch" => new Vector3(0f, -0.16f, 0f),
                _ => new Vector3(0f, -0.20f, 0f)
            };
        }

        private static Vector3 ResolveEnemyFootShadowOffset(
            SpriteRenderer visualRenderer,
            Transform visualTransform,
            string enemyId)
        {
            if (visualRenderer == null || visualRenderer.sprite == null || visualTransform == null)
            {
                return GetEnemyShadowOffset(enemyId);
            }

            // The visual's opaque bottom is anchored exactly on the route line,
            // so the shadow sits a hair below the feet to read as ground contact.
            return new Vector3(visualTransform.localPosition.x, -GetEnemyFootShadowLift(enemyId), 0f);
        }

        private static void AlignEnemyVisualToRouteAnchor(
            SpriteRenderer visualRenderer,
            Transform visualTransform,
            string enemyId)
        {
            if (visualRenderer == null || visualRenderer.sprite == null || visualTransform == null)
            {
                return;
            }

            var authoredOffset = GetEnemyVisualOffset(enemyId);
            var anchoredLocalY = TDArtLibrary.ResolveFootAnchorLocalY(
                visualRenderer.sprite,
                visualTransform.localScale.y);
            visualTransform.localPosition = new Vector3(authoredOffset.x, anchoredLocalY, authoredOffset.z);
        }

        private static float GetEnemyFootShadowLift(string enemyId)
        {
            return enemyId switch
            {
                "husk_titan" => 0.040f,
                "furnace_matriarch" => 0.048f,
                "carapace_brute" => 0.034f,
                _ => 0.026f
            };
        }

        private Vector3 ResolveSpriteScale(Sprite sprite, float targetCellCoverage)
        {
            if (sprite == null)
            {
                return Vector3.one;
            }

            var spriteWidth = Mathf.Max(0.0001f, sprite.bounds.size.x);
            var targetWidth = Mathf.Max(0.1f, CellSize * Mathf.Clamp(targetCellCoverage, 0.1f, 2f));
            return Vector3.one * (targetWidth / spriteWidth);
        }

        private int GetConfiguredWaveCount()
        {
            return _waveSet?.waves?.Length ?? _wave;
        }

        private void SetStatus(string message)
        {
            _lastStatus = message;
            _statusTimer = 2.5f;
        }

        private bool IsBuildWindowOpen()
        {
            if (AllowBuildAndUpgradeDuringCombat)
            {
                return true;
            }

            if (_waveSet == null)
            {
                return true;
            }

            return _wave == 0 || _isInPrepPhase;
        }

        private void UpdateBuildPreviewUnderCursor()
        {
            if (_p133FixtureActive)
            {
                return;
            }

            var previousHoveredTower = _hoveredTower;
            _hoveredTower = null;
            if (previousHoveredTower != null)
            {
                previousHoveredTower.Readability?.SetInteractionState(
                    false,
                    previousHoveredTower == _selectedTowerForUi);
            }

            if (_mainCamera == null || _gridMap == null)
            {
                HideRangePreview();
                return;
            }

            // Same pointer hit decision as the click path (TD-GP-002): while
            // the gamepad cursor owns the pointer, the idle real mouse parked
            // over a HUD panel must not kill hover/tooltip/build-ghost.
            var pointerOverUi = _gamepadCursorMode ? _gamepadVirtualPointerOverUi : IsPointerOverBattleUi();
            if (pointerOverUi)
            {
                _gridMap.HideBuildPreview();
                HideRangePreview();
                UpdateTowerTooltip(null);
                return;
            }

            var mouse = TDInputCompat.MousePosition;
            mouse.z = -_mainCamera.transform.position.z;
            var world = _mainCamera.ScreenToWorldPoint(mouse);
            world.z = 0f;

            if (TryGetTowerUnderCursor(world, out var tower))
            {
                if (_hoveredTower != tower && _lastHoverSfxTower != tower)
                {
                    _lastHoverSfxTower = tower;
                    PlaySfxTone("ui_hover", 620f, 0.045f, 0.22f, true);
                }

                _hoveredTower = tower;
                tower.Readability?.SetInteractionState(true, tower == _selectedTowerForUi);
                UpdateTowerTooltip(tower);
                _gridMap.HideBuildPreview();
                if (tower == _selectedTowerForUi)
                {
                    ShowRangePreview(
                        tower.transform.position,
                        tower.AttackRange,
                        new Color(1f, 0.68f, 0.28f, 0.38f));
                }
                else
                {
                    HideRangePreview();
                }
                return;
            }

            if (!IsBuildWindowOpen() || !IsTowerUnlocked(_selectedTowerKind))
            {
                _gridMap.HideBuildPreview();
                HideRangePreview();
                UpdateTowerTooltip(null);
                return;
            }

            _gridMap.UpdateBuildPreview(world);
            if (_gridMap.TryWorldToCell(world, out var cell) && _gridMap.IsBuildable(cell))
            {
                ShowRangePreview(
                    _gridMap.CellToBuildWorld(cell),
                    TDTower.GetBaseRange(_selectedTowerKind),
                    new Color(0.42f, 0.86f, 0.66f, 0.26f));
                UpdateTowerTooltip(null);
                return;
            }

            HideRangePreview();
            UpdateTowerTooltip(null);
        }

        private void UpdateTowerTooltip(TDTower tower)
        {
            if (_towerTooltip == null && tower != null && _battleCanvas != null)
            {
                _towerTooltip = TDTowerTooltip.Create(_battleCanvas.transform);
            }

            if (_towerTooltip != null)
            {
                if (tower != null)
                {
                    _towerTooltip.HoverTower(tower);
                }
                else
                {
                    _towerTooltip.ClearHover();
                }
            }
        }

        private void EnsureRangePreview()
        {
            if (_rangePreviewRenderer != null)
            {
                return;
            }

            var root = new GameObject("RangePreview");
            root.transform.SetParent(transform, false);
            root.transform.position = Vector3.zero;
            _rangePreviewRoot = root.transform;

            _rangePreviewRenderer = root.AddComponent<SpriteRenderer>();
            _rangePreviewRenderer.sprite = GetOrCreateRangePreviewSprite();
            _rangePreviewRenderer.sortingOrder = TDWorldVisualOrder.RangePreview;
            _rangePreviewRenderer.enabled = false;
        }

        private void ShowRangePreview(Vector3 center, float radius, Color color)
        {
            if (radius <= 0f)
            {
                HideRangePreview();
                return;
            }

            EnsureRangePreview();
            if (_rangePreviewRoot == null || _rangePreviewRenderer == null)
            {
                return;
            }

            _rangePreviewRoot.position = center;
            _rangePreviewRoot.localScale = Vector3.one * radius;
            _rangePreviewRenderer.color = color;
            _rangePreviewRenderer.enabled = true;
        }

        private void HideRangePreview()
        {
            if (_rangePreviewRenderer != null)
            {
                _rangePreviewRenderer.enabled = false;
            }
        }

        private Sprite GetOrCreateRangePreviewSprite()
        {
            if (_rangePreviewSprite != null)
            {
                return _rangePreviewSprite;
            }

            const int size = 256;
            const float center = (size - 1) * 0.5f;
            const float outerRadius = 121f;
            const float ringHalfWidth = 0.55f;
            const float featherWidth = 0.75f;
            const float segmentCount = 32f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    var ringDistance = Mathf.Abs(distance - outerRadius);
                    var ringAlpha = 1f - Mathf.Clamp01((ringDistance - ringHalfWidth) / featherWidth);
                    var angle01 = Mathf.Repeat((Mathf.Atan2(dy, dx) + Mathf.PI) / (Mathf.PI * 2f), 1f);
                    var dashPhase = Mathf.Repeat(angle01 * segmentCount, 1f);
                    var dashAlpha = dashPhase <= 0.52f ? 1f : 0.12f;
                    var alpha = ringAlpha * dashAlpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            _rangePreviewSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
            return _rangePreviewSprite;
        }
    }
}
