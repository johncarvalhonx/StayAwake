using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StayAwake.Core;

namespace StayAwake;

public sealed class LogRow
{
    public string Hora { get; init; } = "";
    public string Mensagem { get; init; } = "";
    public Brush Cor { get; init; } = Brushes.Gray;
}

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly AwakeEngine _engine;
    private readonly ObservableCollection<LogRow> _linhas = new();
    private readonly List<RadioButton> _opcoes = new();
    private readonly DispatcherTimer _salvamento;

    private bool _carregando = true;
    private bool _voltandoDaBandeja;
    private Storyboard? _pulsacao;

    public MainWindow()
    {
        InitializeComponent();

        _settings = App.Current.Settings;
        _engine = App.Current.Engine;

        LogList.ItemsSource = _linhas;

        _salvamento = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _salvamento.Tick += (_, _) =>
        {
            _salvamento.Stop();
            SettingsStore.Save(_settings);
        };

        MontarMetodos();
        CarregarDaConfiguracao();

        foreach (var entrada in _engine.Log)
            AdicionarLinha(entrada);

        _engine.Changed += AtualizarPainel;
        _engine.LogAdded += AdicionarLinha;

        _carregando = false;
        AtualizarPainel();
        AjustarAlturaNaTela();
    }

    /// <summary>
    /// O método marcado puxa o foco inicial e arrastava a página junto. O painel tem que abrir no topo,
    /// mostrando o estado, tanto na primeira vez quanto quando volta da bandeja.
    /// </summary>
    private void Window_Loaded(object sender, RoutedEventArgs e) => VoltarAoTopo();

    private void VoltarAoTopo() => Dispatcher.BeginInvoke(
        new Action(() => ScrollPrincipal.ScrollToTop()), DispatcherPriority.Loaded);

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (_voltandoDaBandeja)
        {
            _voltandoDaBandeja = false;
            VoltarAoTopo();
        }
    }

    /// <summary>Em telas baixas a janela encolhe, o conteúdo continua rolando por dentro.</summary>
    private void AjustarAlturaNaTela()
    {
        var util = SystemParameters.WorkArea.Height - 60;
        if (util > 400 && util < Height)
            Height = util;
    }

    // ==================== Montagem ====================

    private void MontarMetodos()
    {
        foreach (PulseMethod método in Enum.GetValues<PulseMethod>())
        {
            var titulo = new TextBlock
            {
                Text = método.Titulo(),
                Style = (Style)FindResource("Label")
            };

            var detalhe = new TextBlock
            {
                Text = método.Detalhe(),
                Style = (Style)FindResource("Hint")
            };

            var pilha = new StackPanel();
            pilha.Children.Add(titulo);
            pilha.Children.Add(detalhe);

            var opcao = new RadioButton
            {
                Style = (Style)FindResource("MethodOption"),
                GroupName = "Método",
                Content = pilha,
                Tag = método
            };

            opcao.Click += Metodo_Click;
            _opcoes.Add(opcao);
            MethodPanel.Children.Add(opcao);
        }
    }

    private void CarregarDaConfiguracao()
    {
        _carregando = true;

        foreach (var opcao in _opcoes)
            opcao.IsChecked = (PulseMethod)opcao.Tag! == _settings.Method;

        FieldInterval.Text = _settings.IntervalSeconds.ToString(CultureInfo.InvariantCulture);
        FieldSmartIdle.Text = _settings.SmartIdleSeconds.ToString(CultureInfo.InvariantCulture);
        FieldAutoStop.Text = _settings.AutoStopMinutes.ToString(CultureInfo.InvariantCulture);
        FieldStart.Text = _settings.ScheduleStart;
        FieldEnd.Text = _settings.ScheduleEnd;

        SwSmart.IsChecked = _settings.SmartMode;
        SwSystem.IsChecked = _settings.KeepSystemAwake;
        SwDisplay.IsChecked = _settings.KeepDisplayOn;
        SwAutoStop.IsChecked = _settings.AutoStopEnabled;
        SwSchedule.IsChecked = _settings.ScheduleEnabled;
        SwStartup.IsChecked = _settings.StartWithWindows;
        SwStartMin.IsChecked = _settings.StartMinimized;
        SwAutoEngine.IsChecked = _settings.AutoStartEngine;
        SwCloseToTray.IsChecked = _settings.MinimizeToTrayOnClose;

        SincronizarChips();
        AtualizarDependencias();

        _carregando = false;
    }

    // ==================== Eventos da interface ====================

    private void Metodo_Click(object sender, RoutedEventArgs e)
    {
        if (_carregando || sender is not RadioButton rb) return;

        _settings.Method = (PulseMethod)rb.Tag!;
        _engine.ApplySettings(_settings);
        _engine.AddLog(LogKind.Info, $"Método alterado para {_settings.Method.Titulo()}.");
        AgendarSalvamento();
        AtualizarPainel();
    }

    private void Chip_Click(object sender, RoutedEventArgs e)
    {
        if (_carregando || sender is not RadioButton rb || rb.Tag is not string tag) return;
        if (!int.TryParse(tag, out var segundos)) return;

        FieldInterval.Text = segundos.ToString(CultureInfo.InvariantCulture);
    }

    private void FieldInterval_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_carregando) return;

        if (int.TryParse(FieldInterval.Text, out var segundos) && segundos is >= 5 and <= 3600)
        {
            _settings.IntervalSeconds = segundos;
            _engine.ApplySettings(_settings);
            SincronizarChips();
            AgendarSalvamento();
        }

        AtualizarPainel();
    }

    private void Field_TextChanged(object sender, TextChangedEventArgs e) => AplicarCampos();

    private void Setting_Changed(object sender, RoutedEventArgs e) => AplicarCampos();

    private void AplicarCampos()
    {
        if (_carregando) return;

        if (int.TryParse(FieldSmartIdle.Text, out var ocioso) && ocioso is >= 5 and <= 3600)
            _settings.SmartIdleSeconds = ocioso;

        if (int.TryParse(FieldAutoStop.Text, out var minutos) && minutos is >= 1 and <= 1440)
            _settings.AutoStopMinutes = minutos;

        if (_settings.ParseTime(FieldStart.Text) is not null)
            _settings.ScheduleStart = FieldStart.Text;

        if (_settings.ParseTime(FieldEnd.Text) is not null)
            _settings.ScheduleEnd = FieldEnd.Text;

        _settings.SmartMode = SwSmart.IsChecked == true;
        _settings.KeepSystemAwake = SwSystem.IsChecked == true;
        _settings.KeepDisplayOn = SwDisplay.IsChecked == true;
        _settings.AutoStopEnabled = SwAutoStop.IsChecked == true;
        _settings.ScheduleEnabled = SwSchedule.IsChecked == true;
        _settings.StartMinimized = SwStartMin.IsChecked == true;
        _settings.AutoStartEngine = SwAutoEngine.IsChecked == true;
        _settings.MinimizeToTrayOnClose = SwCloseToTray.IsChecked == true;

        _engine.ApplySettings(_settings);
        AtualizarDependencias();
        AgendarSalvamento();
        AtualizarPainel();
    }

    private void SwStartup_Click(object sender, RoutedEventArgs e)
    {
        if (_carregando) return;

        var desejado = SwStartup.IsChecked == true;

        if (StartupManager.Set(desejado))
        {
            _settings.StartWithWindows = desejado;
            _engine.AddLog(LogKind.Info, desejado
                ? "Vai abrir junto com o Windows, direto na bandeja."
                : "Não abre mais junto com o Windows.");
        }
        else
        {
            SwStartup.IsChecked = !desejado;
            _engine.AddLog(LogKind.Warn, "Não consegui gravar a inicialização automática no registro do usuário.");
        }

        AgendarSalvamento();
    }

    private void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        _engine.Toggle();
        AtualizarPainel();
    }

    private void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.Method == PulseMethod.DisplayOnly)
        {
            _engine.AddLog(LogKind.Skip, "Nesse método não existe pulso para testar, ele só segura a energia.");
            return;
        }

        _engine.PulseNow();
    }

    private void BtnQuit_Click(object sender, RoutedEventArgs e) => App.Current.EncerrarTudo();

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            _voltandoDaBandeja = true;
            MemoryTrimmer.Enxugar();
        }
    }

    private void Esconder()
    {
        Hide();
        _voltandoDaBandeja = true;
        MemoryTrimmer.Enxugar();
    }

    /// <summary>Devolve para os campos o valor que a configuração realmente aceitou.</summary>
    private void Campo_LostFocus(object sender, RoutedEventArgs e)
    {
        var estava = _carregando;
        _carregando = true;

        FieldInterval.Text = _settings.IntervalSeconds.ToString(CultureInfo.InvariantCulture);
        FieldSmartIdle.Text = _settings.SmartIdleSeconds.ToString(CultureInfo.InvariantCulture);
        FieldAutoStop.Text = _settings.AutoStopMinutes.ToString(CultureInfo.InvariantCulture);
        FieldStart.Text = _settings.ScheduleStart;
        FieldEnd.Text = _settings.ScheduleEnd;

        _carregando = estava;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.MinimizeToTrayOnClose)
            Esconder();
        else
            App.Current.EncerrarTudo();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Esconder();
            return;
        }

        base.OnClosing(e);
        App.Current.EncerrarTudo();
    }

    // ==================== Atualizacao do painel ====================

    private void AtualizarPainel()
    {
        var estado = _engine.IsRunning ? _engine.State : EngineState.Parado;

        var (texto, dica, cor) = estado switch
        {
            EngineState.Ativo => (
                "Ativo",
                _settings.Method == PulseMethod.DisplayOnly
                    ? "Segurando a tela e a energia. Nenhuma entrada simulada."
                    : $"Enviando {_settings.Method.Titulo().ToLowerInvariant()} a cada {_settings.IntervalSeconds}s.",
                (Brush)FindResource("Accent")),

            EngineState.AguardandoOciosidade => (
                "Você está no PC",
                $"Sem interferir. Age depois de {_settings.SmartIdleSeconds}s parado.",
                (Brush)FindResource("Blue")),

            EngineState.ForaDoHorario => (
                "Em espera",
                $"Volta a agir das {_settings.ScheduleStart} às {_settings.ScheduleEnd}.",
                (Brush)FindResource("Muted")),

            _ => (
                "Desligado",
                "Seu status vai cair para ausente normalmente.",
                (Brush)FindResource("Muted"))
        };

        TxtState.Text = texto;
        TxtStateHint.Text = dica;

        Ring.Stroke = cor;
        Nucleo.Fill = cor;
        Halo.Fill = cor;

        BtnToggle.Content = _engine.IsRunning ? "PAUSAR" : "ATIVAR";
        BtnToggle.Background = _engine.IsRunning
            ? (Brush)FindResource("CardSoft")
            : (Brush)FindResource("Accent");
        BtnToggle.Foreground = _engine.IsRunning
            ? (Brush)FindResource("Text")
            : new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x1F));

        TxtUptime.Text = _engine.IsRunning ? AwakeEngine.Format(_engine.Uptime) : "-";
        TxtPulses.Text = _engine.PulseCount.ToString(CultureInfo.InvariantCulture);

        TxtNext.Text = estado switch
        {
            EngineState.Parado => "-",
            EngineState.ForaDoHorario => "em espera",
            _ when _settings.Method == PulseMethod.DisplayOnly => "não usa",
            _ => $"{(int)_engine.TimeToNextPulse.TotalSeconds}s"
        };

        TxtFooter.Text = _engine.IsRunning
            ? $"Sem mexer no PC há {AwakeEngine.Format(_engine.RealIdle)}. Com a tela bloqueada, nenhum app mantém você disponível no Teams."
            : "Com a tela bloqueada nenhum app consegue manter você disponível no Teams.";

        AnimarHalo(estado == EngineState.Ativo);
    }

    private void AnimarHalo(bool ativo)
    {
        if (ativo)
        {
            if (_pulsacao is not null) return;

            var animacao = new DoubleAnimation
            {
                From = 0.10,
                To = 0.30,
                Duration = TimeSpan.FromSeconds(1.6),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            _pulsacao = new Storyboard();
            _pulsacao.Children.Add(animacao);
            Storyboard.SetTarget(animacao, Halo);
            Storyboard.SetTargetProperty(animacao, new PropertyPath(OpacityProperty));
            _pulsacao.Begin();
        }
        else if (_pulsacao is not null)
        {
            _pulsacao.Stop();
            _pulsacao = null;
            Halo.Opacity = 0.10;
        }
    }

    private void AtualizarDependencias()
    {
        SmartRow.IsEnabled = SwSmart.IsChecked == true;
        SmartRow.Opacity = SmartRow.IsEnabled ? 1 : 0.45;

        AutoStopRow.IsEnabled = SwAutoStop.IsChecked == true;
        AutoStopRow.Opacity = AutoStopRow.IsEnabled ? 1 : 0.45;

        ScheduleRow.IsEnabled = SwSchedule.IsChecked == true;
        ScheduleRow.Opacity = ScheduleRow.IsEnabled ? 1 : 0.45;
    }

    private void SincronizarChips()
    {
        var anterior = _carregando;
        _carregando = true;

        Chip30.IsChecked = _settings.IntervalSeconds == 30;
        Chip60.IsChecked = _settings.IntervalSeconds == 60;
        Chip120.IsChecked = _settings.IntervalSeconds == 120;
        Chip240.IsChecked = _settings.IntervalSeconds == 240;

        _carregando = anterior;
    }

    private void AdicionarLinha(LogEntry entrada)
    {
        var cor = entrada.Kind switch
        {
            LogKind.Pulse => (Brush)FindResource("Accent"),
            LogKind.Skip => (Brush)FindResource("Blue"),
            LogKind.Warn => new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0x7A)),
            _ => (Brush)FindResource("Text")
        };

        _linhas.Add(new LogRow
        {
            Hora = entrada.At.ToString("HH:mm:ss"),
            Mensagem = entrada.Message,
            Cor = cor
        });

        while (_linhas.Count > 120)
            _linhas.RemoveAt(0);

        LogScroll.ScrollToEnd();
    }

    private void AgendarSalvamento()
    {
        _salvamento.Stop();
        _salvamento.Start();
    }
}
