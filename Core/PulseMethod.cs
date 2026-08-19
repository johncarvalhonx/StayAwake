namespace StayAwake.Core;

public enum PulseMethod
{
    /// <summary>Move o cursor 1 pixel e devolve. Funciona em praticamente tudo.</summary>
    MouseNudge = 0,

    /// <summary>Tecla F15, que não existe em teclado fisico moderno e nenhum app usa.</summary>
    KeyF15 = 1,

    /// <summary>Liga e desliga o Scroll Lock, mantendo o estado original.</summary>
    ScrollLock = 2,

    /// <summary>Shift pressionado e solto sem digitar nada.</summary>
    ShiftTap = 3,

    /// <summary>Nenhuma entrada simulada. Só segura a energia e a tela.</summary>
    DisplayOnly = 4
}

public static class PulseMethodInfo
{
    public static string Titulo(this PulseMethod m) => m switch
    {
        PulseMethod.MouseNudge => "Mouse (1 px, ida e volta)",
        PulseMethod.KeyF15 => "Tecla F15 (invisível)",
        PulseMethod.ScrollLock => "Scroll Lock (toque duplo)",
        PulseMethod.ShiftTap => "Shift (toque seco)",
        PulseMethod.DisplayOnly => "Só manter tela ligada",
        _ => m.ToString()
    };

    public static string Detalhe(this PulseMethod m) => m switch
    {
        PulseMethod.MouseNudge => "Mais confiável. O cursor volta para o mesmo pixel.",
        PulseMethod.KeyF15 => "Não digita nada e não mexe o cursor. Ideal com jogo aberto.",
        PulseMethod.ScrollLock => "Dois toques seguidos, o estado da luz não muda.",
        PulseMethod.ShiftTap => "Sozinho o Shift não escreve caractere nenhum.",
        PulseMethod.DisplayOnly => "Não simula entrada. O Teams ainda vai marcar ausente.",
        _ => string.Empty
    };
}
