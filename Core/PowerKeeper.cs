namespace StayAwake.Core;

/// <summary>
/// Impede que o Windows durma ou desligue a tela enquanto o StayAwake estiver ligado.
/// Precisa ser chamado sempre da mesma thread (usamos a thread da UI).
/// </summary>
public static class PowerKeeper
{
    private static bool _active;

    public static bool IsActive => _active;

    public static void Apply(bool keepSystemAwake, bool keepDisplayOn)
    {
        if (!keepSystemAwake && !keepDisplayOn)
        {
            Release();
            return;
        }

        var flags = NativeMethods.ExecutionState.Continuous;
        if (keepSystemAwake) flags |= NativeMethods.ExecutionState.SystemRequired;
        if (keepDisplayOn) flags |= NativeMethods.ExecutionState.DisplayRequired;

        NativeMethods.SetThreadExecutionState(flags);
        _active = true;
    }

    public static void Release()
    {
        if (!_active) return;
        NativeMethods.SetThreadExecutionState(NativeMethods.ExecutionState.Continuous);
        _active = false;
    }
}
