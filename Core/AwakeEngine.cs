using System.Windows.Threading;

namespace StayAwake.Core;

public enum LogKind { Info, Pulse, Skip, Warn }

public sealed record LogEntry(DateTime At, LogKind Kind, string Message);

public enum EngineState { Parado, Ativo, ForaDoHorario, AguardandoOciosidade }

/// <summary>
/// Coração do app: decide quando enviar o pulso, respeita a janela de horário,
/// o desligamento automático e o modo inteligente.
/// </summary>
public sealed class AwakeEngine : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly List<LogEntry> _log = new();

    private DateTime _lastPulseAt = DateTime.MinValue;
    private DateTime _lastRealInputAt = DateTime.Now;
    private DateTime _nextPulseAt = DateTime.MinValue;

    public AppSettings Settings { get; private set; }

    public bool IsRunning { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public int PulseCount { get; private set; }
    public EngineState State { get; private set; } = EngineState.Parado;

    public IReadOnlyList<LogEntry> Log => _log;

    public event Action? Changed;
    public event Action<LogEntry>? LogAdded;

    public AwakeEngine(AppSettings settings)
    {
        Settings = settings;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public TimeSpan Uptime => StartedAt is null ? TimeSpan.Zero : DateTime.Now - StartedAt.Value;

    /// <summary>Tempo desde a ultima entrada que veio de gente de verdade, ignorando nossos pulsos.</summary>
    public TimeSpan RealIdle => DateTime.Now - _lastRealInputAt;

    public TimeSpan TimeToNextPulse
    {
        get
        {
            if (!IsRunning || _nextPulseAt == DateTime.MinValue) return TimeSpan.Zero;
            var restante = _nextPulseAt - DateTime.Now;
            return restante < TimeSpan.Zero ? TimeSpan.Zero : restante;
        }
    }

    public void ApplySettings(AppSettings settings)
    {
        Settings = settings;
        ApplyPower();

        if (IsRunning)
            _nextPulseAt = DateTime.Now + Settings.Interval;

        Changed?.Invoke();
    }

    public void Start(string motivo = "manual")
    {
        if (IsRunning) return;

        IsRunning = true;
        StartedAt = DateTime.Now;
        PulseCount = 0;
        _nextPulseAt = DateTime.Now + Settings.Interval;
        _lastRealInputAt = DateTime.Now - IdleWatcher.GetIdleTime();

        ApplyPower();
        State = EngineState.Ativo;

        AddLog(LogKind.Info, motivo == "manual"
            ? $"Ativado. Método: {Settings.Method.Titulo()}, a cada {Settings.IntervalSeconds}s."
            : $"Ativado ({motivo}).");

        Changed?.Invoke();
    }

    public void Stop(string motivo = "manual")
    {
        if (!IsRunning) return;

        IsRunning = false;
        State = EngineState.Parado;
        _nextPulseAt = DateTime.MinValue;
        PowerKeeper.Release();

        var tempo = StartedAt is null ? "" : $" Ficou ligado por {Format(DateTime.Now - StartedAt.Value)}.";
        AddLog(LogKind.Info, motivo == "manual" ? $"Desativado.{tempo}" : $"Desativado ({motivo}).{tempo}");

        StartedAt = null;
        Changed?.Invoke();
    }

    public void Toggle()
    {
        if (IsRunning) Stop(); else Start();
    }

    /// <summary>Dispara um pulso agora, fora do ciclo, para testar o método escolhido.</summary>
    public bool PulseNow()
    {
        var ok = InputPulser.Send(Settings.Method);
        if (ok)
        {
            _lastPulseAt = DateTime.Now;
            PulseCount++;
            AddLog(LogKind.Pulse, $"Pulso manual enviado ({Settings.Method.Titulo()}).");
        }
        else
        {
            AddLog(LogKind.Warn, "O Windows recusou o pulso. Um app em modo administrador pode estar em foco.");
        }

        Changed?.Invoke();
        return ok;
    }

    private void Tick()
    {
        RefreshRealIdle();

        if (!IsRunning)
        {
            Changed?.Invoke();
            return;
        }

        // Desligamento automático
        if (Settings.AutoStopEnabled && StartedAt is not null &&
            DateTime.Now - StartedAt.Value >= TimeSpan.FromMinutes(Math.Max(1, Settings.AutoStopMinutes)))
        {
            Stop($"tempo limite de {Settings.AutoStopMinutes} min atingido");
            return;
        }

        // Janela de horário
        if (!Settings.IsInsideSchedule(DateTime.Now))
        {
            if (State != EngineState.ForaDoHorario)
            {
                State = EngineState.ForaDoHorario;
                PowerKeeper.Release();
                AddLog(LogKind.Skip, $"Fora do horário ({Settings.ScheduleStart} às {Settings.ScheduleEnd}). Em espera.");
            }

            _nextPulseAt = DateTime.Now + Settings.Interval;
            Changed?.Invoke();
            return;
        }

        if (State == EngineState.ForaDoHorario)
        {
            ApplyPower();
            AddLog(LogKind.Info, "Dentro do horário de novo. Voltando a agir.");
        }

        State = Settings.SmartMode && RealIdle < TimeSpan.FromSeconds(Settings.SmartIdleSeconds)
            ? EngineState.AguardandoOciosidade
            : EngineState.Ativo;

        if (DateTime.Now >= _nextPulseAt)
        {
            _nextPulseAt = DateTime.Now + Settings.Interval;

            if (Settings.Method == PulseMethod.DisplayOnly)
            {
                // Nada a enviar: a energia já está segura por SetThreadExecutionState.
                Changed?.Invoke();
                return;
            }

            if (Settings.SmartMode && RealIdle < TimeSpan.FromSeconds(Settings.SmartIdleSeconds))
            {
                Changed?.Invoke();
                return; // Você está usando o PC, não precisamos atrapalhar.
            }

            if (InputPulser.Send(Settings.Method))
            {
                _lastPulseAt = DateTime.Now;
                PulseCount++;
                AddLog(LogKind.Pulse, $"Pulso #{PulseCount} enviado. Ocioso há {Format(RealIdle)}.");
            }
            else
            {
                AddLog(LogKind.Warn, "O Windows recusou o pulso. Uma janela em modo administrador pode estar em foco.");
            }
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Nossos próprios pulsos também zeram o contador do Windows.
    /// Aqui separamos o que foi entrada humana de verdade do que fomos nos.
    /// </summary>
    private void RefreshRealIdle()
    {
        var lastInput = DateTime.Now - IdleWatcher.GetIdleTime();

        if (lastInput > _lastPulseAt.AddMilliseconds(900) && lastInput > _lastRealInputAt)
            _lastRealInputAt = lastInput;
    }

    private void ApplyPower() => PowerKeeper.Apply(Settings.KeepSystemAwake, Settings.KeepDisplayOn);

    public void AddLog(LogKind kind, string message)
    {
        var entry = new LogEntry(DateTime.Now, kind, message);
        _log.Add(entry);
        if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200);
        LogAdded?.Invoke(entry);
    }

    public static string Format(TimeSpan t)
    {
        if (t.TotalSeconds < 60) return $"{(int)t.TotalSeconds}s";
        if (t.TotalHours < 1) return $"{t.Minutes}m {t.Seconds:00}s";
        return $"{(int)t.TotalHours}h {t.Minutes:00}m";
    }

    public void Dispose()
    {
        _timer.Stop();
        PowerKeeper.Release();
    }
}
