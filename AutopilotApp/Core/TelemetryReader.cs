using SCSSdkClient;
using SCSSdkClient.Object;

namespace ETS2Autopilot.Core;

public struct TelemetryData
{
    public bool   IsConnected;
    public bool   GamePaused;
    public float  SpeedKmh;
    public float  HeadingDeg;    // 0-360
    public double WorldX;
    public double WorldY;
    public double WorldZ;
    public int    Gear;
    public float  Throttle;
    public float  Brake;
    public float  Steering;      // -1.0 to 1.0
    public float  NavDistanceM;
    public float  NavSpeedLimitKmh;
    public float  FuelLiters;
    public float  EngineRpm;
    public bool   ParkBrakeOn;
    public bool   CruiseControlOn;
}

/// <summary>
/// Reads telemetry using the official SCSSdkClient wrapper (bundled with
/// RenCloud's scs-sdk-plugin) instead of manually parsing shared memory.
/// </summary>
public class TelemetryReader : IDisposable
{
    private SCSSdkTelemetry? _client;
    private TelemetryData    _latest;
    private bool _disposed;
    private bool _everReceived;

    public bool IsConnected { get; private set; }

    public bool TryConnect()
    {
        if (_client != null) return IsConnected;

        try
        {
            _client = new SCSSdkTelemetry();
            _client.Data += OnData;
            // Connection itself succeeds even before the game sends data;
            // IsConnected flips true once the first frame arrives.
            return true;
        }
        catch
        {
            _client = null;
            IsConnected = false;
            return false;
        }
    }

    private void OnData(SCSTelemetry data, bool newTimestamp)
    {
        _everReceived = true;
        IsConnected   = data.SdkActive;

        var truckCurrent = data.TruckValues.CurrentValues;
        var dashboard    = truckCurrent.DashboardValues;
        var position     = truckCurrent.PositionValue;
        var input         = data.ControlValues.InputValues;
        var nav           = data.NavigationValues;

        _latest = new TelemetryData
        {
            IsConnected      = data.SdkActive,
            GamePaused       = data.Paused,
            SpeedKmh         = dashboard.Speed.Kph,
            HeadingDeg       = position.Orientation.Heading * 360f,
            WorldX           = position.Position.X,
            WorldY           = position.Position.Y,
            WorldZ           = position.Position.Z,
            Gear             = truckCurrent.MotorValues.GearValues.Selected,
            Throttle         = input.Throttle,
            Brake            = input.Brake,
            Steering         = input.Steering,
            NavDistanceM     = nav.NavigationDistance,
            NavSpeedLimitKmh = nav.SpeedLimit.Kph,
            FuelLiters       = dashboard.FuelValue.Amount,
            EngineRpm        = dashboard.RPM,
            ParkBrakeOn      = truckCurrent.MotorValues.BrakeValues.ParkingBrake,
            CruiseControlOn  = dashboard.CruiseControl,
        };
    }

    public TelemetryData Read()
    {
        // No data received yet (game not running / plugin not loaded)
        if (!_everReceived)
        {
            IsConnected = false;
            return default;
        }
        return _latest;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_client != null)
        {
            _client.Data -= OnData;
            _client.Dispose();
            _client = null;
        }
    }
}
