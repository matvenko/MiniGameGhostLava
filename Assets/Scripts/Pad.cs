using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

// The game controller, read in one place.
//
// It goes through the Input System rather than the old Input Manager because the
// old one numbers a controller's buttons differently on every platform - Start is
// one number on Windows and another on Android, and the D-pad is an axis on one
// and a set of keys on the other - while the Input System names them: buttonSouth
// is the bottom face button on an Xbox pad, a PlayStation pad, and anything else
// Android recognises as a gamepad.
//
// The buttons are named here by where they sit rather than by their letter for the
// same reason. South is A on an Xbox pad and Cross on a PlayStation one.
//
// Built without the Input System, every read here comes back empty and the game is
// exactly what it was before a controller was thought of.
public enum PadButton { South, East, West, North, Start, Select }

public static class Pad
{
    // Whether the controller is what the player last reached for. The focus ring
    // in the menus and the letters on the ability buttons are only for someone
    // holding one; a tap or the mouse puts them away again. Kept up to date by
    // GamepadMenus.
    public static bool InUse { get; internal set; }

    // Left stick and D-pad together, right and up positive, never longer than one.
    public static Vector2 Move
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad == null) return Vector2.zero;
            return Vector2.ClampMagnitude(pad.leftStick.ReadValue() + pad.dpad.ReadValue(), 1f);
#else
            return Vector2.zero;
#endif
        }
    }

    // The right stick's up and down, for scrolling a long page.
    public static float Scroll
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            return pad != null ? pad.rightStick.ReadValue().y : 0f;
#else
            return 0f;
#endif
        }
    }

    public static bool Pressed(PadButton button)
    {
#if ENABLE_INPUT_SYSTEM
        var pad = Gamepad.current;
        return pad != null && Control(pad, button).wasPressedThisFrame;
#else
        return false;
#endif
    }

    // Anything at all done on the controller this frame: a button going down, or a
    // stick pushed well off centre. A stick resting a hair off centre is not the
    // player picking the controller up.
    internal static bool Touched()
    {
#if ENABLE_INPUT_SYSTEM
        var pad = Gamepad.current;
        if (pad == null) return false;
        foreach (var control in pad.allControls)
            if (control is ButtonControl button && button.wasPressedThisFrame) return true;
        return pad.leftStick.ReadValue().sqrMagnitude > .25f || pad.rightStick.ReadValue().sqrMagnitude > .25f;
#else
        return false;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static ButtonControl Control(Gamepad pad, PadButton button)
    {
        switch (button)
        {
            case PadButton.South: return pad.buttonSouth;
            case PadButton.East: return pad.buttonEast;
            case PadButton.West: return pad.buttonWest;
            case PadButton.North: return pad.buttonNorth;
            case PadButton.Start: return pad.startButton;
            default: return pad.selectButton;
        }
    }
#endif
}
