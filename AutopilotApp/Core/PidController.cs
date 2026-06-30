namespace ETS2Autopilot.Core;

/// <summary>
/// Generic PID controller used for both steering and speed regulation.
/// </summary>
public class PidController
{
    private readonly float _kp;
    private readonly float _ki;
    private readonly float _kd;
    private readonly float _outputMin;
    private readonly float _outputMax;

    private float _integral;
    private float _previousError;
    private bool  _firstRun = true;

    public PidController(float kp, float ki, float kd,
                         float outputMin = -1f, float outputMax = 1f)
    {
        _kp = kp;
        _ki = ki;
        _kd = kd;
        _outputMin = outputMin;
        _outputMax = outputMax;
    }

    public float Compute(float setpoint, float measured, float dt)
    {
        if (dt <= 0f) return 0f;

        float error = setpoint - measured;

        if (_firstRun)
        {
            _previousError = error;
            _firstRun = false;
        }

        _integral += error * dt;
        // Anti-windup clamp
        _integral = Math.Clamp(_integral, _outputMin / _ki, _outputMax / _ki);

        float derivative = (error - _previousError) / dt;
        _previousError = error;

        float output = _kp * error + _ki * _integral + _kd * derivative;
        return Math.Clamp(output, _outputMin, _outputMax);
    }

    public void Reset()
    {
        _integral     = 0f;
        _previousError = 0f;
        _firstRun     = true;
    }
}
