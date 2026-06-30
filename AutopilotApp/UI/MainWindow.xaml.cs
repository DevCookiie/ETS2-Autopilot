using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ETS2Autopilot.Core;
using ETS2Autopilot.Input;

namespace ETS2Autopilot.UI;

public partial class MainWindow : Window
{
    private readonly TelemetryReader   _telemetry = new();
    private readonly VJoyOutput        _vjoy      = new();
    private readonly AutopilotSettings _settings  = new();
    private          AutopilotEngine?  _engine;

    private readonly DispatcherTimer _tickTimer    = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer _connectTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _diagTimer    = new() { Interval = TimeSpan.FromSeconds(3) };

    private bool _autopilotActive;

    public MainWindow()
    {
        InitializeComponent();

        _tickTimer.Tick    += OnTick;
        _connectTimer.Tick += OnConnectRetry;
        _diagTimer.Tick    += OnDiagTick;

        KeyDown += (_, e) => { if (e.Key == Key.F5) ToggleAutopilot(); };

        InitVJoy();
        _diagTimer.Start();
        OnDiagTick(null, EventArgs.Empty);
        _connectTimer.Start();
        TryConnect();
    }

    private void InitVJoy()
    {
        bool ok = _vjoy.Initialize();
        DiagVjoy.Text      = ok ? "OK" : "Ikke fundet - kør som administrator";
        DiagVjoy.Foreground = ok
            ? new SolidColorBrush(Color.FromRgb(0, 200, 100))
            : new SolidColorBrush(Color.FromRgb(220, 80, 80));
    }

    // --- Diagnostik ---

    private void OnDiagTick(object? s, EventArgs e)
    {
        // ETS2 kører?
        bool ets2Running = Process.GetProcessesByName("eurotrucks2").Length > 0;
        DiagGame.Text       = ets2Running ? "Ja" : "Nej - start ETS2";
        DiagGame.Foreground = ets2Running
            ? new SolidColorBrush(Color.FromRgb(0, 200, 100))
            : new SolidColorBrush(Color.FromRgb(220, 80, 80));

        // Plugin forbundet?
        bool pluginOk = _telemetry.IsConnected;
        DiagPlugin.Text       = pluginOk ? "Forbundet" : "Ikke forbundet (scs-telemetry.dll mangler i plugins-mappen)";
        DiagPlugin.Foreground = pluginOk
            ? new SolidColorBrush(Color.FromRgb(0, 200, 100))
            : new SolidColorBrush(Color.FromRgb(220, 80, 80));
    }

    private void UpdateDiagTelemetry(TelemetryData tele)
    {
        DiagSpeed.Text      = $"{tele.SpeedKmh:F1} km/h";
        DiagGear.Text       = tele.Gear switch { -1 => "R", 0 => "N", var g => g.ToString() };
        DiagHeading.Text    = $"{tele.HeadingDeg:F1}°";
        DiagSpeedLimit.Text = tele.NavSpeedLimitKmh > 0 ? $"{tele.NavSpeedLimitKmh:F0} km/h" : "Ingen grænse";
        DiagNavDist.Text    = tele.NavDistanceM > 0 ? $"{tele.NavDistanceM:F0} m" : "--";
        DiagSteer.Text      = $"{tele.Steering:F3}";
        DiagThrottle.Text   = $"{tele.Throttle:P0}";
        DiagBrake.Text      = $"{tele.Brake:P0}";
        DiagPaused.Text     = tele.GamePaused ? "Ja" : "Nej";
    }

    private void VjoyTestButton_Click(object s, RoutedEventArgs e)
    {
        if (!_vjoy.IsAvailable)
        {
            DiagVjoyResult.Text       = "vJoy ikke tilgængeligt - installer driver og kør som administrator";
            DiagVjoyResult.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
            return;
        }

        // Send fuld venstre → center → fuld højre → center
        _ = Task.Run(async () =>
        {
            _vjoy.Send(-1f, 0f, 0f);
            await Task.Delay(400);
            _vjoy.Send(0f, 0f, 0f);
            await Task.Delay(200);
            _vjoy.Send(1f, 0f, 0f);
            await Task.Delay(400);
            _vjoy.Center();

            Dispatcher.Invoke(() =>
            {
                DiagVjoyResult.Text       = "Test sendt! Tjekkede du at styrehjulet bevægede sig i ETS2?";
                DiagVjoyResult.Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 100));
            });
        });

        DiagVjoyResult.Text       = "Sender test...";
        DiagVjoyResult.Foreground = new SolidColorBrush(Color.FromRgb(240, 165, 0));
    }

    // --- Forbindelseshåndtering ---

    private void TryConnect()
    {
        if (_telemetry.TryConnect())
        {
            _connectTimer.Stop();
            _tickTimer.Start();
            SetConnectionUI(true);
            ActivateButton.IsEnabled = true;
            OnDiagTick(null, EventArgs.Empty);
        }
        else
        {
            SetConnectionUI(false);
        }
    }

    private void OnConnectRetry(object? s, EventArgs e) => TryConnect();

    // --- Hoved-tick (50ms) ---

    private void OnTick(object? s, EventArgs e)
    {
        TelemetryData tele = _telemetry.Read();

        if (!tele.IsConnected)
        {
            SetConnectionUI(false);
            if (_autopilotActive) DeactivateAutopilot();
            ActivateButton.IsEnabled = false;
            _tickTimer.Stop();
            _connectTimer.Start();
            OnDiagTick(null, EventArgs.Empty);
            return;
        }

        // Autopilot-fane
        SpeedText.Text      = ((int)tele.SpeedKmh).ToString();
        SpeedLimitText.Text = tele.NavSpeedLimitKmh > 0.1f ? ((int)tele.NavSpeedLimitKmh).ToString() : "--";
        NavDistText.Text    = tele.NavDistanceM > 0f ? ((int)tele.NavDistanceM).ToString() : "--";

        // Diagnostik-fane
        UpdateDiagTelemetry(tele);

        // Autopilot logik
        if (_autopilotActive && _engine != null)
        {
            AutopilotOutput output = _engine.Update(tele);
            StatusText.Text = output.StatusText;
            SteerBar.Value  = output.Steering;
            UpdateActivateButton(output.State);

            if (output.State != AutopilotState.Inactive)
                _vjoy.Send(output.Steering, output.Throttle, output.Brake);
        }
    }

    // --- Autopilot toggle ---

    private void ToggleAutopilot()
    {
        if (_autopilotActive) DeactivateAutopilot();
        else ActivateAutopilot();
    }

    private void ActivateAutopilot()
    {
        if (!_telemetry.IsConnected) return;
        _engine = new AutopilotEngine(BuildSettings());
        _engine.Activate();
        _autopilotActive = true;
        ActivateButton.Content    = "DEAKTIVER AUTOPILOT  [F5]";
        ActivateButton.Background = new SolidColorBrush(Color.FromRgb(0x9b, 0x2, 0x26));
        StatusText.Text = "Autopilot aktiveret!";
    }

    private void DeactivateAutopilot()
    {
        _engine?.Deactivate();
        _autopilotActive = false;
        _vjoy.Center();
        ActivateButton.Content    = "AKTIVER AUTOPILOT  [F5]";
        ActivateButton.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3e));
        StatusText.Text = "Autopilot deaktiveret";
    }

    private AutopilotSettings BuildSettings() => new()
    {
        MaxSpeedKmh           = (float)MaxSpeedSlider.Value,
        TurnApproachDistanceM = (float)TurnDistSlider.Value,
        EnableSpeedLimitFollow = SpeedLimitCheck.IsChecked == true,
        EnableLaneKeeping      = LaneKeepingCheck.IsChecked == true,
    };

    private void UpdateActivateButton(AutopilotState state)
    {
        ActivateButton.Background = state switch
        {
            AutopilotState.Active      => new SolidColorBrush(Color.FromRgb(0x9b, 0x02, 0x26)),
            AutopilotState.Approaching => new SolidColorBrush(Color.FromRgb(0xb5, 0x6a, 0x00)),
            AutopilotState.Paused      => new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            _                          => new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3e)),
        };
    }

    private void SetConnectionUI(bool connected)
    {
        StatusDot.Fill      = new SolidColorBrush(connected ? Color.FromRgb(0, 200, 100) : Color.FromRgb(180, 30, 30));
        ConnectionText.Text = connected ? "Forbundet til ETS2" : "Ikke forbundet — start ETS2 med scs-sdk-plugin installeret";
    }

    private void ActivateButton_Click(object s, RoutedEventArgs e) => ToggleAutopilot();
    private void MaxSpeedSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        => MaxSpeedLabel.Text = ((int)e.NewValue).ToString();
    private void TurnDistSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        => TurnDistLabel.Text = ((int)e.NewValue).ToString();

    protected override void OnClosed(EventArgs e)
    {
        _tickTimer.Stop();
        _connectTimer.Stop();
        _diagTimer.Stop();
        if (_autopilotActive) _vjoy.Center();
        _vjoy.Dispose();
        _telemetry.Dispose();
        base.OnClosed(e);
    }
}
