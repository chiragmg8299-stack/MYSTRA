using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class MystraInput
{
    public static Vector2 Move()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current; if (k == null) return Vector2.zero;
        float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
        float y = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
        return new Vector2(x, y);
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }

    public static Vector2 Look()
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current; return m == null ? Vector2.zero : m.delta.ReadValue() * 0.1f;
#else
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 3f;
#endif
    }

    public static bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}