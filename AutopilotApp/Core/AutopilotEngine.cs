namespace ETS2Autopilot.Core;

public enum AutopilotState
{
    Inactive,
    Active,
    Paused,      // Game paused or speed = 0
    Approaching, // Nearing a turn
    Parking
}

public class AutopilotSettings
{
    // Speed
    public float MaxSpeedKmh       { get; set; } = 90f;
    public float SpeedLimitOffsetKmh { get; set; } = 0f; // +/- from limit
    public float ApproachSlowdownKmh { get; set; } = 60f; // speed before turns

    // PID tuning - Steering
    public float SteerKp { get; set; } = 0.6f;
    public float SteerKi { get; set; } = 0.02f;
    public float SteerKd { get; set; } = 0.15f;

    // PID tuning - Speed
    public float SpeedKp { get; set; } = 0.5f;
    public float SpeedKi { get; set; } = 0.05f;
    public float SpeedKd { get; set; } = 0.1f;

    // Safety
    public float TurnApproachDistanceM { get; set; } = 300f;
    public bool  EnableSpeedLimitFollow { get; set; } = true;
    public bool  EnableLaneKeeping      { get; set; } = true;
}

public class AutopilotOutput
{
    public float Steering   { get; set; } // -1.0 to 1.0
    public float Throttle   { get; set; } // 0.0 to 1.0
    public float Brake      { get; set; } // 0.0 to 1.0
    public AutopilotState State { get; set; }
    public string StatusText { get; set; } = "";
}

/// <summary>
/// Core autopilot logic: computes steering and throttle from telemetry.
/// Heading-based lane keeping + PID speed controller.
/// </summary>
public class AutopilotEngine
{
    private readonly AutopilotSettings _settings;
    private readonly PidController     _steerPid;
    private readonly PidController     _speedPid;

    // Heading history for lane-keeping smoothing
    private readonly Queue<float> _headingHistory = new(10);
    private float _targetHeading;
    private bool  _targetHeadingSet;

    private DateTime _lastUpdate = DateTime.Now;

    public AutopilotState State { get; private set; } = AutopilotState.Inactive;

    public AutopilotEngine(AutopilotSettings? settings = null)
    {
        _settings = settings ?? new AutopilotSettings();

        _steerPid = new PidController(
            _settings.SteerKp, _settings.SteerKi, _settings.SteerKd,
            outputMin: -1f, outputMax: 1f
        );

        _speedPid = new PidController(
            _settings.SpeedKp, _settings.SpeedKi, _settings.SpeedKd,
            outputMin: -1f, outputMax: 1f
        );
    }

    public void Activate()
    {
        State = AutopilotState.Active;
        _steerPid.Reset();
        _speedPid.Reset();
        _targetHeadingSet = false;
        _headingHistory.Clear();
    }

    public void Deactivate()
    {
        State = AutopilotState.Inactive;
        _steerPid.Reset();
        _speedPid.Reset();
    }

    public AutopilotOutput Update(TelemetryData tele)
    {
        var now = DateTime.Now;
        float dt = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;
        dt = Math.Clamp(dt, 0.001f, 0.1f);

        if (State == AutopilotState.Inactive)
            return new AutopilotOutput { State = State, StatusText = "Autopilot inaktiv" };

        if (tele.GamePaused || tele.SpeedKmh < 0.5f && State == AutopilotState.Active)
        {
            State = AutopilotState.Paused;
            return new AutopilotOutput { State = State, StatusText = "Spillet sat på pause" };
        }

        if (State == AutopilotState.Paused && tele.SpeedKmh >= 0.5f)
            State = AutopilotState.Active;

        // --- Target speed ---
        float targetSpeedKmh = _settings.MaxSpeedKmh;

        if (_settings.EnableSpeedLimitFollow && tele.NavSpeedLimitKmh > 0.1f)
        {
            float limitKmh = tele.NavSpeedLimitKmh + _settings.SpeedLimitOffsetKmh;
            targetSpeedKmh = Math.Min(targetSpeedKmh, limitKmh);
        }

        // Slow down approaching turns
        bool nearTurn = tele.NavDistanceM > 0 && tele.NavDistanceM < _settings.TurnApproachDistanceM;
        if (nearTurn)
        {
            float blend = tele.NavDistanceM / _settings.TurnApproachDistanceM;
            float slowSpeed = _settings.ApproachSlowdownKmh +
                              (targetSpeedKmh - _settings.ApproachSlowdownKmh) * blend;
            targetSpeedKmh = Math.Min(targetSpeedKmh, slowSpeed);
            State = AutopilotState.Approaching;
        }
        else
        {
            State = AutopilotState.Active;
        }

        // --- Speed PID ---
        float speedError_kmh = targetSpeedKmh - tele.SpeedKmh;
        float rawSpeedOut    = _speedPid.Compute(targetSpeedKmh, tele.SpeedKmh, dt);

        float throttle = Math.Max(0f, rawSpeedOut);
        float brake    = Math.Max(0f, -rawSpeedOut);

        // Hard brake if way over limit
        if (tele.SpeedKmh > targetSpeedKmh + 10f)
        {
            brake    = Math.Min(1f, (tele.SpeedKmh - targetSpeedKmh) / 20f);
            throttle = 0f;
        }

        // --- Steering / Lane keeping ---
        float steer = 0f;

        if (_settings.EnableLaneKeeping && tele.SpeedKmh > 5f)
        {
            // Accumulate heading samples to get a smooth target
            _headingHistory.Enqueue(tele.HeadingDeg);
            if (_headingHistory.Count > 8) _headingHistory.Dequeue();

            if (!_targetHeadingSet)
            {
                _targetHeading    = tele.HeadingDeg;
                _targetHeadingSet = true;
            }

            // Slowly update target heading toward current (follow GPS direction gradually)
            float headingAvg = CircularMean(_headingHistory);
            _targetHeading = LerpAngle(_targetHeading, headingAvg, dt * 0.3f);

            // Correction = difference between target and actual heading
            float headingError = AngleDiff(_targetHeading, tele.HeadingDeg);

            // Scale sensitivity with speed
            float sensitivity = 1f + (tele.SpeedKmh / 120f) * 0.5f;
            steer = _steerPid.Compute(0f, headingError * sensitivity, dt);
        }

        string status = State == AutopilotState.Approaching
            ? $"Nærmer sig sving ({tele.NavDistanceM:F0}m) — {targetSpeedKmh:F0} km/h"
            : $"Autopilot aktiv — {tele.SpeedKmh:F0}/{targetSpeedKmh:F0} km/h";

        return new AutopilotOutput
        {
            Steering   = steer,
            Throttle   = throttle,
            Brake      = brake,
            State      = State,
            StatusText = status
        };
    }

    // --- Math helpers ---

    private static float AngleDiff(float a, float b)
    {
        float diff = ((b - a) % 360f + 540f) % 360f - 180f;
        return diff;
    }

    private static float LerpAngle(float a, float b, float t)
    {
        float diff = AngleDiff(a, b);
        return a + diff * Math.Clamp(t, 0f, 1f);
    }

    private static float CircularMean(IEnumerable<float> angles)
    {
        float sinSum = 0, cosSum = 0;
        foreach (float a in angles)
        {
            float rad = a * MathF.PI / 180f;
            sinSum += MathF.Sin(rad);
            cosSum += MathF.Cos(rad);
        }
        float mean = MathF.Atan2(sinSum, cosSum) * 180f / MathF.PI;
        return (mean + 360f) % 360f;
    }
}
