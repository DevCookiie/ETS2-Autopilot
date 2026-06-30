using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ETS2Autopilot.Core;
using ETS2Autopilot.Input;

namespace ETS2Autopilot.UI;

public partial class MainWindow : Window
{
    private readonly TelemetryReader  _telemetry = new();
    private readonly VJoyOutput       _vjoy      = new();
    private readonly AutopilotSettings _settings  = new();
    private          AutopilotEngine?  _engine;

    private readonly DispatcherTimer _tickTimer      = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer _connectTimer   = new() { Interval = TimeSpan.FromSeconds(2) };

    private bool _autopilotActive;

    public MainWindow()
    {
        InitializeComponent();

        _tickTimer.Tick    += OnTick;
        _connectTimer.Tick += OnConnectRetry;

        // Register F5 hotkey
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F5) ToggleAutopilot();
        };

        // Init vJoy (non-fatal if not installed)
        bool vjoyOk = _vjoy.Initialize();
        if (!vjoyOk)
            AppendStatus("vJoy ikke fundet - kør som administrator eller installer vJoy.");
        else
            AppendStatus("vJoy OK.");

        _connectTimer.Start();
        TryConnect();
    }

    private void TryConnect()
    {
        if (_telemetry.TryConnect())
        {
            _connectTimer.Stop();
            _tickTimer.Start();
            SetConnectionUI(true);
            AppendStatus("Forbundet til ETS2!");
            ActivateButton.IsEnabled = true;
        }
        else
        {
            SetConnectionUI(false);
        }
    }

    private void OnConnectRetry(object? s, EventArgs e) => TryConnect();

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
            AppendStatus("Forbindelsen mistet - prøver igen...");
            return;
        }

        // Update telemetry display
        SpeedText.Text      = ((int)tele.SpeedKmh).ToString();
        SpeedLimitText.Text = tele.NavSpeedLimitKmh > 0.1f
            ? ((int)tele.NavSpeedLimitKmh).ToString()
            : "--";
        NavDistText.Text = tele.NavDistanceM > 0f
            ? ((int)tele.NavDistanceM).ToString()
            : "--";

        // Run autopilot if active
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

    private void ToggleAutopilot()
    {
        if (_autopilotActive)
            DeactivateAutopilot();
        else
            ActivateAutopilot();
    }

    private void ActivateAutopilot()
    {
        if (!_telemetry.IsConnected) return;

        _engine = new AutopilotEngine(BuildSettings());
        _engine.Activate();
        _autopilotActive = true;

        ActivateButton.Content = "DEAKTIVER AUTOPILOT  [F5]";
        ActivateButton.Background = new SolidColorBrush(Color.FromRgb(0x9b, 0x2, 0x26));
        AppendStatus("Autopilot aktiveret!");
    }

    private void DeactivateAutopilot()
    {
        _engine?.Deactivate();
        _autopilotActive = false;
        _vjoy.Center();

        ActivateButton.Content    = "AKTIVER AUTOPILOT  [F5]";
        ActivateButton.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3e));
        StatusText.Text = "Autopilot deaktiveret";
        AppendStatus("Autopilot deaktiveret.");
    }

    private AutopilotSettings BuildSettings() => new()
    {
        MaxSpeedKmh            = (float)MaxSpeedSlider.Value,
        TurnApproachDistanceM  = (float)TurnDistSlider.Value,
        EnableSpeedLimitFollow = SpeedLimitCheck.IsChecked == true,
        EnableLaneKeeping      = LaneKeepingCheck.IsChecked == true,
    };

    private void UpdateActivateButton(AutopilotState state)
    {
        ActivateButton.Background = state switch
        {
            AutopilotState.Active      => new SolidColorBrush(Color.FromRgb(0x9b, 0x2, 0x26)),
            AutopilotState.Approaching => new SolidColorBrush(Color.FromRgb(0xb5, 0x6a, 0)),
            AutopilotState.Paused      => new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            _                          => new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3e)),
        };
    }

    private void SetConnectionUI(bool connected)
    {
        StatusDot.Fill      = new SolidColorBrush(connected ? Color.FromRgb(0, 200, 100) : Color.FromRgb(180, 30, 30));
        ConnectionText.Text = connected ? "Forbundet til ETS2" : "Ikke forbundet - start ETS2 med plugin";
    }

    private void AppendStatus(string msg)
    {
        StatusText.Text = $"[{DateTime.Now:HH:mm:ss}] {msg}";
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
        if (_autopilotActive) _vjoy.Center();
        _vjoy.Dispose();
        _telemetry.Dispose();
        base.OnClosed(e);
    }
}
