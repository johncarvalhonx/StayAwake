namespace StayAwake.Core;

/// <summary>
/// O app passa horas escondido na bandeja. Quando some da tela devolvemos as páginas
/// da interface para o Windows, que as traz de volta sozinho quando o painel reabrir.
/// </summary>
public static class MemoryTrimmer
{
    public static void Enxugar()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
            NativeMethods.EmptyWorkingSet(NativeMethods.GetCurrentProcess());
        }
        catch
        {
            // Enxugar memória é otimização, nunca motivo para quebrar o app.
        }
    }
}
