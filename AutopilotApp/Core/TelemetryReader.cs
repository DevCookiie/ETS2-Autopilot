using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace ETS2Autopilot.Core;

/// <summary>
/// Reads telemetry from scs-sdk-plugin shared memory (Local\SCSTelemetry).
/// Compatible with: https://github.com/RenCloud/scs-sdk-plugin
/// </summary>
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

    public float SpeedLimitKmh => NavSpeedLimitKmh;
}

public class TelemetryReader : IDisposable
{
    // scs-sdk-plugin shared memory name
    private const string MapName = "Local\\SCSTelemetry";

    // Byte offsets in the scs-sdk-plugin struct (v3, RenCloud fork)
    private const int OFF_PAUSED          = 4;
    private const int OFF_SPEED           = 20;  // float, m/s
    private const int OFF_ACCEL_X         = 24;
    private const int OFF_ACCEL_Y         = 28;
    private const int OFF_ACCEL_Z         = 32;
    // 4 bytes padding before doubles
    private const int OFF_COORD_X         = 40;  // double
    private const int OFF_COORD_Y         = 48;  // double
    private const int OFF_COORD_Z         = 56;  // double
    private const int OFF_ROT_X           = 64;  // float heading 0..1
    private const int OFF_ROT_Y           = 68;
    private const int OFF_ROT_Z           = 72;
    private const int OFF_GEAR            = 76;  // int
    private const int OFF_ENGINE_RPM      = 84;  // float
    private const int OFF_FUEL            = 92;  // float liters
    private const int OFF_USER_STEER      = 104; // float -1..1
    private const int OFF_USER_THROTTLE   = 108; // float 0..1
    private const int OFF_USER_BRAKE      = 112; // float 0..1
    private const int OFF_NAV_SPEED       = 156; // float m/s speed limit
    private const int OFF_NAV_DISTANCE    = 160; // float meters
    private const int OFF_CRUISE_CONTROL  = 200; // int (bool)
    private const int OFF_PARK_BRAKE      = 204; // int (bool)

    private const int MapSize = 5000;

    private MemoryMappedFile?         _mmf;
    private MemoryMappedViewAccessor? _view;
    private bool _disposed;

    public bool IsConnected { get; private set; }

    public bool TryConnect()
    {
        try
        {
            _mmf  = MemoryMappedFile.OpenExisting(MapName);
            _view = _mmf.CreateViewAccessor(0, MapSize);
            IsConnected = true;
            return true;
        }
        catch
        {
            IsConnected = false;
            return false;
        }
    }

    public TelemetryData Read()
    {
        if (_view == null)
            return default;

        try
        {
            var d = new TelemetryData();

            d.GamePaused    = _view.ReadInt32(OFF_PAUSED) != 0;
            float speedMs   = _view.ReadSingle(OFF_SPEED);
            d.SpeedKmh      = speedMs * 3.6f;
            d.WorldX        = _view.ReadDouble(OFF_COORD_X);
            d.WorldY        = _view.ReadDouble(OFF_COORD_Y);
            d.WorldZ        = _view.ReadDouble(OFF_COORD_Z);
            float rotX      = _view.ReadSingle(OFF_ROT_X); // 0..1 normalized heading
            d.HeadingDeg    = rotX * 360f;
            d.Gear          = _view.ReadInt32(OFF_GEAR);
            d.EngineRpm     = _view.ReadSingle(OFF_ENGINE_RPM);
            d.FuelLiters    = _view.ReadSingle(OFF_FUEL);
            d.Steering      = _view.ReadSingle(OFF_USER_STEER);
            d.Throttle      = _view.ReadSingle(OFF_USER_THROTTLE);
            d.Brake         = _view.ReadSingle(OFF_USER_BRAKE);
            float navMs     = _view.ReadSingle(OFF_NAV_SPEED);
            d.NavSpeedLimitKmh = navMs * 3.6f;
            d.NavDistanceM  = _view.ReadSingle(OFF_NAV_DISTANCE);
            d.CruiseControlOn = _view.ReadInt32(OFF_CRUISE_CONTROL) != 0;
            d.ParkBrakeOn   = _view.ReadInt32(OFF_PARK_BRAKE) != 0;
            d.IsConnected   = true;

            IsConnected = true;
            return d;
        }
        catch
        {
            IsConnected = false;
            return default;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _view?.Dispose();
        _mmf?.Dispose();
    }
}
