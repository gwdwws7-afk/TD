using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TD
{
    public enum TDGamepadButton
    {
        South = 0,
        East = 1,
        West = 2,
        North = 3,
        Start = 4,
        Select = 5,
        LeftShoulder = 6,
        RightShoulder = 7,
        DpadUp = 8,
        DpadDown = 9,
        DpadLeft = 10,
        DpadRight = 11
    }

    public static class TDInputCompat
    {
        public static bool GetKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null || !TryMapKey(key, out var mappedKey))
            {
                return false;
            }

            var control = keyboard[mappedKey];
            return control != null && control.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        public static bool GetMouseButtonDown(int button)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            return button switch
            {
                0 => mouse.leftButton.wasPressedThisFrame,
                1 => mouse.rightButton.wasPressedThisFrame,
                2 => mouse.middleButton.wasPressedThisFrame,
                _ => false
            };
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }

        // --- Virtual pointer (gamepad cursor) ---
        // While the gamepad cursor is active, MousePosition reports the virtual
        // cursor so every existing pointer consumer (build preview, tower hover,
        // tooltip, radial menu placement) follows it without per-call-site changes.
        private static Vector2 _virtualPointerPosition;
        private static bool _virtualPointerActive;

        public static bool VirtualPointerActive => _virtualPointerActive;

        public static void SetVirtualPointer(Vector2 screenPosition)
        {
            _virtualPointerPosition = screenPosition;
            _virtualPointerActive = true;
        }

        public static void ClearVirtualPointer()
        {
            _virtualPointerActive = false;
        }

        public static Vector3 MousePosition
        {
            get
            {
                if (_virtualPointerActive)
                {
                    return new Vector3(_virtualPointerPosition.x, _virtualPointerPosition.y, 0f);
                }

                return MousePositionRaw;
            }
        }

        // Raw OS mouse position, ignoring the virtual pointer override — callers
        // that manage the override itself must read this, not MousePosition.
        public static Vector3 MousePositionRaw
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse == null)
                {
                    return Vector3.zero;
                }

                var pos = mouse.position.ReadValue();
                return new Vector3(pos.x, pos.y, 0f);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                return Vector3.zero;
#endif
            }
        }

        public static bool HasGamepad
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Gamepad.current != null;
#else
                return false;
#endif
            }
        }

        public static bool GetGamepadButtonDown(TDGamepadButton button)
        {
#if ENABLE_INPUT_SYSTEM
            var gamepad = Gamepad.current;
            if (gamepad == null)
            {
                return false;
            }

            return button switch
            {
                TDGamepadButton.South => gamepad.buttonSouth.wasPressedThisFrame,
                TDGamepadButton.East => gamepad.buttonEast.wasPressedThisFrame,
                TDGamepadButton.West => gamepad.buttonWest.wasPressedThisFrame,
                TDGamepadButton.North => gamepad.buttonNorth.wasPressedThisFrame,
                TDGamepadButton.Start => gamepad.startButton.wasPressedThisFrame,
                TDGamepadButton.Select => gamepad.selectButton.wasPressedThisFrame,
                TDGamepadButton.LeftShoulder => gamepad.leftShoulder.wasPressedThisFrame,
                TDGamepadButton.RightShoulder => gamepad.rightShoulder.wasPressedThisFrame,
                TDGamepadButton.DpadUp => gamepad.dpad.up.wasPressedThisFrame,
                TDGamepadButton.DpadDown => gamepad.dpad.down.wasPressedThisFrame,
                TDGamepadButton.DpadLeft => gamepad.dpad.left.wasPressedThisFrame,
                TDGamepadButton.DpadRight => gamepad.dpad.right.wasPressedThisFrame,
                _ => false
            };
#else
            return false;
#endif
        }

        public static bool GetGamepadNavigationDown()
        {
#if ENABLE_INPUT_SYSTEM
            var gamepad = Gamepad.current;
            return gamepad != null &&
                   (gamepad.dpad.up.wasPressedThisFrame ||
                    gamepad.dpad.down.wasPressedThisFrame ||
                    gamepad.dpad.left.wasPressedThisFrame ||
                    gamepad.dpad.right.wasPressedThisFrame ||
                    gamepad.leftStick.up.wasPressedThisFrame ||
                    gamepad.leftStick.down.wasPressedThisFrame ||
                    gamepad.leftStick.left.wasPressedThisFrame ||
                    gamepad.leftStick.right.wasPressedThisFrame);
#else
            return false;
#endif
        }

        public static Vector2 GetGamepadLeftStick(float deadzone = 0.18f)
        {
#if ENABLE_INPUT_SYSTEM
            var gamepad = Gamepad.current;
            if (gamepad == null)
            {
                return Vector2.zero;
            }

            var value = gamepad.leftStick.ReadValue();
            var magnitude = value.magnitude;
            if (magnitude < deadzone)
            {
                return Vector2.zero;
            }

            // Rescale above the deadzone so cursor speed ramps 0→1 across the
            // usable range instead of jumping on first contact.
            var normalized = (magnitude - deadzone) / (1f - deadzone);
            return value / magnitude * Mathf.Min(1f, normalized);
#else
            return Vector2.zero;
#endif
        }

        // Normalized selection direction for radial-style menus: the left stick
        // when pushed, otherwise the dpad axes.
        public static Vector2 GetGamepadNavigationVector()
        {
#if ENABLE_INPUT_SYSTEM
            var stick = GetGamepadLeftStick();
            if (stick.sqrMagnitude > 0.04f)
            {
                return stick.normalized;
            }

            var gamepad = Gamepad.current;
            if (gamepad == null)
            {
                return Vector2.zero;
            }

            var direction = Vector2.zero;
            if (gamepad.dpad.left.isPressed)
            {
                direction.x -= 1f;
            }

            if (gamepad.dpad.right.isPressed)
            {
                direction.x += 1f;
            }

            if (gamepad.dpad.up.isPressed)
            {
                direction.y += 1f;
            }

            if (gamepad.dpad.down.isPressed)
            {
                direction.y -= 1f;
            }

            return direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.zero;
#else
            return Vector2.zero;
#endif
        }

        public static bool GetAnyGamepadButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            var gamepad = Gamepad.current;
            if (gamepad == null)
            {
                return false;
            }

            return gamepad.buttonSouth.wasPressedThisFrame ||
                   gamepad.buttonEast.wasPressedThisFrame ||
                   gamepad.buttonWest.wasPressedThisFrame ||
                   gamepad.buttonNorth.wasPressedThisFrame ||
                   gamepad.startButton.wasPressedThisFrame ||
                   gamepad.selectButton.wasPressedThisFrame ||
                   gamepad.leftShoulder.wasPressedThisFrame ||
                   gamepad.rightShoulder.wasPressedThisFrame ||
                   gamepad.dpad.up.wasPressedThisFrame ||
                   gamepad.dpad.down.wasPressedThisFrame ||
                   gamepad.dpad.left.wasPressedThisFrame ||
                   gamepad.dpad.right.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool TryGetPressedKeyCode(out KeyCode keyCode)
        {
            keyCode = KeyCode.None;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            foreach (var keyControl in keyboard.allKeys)
            {
                if (keyControl != null && keyControl.wasPressedThisFrame && TryMapInputKey(keyControl.keyCode, out keyCode))
                {
                    return true;
                }
            }

            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.anyKeyDown)
            {
                return false;
            }

            foreach (KeyCode candidate in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (candidate != KeyCode.None && Input.GetKeyDown(candidate))
                {
                    keyCode = candidate;
                    return true;
                }
            }

            return false;
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryMapKey(KeyCode keyCode, out Key key)
        {
            key = keyCode switch
            {
                KeyCode.R => Key.R,
                KeyCode.A => Key.A,
                KeyCode.B => Key.B,
                KeyCode.C => Key.C,
                KeyCode.D => Key.D,
                KeyCode.Q => Key.Q,
                KeyCode.E => Key.E,
                KeyCode.F => Key.F,
                KeyCode.G => Key.G,
                KeyCode.H => Key.H,
                KeyCode.I => Key.I,
                KeyCode.J => Key.J,
                KeyCode.K => Key.K,
                KeyCode.L => Key.L,
                KeyCode.M => Key.M,
                KeyCode.N => Key.N,
                KeyCode.O => Key.O,
                KeyCode.P => Key.P,
                KeyCode.S => Key.S,
                KeyCode.T => Key.T,
                KeyCode.U => Key.U,
                KeyCode.V => Key.V,
                KeyCode.W => Key.W,
                KeyCode.Z => Key.Z,
                KeyCode.X => Key.X,
                KeyCode.Y => Key.Y,
                KeyCode.Space => Key.Space,
                KeyCode.Escape => Key.Escape,
                KeyCode.Return => Key.Enter,
                KeyCode.KeypadEnter => Key.NumpadEnter,
                KeyCode.Tab => Key.Tab,
                KeyCode.Pause => Key.Pause,
                KeyCode.Minus => Key.Minus,
                KeyCode.Equals => Key.Equals,
                KeyCode.KeypadMinus => Key.NumpadMinus,
                KeyCode.KeypadPlus => Key.NumpadPlus,
                KeyCode.UpArrow => Key.UpArrow,
                KeyCode.DownArrow => Key.DownArrow,
                KeyCode.LeftArrow => Key.LeftArrow,
                KeyCode.RightArrow => Key.RightArrow,
                KeyCode.F5 => Key.F5,
                KeyCode.F6 => Key.F6,
                KeyCode.F1 => Key.F1,
                KeyCode.F2 => Key.F2,
                KeyCode.F3 => Key.F3,
                KeyCode.F4 => Key.F4,
                KeyCode.F7 => Key.F7,
                KeyCode.F8 => Key.F8,
                KeyCode.F9 => Key.F9,
                KeyCode.F10 => Key.F10,
                KeyCode.F11 => Key.F11,
                KeyCode.F12 => Key.F12,
                KeyCode.Alpha1 => Key.Digit1,
                KeyCode.Alpha2 => Key.Digit2,
                KeyCode.Alpha3 => Key.Digit3,
                KeyCode.Alpha4 => Key.Digit4,
                KeyCode.Alpha5 => Key.Digit5,
                KeyCode.Alpha6 => Key.Digit6,
                KeyCode.Alpha7 => Key.Digit7,
                KeyCode.Alpha8 => Key.Digit8,
                KeyCode.Alpha9 => Key.Digit9,
                KeyCode.Alpha0 => Key.Digit0,
                KeyCode.Keypad0 => Key.Numpad0,
                KeyCode.Keypad1 => Key.Numpad1,
                KeyCode.Keypad2 => Key.Numpad2,
                KeyCode.Keypad3 => Key.Numpad3,
                KeyCode.Keypad4 => Key.Numpad4,
                KeyCode.Keypad5 => Key.Numpad5,
                KeyCode.Keypad6 => Key.Numpad6,
                KeyCode.Keypad7 => Key.Numpad7,
                KeyCode.Keypad8 => Key.Numpad8,
                KeyCode.Keypad9 => Key.Numpad9,
                _ => Key.None
            };

            if (key != Key.None)
            {
                return true;
            }

            // Name-match fallback, mirroring TryMapInputKey's fallback so every
            // key the capture side can produce is detectable here — without it,
            // a rebound key (e.g. F9) would pass capture but never register.
            return System.Enum.TryParse(keyCode.ToString(), true, out key) && key != Key.None;
        }

        private static bool TryMapInputKey(Key key, out KeyCode keyCode)
        {
            keyCode = key switch
            {
                Key.Digit1 => KeyCode.Alpha1,
                Key.Digit2 => KeyCode.Alpha2,
                Key.Digit3 => KeyCode.Alpha3,
                Key.Digit4 => KeyCode.Alpha4,
                Key.Digit5 => KeyCode.Alpha5,
                Key.Digit6 => KeyCode.Alpha6,
                Key.Digit7 => KeyCode.Alpha7,
                Key.Digit8 => KeyCode.Alpha8,
                Key.Digit9 => KeyCode.Alpha9,
                Key.Digit0 => KeyCode.Alpha0,
                Key.Enter => KeyCode.Return,
                Key.NumpadEnter => KeyCode.KeypadEnter,
                Key.NumpadMinus => KeyCode.KeypadMinus,
                Key.NumpadPlus => KeyCode.KeypadPlus,
                Key.LeftCtrl => KeyCode.LeftControl,
                Key.RightCtrl => KeyCode.RightControl,
                Key.LeftAlt => KeyCode.LeftAlt,
                Key.RightAlt => KeyCode.RightAlt,
                Key.LeftShift => KeyCode.LeftShift,
                Key.RightShift => KeyCode.RightShift,
                _ => KeyCode.None
            };

            if (keyCode != KeyCode.None)
            {
                return true;
            }

            return System.Enum.TryParse(key.ToString(), true, out keyCode) && keyCode != KeyCode.None;
        }
#endif
    }
}
