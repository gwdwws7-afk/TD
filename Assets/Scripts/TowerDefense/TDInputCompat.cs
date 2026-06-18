using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TD
{
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

        public static Vector3 MousePosition
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

#if ENABLE_INPUT_SYSTEM
        private static bool TryMapKey(KeyCode keyCode, out Key key)
        {
            key = keyCode switch
            {
                KeyCode.R => Key.R,
                KeyCode.Q => Key.Q,
                KeyCode.E => Key.E,
                KeyCode.Z => Key.Z,
                KeyCode.X => Key.X,
                KeyCode.Space => Key.Space,
                KeyCode.F5 => Key.F5,
                KeyCode.F6 => Key.F6,
                KeyCode.Alpha1 => Key.Digit1,
                KeyCode.Alpha2 => Key.Digit2,
                KeyCode.Alpha3 => Key.Digit3,
                KeyCode.Alpha4 => Key.Digit4,
                KeyCode.Alpha5 => Key.Digit5,
                KeyCode.Alpha6 => Key.Digit6,
                KeyCode.Alpha7 => Key.Digit7,
                KeyCode.Alpha8 => Key.Digit8,
                _ => Key.None
            };

            return key != Key.None;
        }
#endif
    }
}
