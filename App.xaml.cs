using System.Windows;
using Microsoft.Win32;
using StayAwake.Core;
using Forms = System.Windows.Forms;

namespace StayAwake;

public partial class App : Application
{
    private const string MutexName = "StayAwake.SingleInstance.v1";

    private Mutex? _mutex;
    private Forms.NotifyIcon? _tray;
    private System.Drawing.Icon? _trayIcon;
    private EngineState _lastTrayState = (EngineState)(-1);
    private Forms.ToolStripMenuItem? _toggleItem;

    public AppSettings Settings { get; private set; } = new();
    public AwakeEngine Engine { get; private set; } = null!;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out var criouPrimeiro);
        if (!criouPrimeiro)
        {
            // Já existe uma instancia rodando na bandeja.
            MessageBox.Show("O StayAwake já está aberto. Procure o ícone na bandeja do sistema.",
                "StayAwake", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Settings = SettingsStore.Load();
        Settings.StartWithWindows = StartupManager.IsEnabled();

        Engine = new AwakeEngine(Settings);
        Engine.Changed += AtualizarBandeja;

        SystemEvents.SessionSwitch += OnSessionSwitch;

        BuildTray();

        var minimizado = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
                         || Settings.StartMinimized;

        var janela = new MainWindow();
        MainWindow = janela;

        if (!minimizado)
            janela.Show();
        else
        {
            Engine.AddLog(LogKind.Info, "Iniciado direto na bandeja.");
            MemoryTrimmer.Enxugar();
        }

        if (Settings.AutoStartEngine)
            Engine.Start("início automático");

        AtualizarBandeja();
    }

    private void BuildTray()
    {
        var menu = new Forms.ContextMenuStrip { ShowImageMargin = false };

        _toggleItem = new Forms.ToolStripMenuItem("Ativar", null, (_, _) =>
        {
            Engine.Toggle();
            AtualizarBandeja();
        })
        { Font = new System.Drawing.Font(Forms.Control.DefaultFont, System.Drawing.FontStyle.Bold) };

        menu.Items.Add(_toggleItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Abrir painel", null, (_, _) => MostrarJanela());
        menu.Items.Add("Enviar pulso agora", null, (_, _) => Engine.PulseNow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => EncerrarTudo());

        _tray = new Forms.NotifyIcon
        {
            Text = "StayAwake",
            Visible = true,
            ContextMenuStrip = menu
        };

        _tray.DoubleClick += (_, _) => MostrarJanela();
    }

    public void MostrarJanela()
    {
        Dispatcher.Invoke(() =>
        {
            if (MainWindow is null) return;
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
            MainWindow.Topmost = true;
            MainWindow.Topmost = false;
        });
    }

    private void AtualizarBandeja()
    {
        if (_tray is null) return;

        var estado = Engine.IsRunning ? Engine.State : EngineState.Parado;

        if (estado != _lastTrayState)
        {
            _lastTrayState = estado;
            var novo = TrayIconFactory.Create(estado);
            var antigo = _trayIcon;
            _tray.Icon = novo;
            _trayIcon = novo;
            antigo?.Dispose();
        }

        var descricao = estado switch
        {
            EngineState.Ativo => $"Ativo, pulso a cada {Settings.IntervalSeconds}s",
            EngineState.AguardandoOciosidade => "Ativo, aguardando você parar",
            EngineState.ForaDoHorario => "Em espera, fora do horário",
            _ => "Desligado"
        };

        var texto = $"StayAwake - {descricao}";
        _tray.Text = texto.Length > 62 ? texto[..62] : texto;

        if (_toggleItem is not null)
            _toggleItem.Text = Engine.IsRunning ? "Pausar" : "Ativar";
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (!Engine.IsRunning) return;

        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                Engine.AddLog(LogKind.Warn,
                    "Windows bloqueado. Com a tela bloqueada o Teams marca ausente de qualquer jeito.");
                break;
            case SessionSwitchReason.SessionUnlock:
                Engine.AddLog(LogKind.Info, "Sessão desbloqueada. Seguindo normalmente.");
                break;
        }
    }

    public void EncerrarTudo()
    {
        Settings.StartWithWindows = StartupManager.IsEnabled();
        SettingsStore.Save(Settings);

        Engine.Stop("saindo");
        Engine.Dispose();
        PowerKeeper.Release();

        SystemEvents.SessionSwitch -= OnSessionSwitch;

        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        _trayIcon?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PowerKeeper.Release();
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
