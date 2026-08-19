using System.Drawing;
using System.Drawing.Drawing2D;

namespace StayAwake.Core;

/// <summary>
/// Desenha o ícone da bandeja em tempo de execução, para ele mudar de cor conforme o estado.
/// Evita carregar arquivos e continua nítido em qualquer DPI.
/// </summary>
public static class TrayIconFactory
{
    public static Icon Create(EngineState state)
    {
        var (anel, nucleo) = state switch
        {
            EngineState.Ativo => (Color.FromArgb(255, 184, 77), Color.FromArgb(255, 214, 143)),
            EngineState.AguardandoOciosidade => (Color.FromArgb(96, 165, 250), Color.FromArgb(191, 219, 254)),
            EngineState.ForaDoHorario => (Color.FromArgb(148, 163, 184), Color.FromArgb(203, 213, 225)),
            _ => (Color.FromArgb(100, 110, 130), Color.FromArgb(140, 150, 170))
        };

        const int s = 32;
        using var bmp = new Bitmap(s, s);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var pen = new Pen(anel, 3.6f);
            g.DrawEllipse(pen, 5f, 5f, s - 10f, s - 10f);

            using var brush = new SolidBrush(nucleo);
            g.FillEllipse(brush, 12.5f, 12.5f, 7f, 7f);
        }

        var handle = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
