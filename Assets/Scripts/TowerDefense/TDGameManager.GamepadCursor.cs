using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TD
{
    // Gamepad virtual cursor for the battle board: left stick moves a screen
    // pointer, South acts as the left click (open the build wheel / select a
    // tower), East cancels the wheel, and D-pad Left/Right upgrade the focused
    // tower down the Damage/Utility branch. Modal screens (title, world map,
    // mission board, pause, settings) keep the regular EventSystem focus
    // navigation — the cursor only owns the in-battle board, where focus
    // navigation alone cannot reach arbitrary build sites.
    public sealed partial class TDGameManager
    {
        private bool _gamepadCursorMode;
        private Vector2 _gamepadCursorPosition;
        private Vector3 _lastRealMousePosition = new(-9999f, -9999f, 0f);
        private bool _gamepadVirtualClick;
        private bool _gamepadVirtualPointerOverUi;
        private bool _gamepadCursorHintShown;
        private Image _gamepadCursorVisual;
        private readonly List<RaycastResult> _gamepadPointerRaycasts = new();

        private void UpdateGamepadCursorInput()
        {
            _gamepadVirtualClick = false;
            _gamepadVirtualPointerOverUi = false;

            if (IsBattleInteractionBlockedForGamepad())
            {
                SetGamepadCursorMode(false);
                return;
            }

            // Real mouse activity hands the pointer back to the OS cursor.
            // Clicks must release the override before the shared click handling
            // runs, so the click lands where the mouse aims.
            var rawMouse = TDInputCompat.MousePositionRaw;
            var mouseMoved = (rawMouse - _lastRealMousePosition).sqrMagnitude > 4f;
            _lastRealMousePosition = rawMouse;
            var mousePressed = TDInputCompat.GetMouseButtonDown(0) || TDInputCompat.GetMouseButtonDown(1);
            if (mouseMoved || mousePressed)
            {
                SetGamepadCursorMode(false);
            }

            var stick = TDInputCompat.GetGamepadLeftStick();
            var anyGamepadButton = TDInputCompat.GetAnyGamepadButtonDown();

            if (!_gamepadCursorMode)
            {
                if (stick.sqrMagnitude < 0.09f && !anyGamepadButton)
                {
                    return;
                }

                SetGamepadCursorMode(true);
                _gamepadCursorPosition = ClampToScreen(rawMouse);
                if (anyGamepadButton)
                {
                    // The wake-up press must not also act as a click this frame.
                    return;
                }
            }

            if (_radialTowerMenu != null && _radialTowerMenu.IsVisible)
            {
                UpdateGamepadRadialMenu();
                return;
            }

            if (stick.sqrMagnitude > 0.001f)
            {
                var speed = Screen.height * 1.15f;
                _gamepadCursorPosition += stick * (speed * Time.unscaledDeltaTime);
                _gamepadCursorPosition = ClampToScreen(_gamepadCursorPosition);
            }

            TDInputCompat.SetVirtualPointer(_gamepadCursorPosition);
            _gamepadVirtualPointerOverUi = IsVirtualPointerOverUi(_gamepadCursorPosition);

            if (TDInputCompat.GetGamepadButtonDown(TDGamepadButton.South))
            {
                // TD-GP-001: over UI, South acts as the pointer's click so
                // buttons (Start Wave, panel actions) are reachable by gamepad
                // — focus navigation is unavailable in cursor mode by design.
                // Over the board it stays the build/select virtual click.
                if (_gamepadVirtualPointerOverUi)
                {
                    TryClickUiAtVirtualPointer();
                }
                else
                {
                    _gamepadVirtualClick = true;
                }
            }

            if (TDInputCompat.GetGamepadButtonDown(TDGamepadButton.DpadLeft))
            {
                TryUpgradeSelectedTowerFromUi(TDTowerUpgradeBranch.Damage);
            }
            else if (TDInputCompat.GetGamepadButtonDown(TDGamepadButton.DpadRight))
            {
                TryUpgradeSelectedTowerFromUi(TDTowerUpgradeBranch.Utility);
            }
            else if (TDInputCompat.GetGamepadButtonDown(TDGamepadButton.DpadDown))
            {
                TrySellSelectedTowerFromUi();
            }
        }

        private void UpdateGamepadRadialMenu()
        {
            var direction = TDInputCompat.GetGamepadNavigationVector();
            if (direction.sqrMagnitude > 0.2f)
            {
                _radialTowerMenu.HandleGamepadNavigation(direction);
            }

            if (TDInputCompat.GetGamepadButtonDown(TDGamepadButton.South))
            {
                if (!_radialTowerMenu.HasGamepadSelection)
                {
                    // First press without a direction held: surface the wheel
                    // selection instead of doing nothing.
                    _radialTowerMenu.HighlightFirstGamepadSlot();
                    return;
                }

                if (_radialTowerMenu.TryConfirmGamepadSelection(out var locked))
                {
                    if (locked)
                    {
                        SetStatus("Tower is locked or unaffordable.");
                        PlaySfxTone("ui_deny", 180f, 0.08f, 0.30f, true);
                    }
                }
            }
            else if (TDInputCompat.GetGamepadButtonDown(TDGamepadButton.East))
            {
                _radialTowerMenu.Hide();
                PlaySfxTone("ui_hover", 520f, 0.04f, 0.20f, true);
            }
        }

        private void SetGamepadCursorMode(bool active)
        {
            if (_gamepadCursorMode == active)
            {
                return;
            }

            _gamepadCursorMode = active;
            if (active)
            {
                // Without this, South would submit whatever Selectable still
                // holds focus (e.g. Start Wave) on top of the virtual click.
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }

                if (!_gamepadCursorHintShown)
                {
                    _gamepadCursorHintShown = true;
                    SetStatus("Gamepad: L-stick cursor, A build/select, D-pad L/R upgrade, D-pad Down sell.");
                }

                EnsureGamepadCursorVisual();
            }

            TDInputCompat.ClearVirtualPointer();
            UpdateGamepadCursorVisual();
        }

        private void EnsureGamepadCursorVisual()
        {
            if (_gamepadCursorVisual != null || _battleCanvas == null)
            {
                return;
            }

            var cursorObject = new GameObject("TD Gamepad Cursor", typeof(RectTransform));
            cursorObject.transform.SetParent(_battleCanvas.transform, false);
            var rect = (RectTransform)cursorObject.transform;
            rect.sizeDelta = new Vector2(30f, 30f);

            var ring = cursorObject.AddComponent<Image>();
            ring.sprite = TDArtLibrary.GetSoftRingSprite();
            ring.color = new Color(1f, 0.62f, 0.24f, 0.95f);
            ring.raycastTarget = false;
            _gamepadCursorVisual = ring;

            var center = new Vector2(0.5f, 0.5f);
            var dotRect = CreateUiRect("Dot", rect, center, center, center, Vector2.zero, new Vector2(8f, 8f));
            var dot = dotRect.gameObject.AddComponent<Image>();
            dot.color = new Color(1f, 0.85f, 0.65f, 1f);
            dot.raycastTarget = false;
        }

        private void UpdateGamepadCursorVisual()
        {
            if (_gamepadCursorVisual == null)
            {
                return;
            }

            var visible = _gamepadCursorMode && !IsBattleInteractionBlockedForGamepad();
            if (_gamepadCursorVisual.gameObject.activeSelf != visible)
            {
                _gamepadCursorVisual.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            _gamepadCursorVisual.transform.SetAsLastSibling();
            if (_battleCanvas != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_battleCanvas.transform, _gamepadCursorPosition, null, out var localPoint))
            {
                _gamepadCursorVisual.rectTransform.localPosition = localPoint;
            }

            var pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
            _gamepadCursorVisual.transform.localScale = new Vector3(pulse, pulse, 1f);
        }

        private bool IsVirtualPointerOverUi(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            _gamepadPointerRaycasts.Clear();
            var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
            eventSystem.RaycastAll(pointerData, _gamepadPointerRaycasts);
            return _gamepadPointerRaycasts.Count > 0;
        }

        /// <summary>
        /// Synthesize a full pointer click (down/up/click) on the first
        /// clickable handler under the virtual cursor. Buttons driven this way
        /// fire the exact onClick path a real mouse click takes.
        /// </summary>
        private bool TryClickUiAtVirtualPointer()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            _gamepadPointerRaycasts.Clear();
            var pointerData = new PointerEventData(eventSystem)
            {
                position = _gamepadCursorPosition,
                button = PointerEventData.InputButton.Left
            };
            eventSystem.RaycastAll(pointerData, _gamepadPointerRaycasts);
            for (var i = 0; i < _gamepadPointerRaycasts.Count; i++)
            {
                var hit = _gamepadPointerRaycasts[i].gameObject;
                if (hit == null)
                {
                    continue;
                }

                // Raycasts often land on child graphics (labels, icons) —
                // resolve up to the owning clickable.
                var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
                if (handler == null)
                {
                    continue;
                }

                // Fill the fields real handlers expect: the raycast result
                // (custom IPointerClickHandlers read pointerCurrentRaycast)
                // and the press position (drag/press semantics).
                pointerData.pointerCurrentRaycast = _gamepadPointerRaycasts[i];
                pointerData.pressPosition = _gamepadCursorPosition;
                if (pointerData.pointerEnter == null)
                {
                    pointerData.pointerEnter = hit;
                }

                ExecuteEvents.Execute(handler, pointerData, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(handler, pointerData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(handler, pointerData, ExecuteEvents.pointerClickHandler);

                // Selectable.OnPointerDown (with navigation) re-acquires
                // event-system focus; the InputSystemUIInputModule's default
                // Submit action is also bound to South, so from the second
                // press onwards every South would fire BOTH the module's
                // submit onClick and this synthetic click. Drop the focus the
                // down-event just grabbed.
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }

                return true;
            }

            return false;
        }

        private bool IsBattleInteractionBlockedForGamepad()
        {
            return (_worldMap != null && _worldMap.IsVisible) ||
                   (_titleScreen != null && _titleScreen.IsVisible) ||
                   (_missionBriefing != null && _missionBriefing.IsVisible) ||
                   (_pauseMenu != null && _pauseMenu.IsVisible) ||
                   (_settingsPanel != null && _settingsPanel.IsOpen) ||
                   _gameOver ||
                   _missionBoardOpen ||
                   _formationPanelOpen ||
                   _campaignProfileOpen;
        }

        private static Vector2 ClampToScreen(Vector2 position)
        {
            return new Vector2(
                Mathf.Clamp(position.x, 0f, Screen.width),
                Mathf.Clamp(position.y, 0f, Screen.height));
        }
    }
}
