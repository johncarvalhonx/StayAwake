using System.Runtime.InteropServices;

namespace StayAwake.Core;

/// <summary>
/// Envia a menor entrada possível para zerar o contador de ociosidade do Windows.
/// </summary>
public static class InputPulser
{
    private static readonly int InputSize = Marshal.SizeOf<NativeMethods.INPUT>();

    /// <summary>Marca nossos eventos para conseguirmos distinguir depois, se preciso.</summary>
    public static readonly IntPtr Signature = new(0x57414B45); // "WAKE"

    public static bool Send(PulseMethod method) => method switch
    {
        PulseMethod.MouseNudge => MouseNudge(),
        PulseMethod.KeyF15 => TapKey(NativeMethods.VK_F15),
        PulseMethod.ScrollLock => TapKey(NativeMethods.VK_SCROLL) && TapKey(NativeMethods.VK_SCROLL),
        PulseMethod.ShiftTap => TapKey(NativeMethods.VK_SHIFT),
        PulseMethod.DisplayOnly => true,
        _ => false
    };

    private static bool MouseNudge()
    {
        NativeMethods.GetCursorPos(out var before);

        var ida = MouseMove(1, 0);
        var volta = MouseMove(-1, 0);

        // Se por algum motivo o cursor não voltou (limite de tela), corrige pelo valor original.
        if (NativeMethods.GetCursorPos(out var after) && (after.X != before.X || after.Y != before.Y))
            MouseMove(before.X - after.X, before.Y - after.Y);

        return ida && volta;
    }

    private static bool MouseMove(int dx, int dy)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    dwFlags = NativeMethods.MOUSEEVENTF_MOVE,
                    dwExtraInfo = Signature
                }
            }
        };

        return NativeMethods.SendInput(1, new[] { input }, InputSize) == 1;
    }

    private static bool TapKey(ushort vk)
    {
        var inputs = new[]
        {
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwExtraInfo = Signature }
                }
            },
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = vk,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        dwExtraInfo = Signature
                    }
                }
            }
        };

        return NativeMethods.SendInput((uint)inputs.Length, inputs, InputSize) == inputs.Length;
    }
}
