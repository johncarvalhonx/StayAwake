namespace StayAwake.Core;

/// <summary>
/// Mede há quanto tempo o Windows não recebe entrada humana.
/// O Teams, o Slack e o próprio Windows usam exatamente esse contador para marcar "ausente".
/// </summary>
public static class IdleWatcher
{
    public static TimeSpan GetIdleTime()
    {
        var info = new NativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>()
        };

        if (!NativeMethods.GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        // dwTime é um contador de 32 bits; GetTickCount64 evita o estouro depois de 49 dias ligado.
        ulong now = NativeMethods.GetTickCount64();
        ulong last = (now & 0xFFFFFFFF00000000UL) | info.dwTime;
        if (last > now) last -= 0x100000000UL;

        return TimeSpan.FromMilliseconds(now - last);
    }
}
