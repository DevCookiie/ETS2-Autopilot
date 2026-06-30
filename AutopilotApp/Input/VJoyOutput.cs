using System.Runtime.InteropServices;

namespace ETS2Autopilot.Input;

/// <summary>
/// Sends virtual joystick (vJoy) input to ETS2.
/// Requires vJoy driver installed: https://github.com/jshafer817/vJoy
///
/// vJoy axis range: 0x1 to 0x8000 (center = 0x4000)
/// </summary>
public class VJoyOutput : IDisposable
{
    private const uint DeviceId  = 1;
    private const long AxisCenter = 0x4000;
    private const long AxisMax    = 0x7FFF;
    private const long AxisMin    = 0x0001;

    private bool _acquired;
    private bool _disposed;

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool vJoyEnabled();

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool AcquireVJD(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void RelinquishVJD(uint rID);

    [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SetAxis(long Value, uint rID, uint Axis);

    // Axis IDs from vJoy SDK
    private const uint HID_USAGE_X  = 0x30; // Steering
    private const uint HID_USAGE_Y  = 0x31; // Throttle/Brake combined (not used here)
    private const uint HID_USAGE_RZ = 0x35; // Throttle
    private const uint HID_USAGE_Z  = 0x32; // Brake

    public bool IsAvailable { get; private set; }

    public bool Initialize()
    {
        try
        {
            if (!vJoyEnabled())
            {
                IsAvailable = false;
                return false;
            }

            _acquired    = AcquireVJD(DeviceId);
            IsAvailable  = _acquired;
            return _acquired;
        }
        catch
        {
            IsAvailable = false;
            return false;
        }
    }

    /// <param name="steering">-1.0 (full left) to 1.0 (full right)</param>
    /// <param name="throttle">0.0 to 1.0</param>
    /// <param name="brake">0.0 to 1.0</param>
    public void Send(float steering, float throttle, float brake)
    {
        if (!_acquired) return;

        long steerVal    = FloatToAxis(steering);
        long throttleVal = FloatToAxisPositive(throttle);
        long brakeVal    = FloatToAxisPositive(brake);

        SetAxis(steerVal,    DeviceId, HID_USAGE_X);
        SetAxis(throttleVal, DeviceId, HID_USAGE_RZ);
        SetAxis(brakeVal,    DeviceId, HID_USAGE_Z);
    }

    public void Center()
    {
        if (!_acquired) return;
        SetAxis(AxisCenter, DeviceId, HID_USAGE_X);
        SetAxis(AxisMin,    DeviceId, HID_USAGE_RZ);
        SetAxis(AxisMin,    DeviceId, HID_USAGE_Z);
    }

    private static long FloatToAxis(float value)
    {
        float clamped = Math.Clamp(value, -1f, 1f);
        return (long)(AxisCenter + clamped * (AxisMax - AxisCenter));
    }

    private static long FloatToAxisPositive(float value)
    {
        float clamped = Math.Clamp(value, 0f, 1f);
        return (long)(AxisMin + clamped * (AxisMax - AxisMin));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_acquired)
        {
            Center();
            RelinquishVJD(DeviceId);
        }
    }
}
