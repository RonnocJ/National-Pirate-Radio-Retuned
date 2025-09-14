using UnityEngine;
using System;
using System.Collections;
public enum GearState
{
    Neutral,
    Running,
    CheckingChange,
    Changing
};
public class VanController : Singleton<VanController>
{
    [Header("Stats (Readonly)")]
    [SerializeField] private float currentSpeed;
    [SerializeField] private float currentWheelRPM;
    [SerializeField] private float currentEngineRPM;
    [SerializeField] private float currentTorque;
    [SerializeField] private int currentGear;
    [Header("Motor Settings")]
    [SerializeField] private float motorForce;
    [SerializeField] private float brakeForce;
    [SerializeField] private AnimationCurve hpToRPMCurve;
    [Header("Gear Settings")]
    [SerializeField] private float redLine;
    [SerializeField] private float idleRPM;
    [SerializeField] private float[] gearRatios;
    [SerializeField] private float increaseGearRPM;
    [SerializeField] private float decreaseGearRPM;
    [SerializeField] private float changeGearTime;
    [SerializeField] private float differentialRatio;
    [Header("Auto Clutch Settings")]
    [SerializeField] private float clutchLockSpeed = 8f;
    [SerializeField] private float minCoupling = 0.25f;
    [SerializeField] private float rpmResponse = 5f;
    [Header("Rigidbody Settings")]
    public Rigidbody PlayerRb;
    [SerializeField] private float maxSpeed;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private AnimationCurve motorBoostCurve;
    [Header("Steering Settings")]
    [SerializeField] private float steerMultiplier;
    [SerializeField] private float maxSteering;
    [Header("Wheel References")]
    [SerializeField] private WheelColliders wheelColliders;
    [SerializeField] private WheelMeshes wheelMeshes;
    private bool _stopController;
    private GearState gearState;
    private float _steerAngle;
    private float _slipAngle => Vector3.Angle(transform.forward, PlayerRb.linearVelocity - transform.forward);
    private float _brakeInput => PInputManager.root.actions[PlayerActionType.Brake].fValue;
    private Vector2 _driveInput => PInputManager.root.actions[PlayerActionType.Drive].v2Value;
    private Coroutine _upShiftRoutine;
    private Coroutine _downShiftRoutine;
    private void Start()
    {
        PlayerRb = GetComponent<Rigidbody>();
        PlayerRb.maxLinearVelocity = maxSpeed;

        AudioManager.root.PlaySound(AudioEvent.playVanEngine, gameObject);
        AudioManager.root.SetSwitch(AudioSwitch.Engine_BREAK_Started, gameObject);

        VanDamage.root.OnPlayerDie += () =>
        {
            _stopController = true;
            AudioManager.root.SetSwitch(AudioSwitch.Engine_BREAK_Stopped, gameObject);
        };
    }
    private void FixedUpdate()
    {
        PlayerRb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);

        HandleAudio();

        if (!_stopController)
        {
            ApplyMotor();
            ApplyBrakes();
            ApplySteering();
            ApplyWheelPos();
        }
        else
        {
            ApplyBrakes(true);
        }
    }
    private void ApplyMotor()
    {
        if (Mathf.Abs(_driveInput.y) > 0)
        {
            gearState = GearState.Running;
        }

        if (gearState == GearState.Neutral && Mathf.Abs(_driveInput.y) > 0)
        {
            gearState = GearState.Running;
        }

        if (currentEngineRPM < idleRPM + 200 && _driveInput.y == 0 && currentGear == 0)
        {
            gearState = GearState.Neutral;
        }
        if (gearState == GearState.Running)
        {
            if (currentEngineRPM > increaseGearRPM && _upShiftRoutine == null)
            {
                _upShiftRoutine = StartCoroutine(ChangeGear(1));
                if (_downShiftRoutine != null) StopCoroutine(_downShiftRoutine);
            }
            else if (currentEngineRPM < decreaseGearRPM && _downShiftRoutine == null)
            {
                _downShiftRoutine = StartCoroutine(ChangeGear(-1));
                if (_upShiftRoutine != null) StopCoroutine(_upShiftRoutine);
            }
        }

        // Convert average drive-wheel RPM to engine-equivalent RPM via gearing
        currentWheelRPM = Mathf.Abs((wheelColliders.WheelBL.rpm + wheelColliders.WheelBR.rpm) / 2f) * gearRatios[currentGear] * differentialRatio;

        // Automatic "torque converter" style coupling (no manual clutch input)
        float throttle = Mathf.Clamp01(Mathf.Abs(_driveInput.y));
        float freeRevRPM = Mathf.Lerp(idleRPM, redLine, throttle); // engine free-rev target from throttle

        // As speed increases, coupling increases from minCoupling -> 1
        float speed = PlayerRb.linearVelocity.magnitude; // m/s
        float lockFactor = Mathf.InverseLerp(0.1f, Mathf.Max(0.1f, clutchLockSpeed), speed);
        float coupling = Mathf.Clamp01(Mathf.Lerp(minCoupling, 1f, lockFactor));

        // Target RPM blends from free-rev at low speed to wheel-driven RPM when coupled
        float wheelDrivenRPM = Mathf.Max(idleRPM - 100f, currentWheelRPM);
        float rpmTarget = Mathf.Lerp(freeRevRPM, wheelDrivenRPM, coupling);
        currentEngineRPM = Mathf.Lerp(currentEngineRPM <= 1f ? idleRPM : currentEngineRPM, rpmTarget, Time.deltaTime * rpmResponse);
        currentEngineRPM = Mathf.Clamp(currentEngineRPM, idleRPM, redLine * 1.1f);

        // Compute engine torque to wheels; guard against very low RPM
        float rpmForTorque = Mathf.Max(100f, currentEngineRPM);
        currentTorque = hpToRPMCurve.Evaluate(currentEngineRPM / redLine) * motorForce * gearRatios[currentGear] * differentialRatio * 5252f / rpmForTorque;

        wheelColliders.WheelBL.motorTorque = currentTorque * _driveInput.y;
        wheelColliders.WheelBR.motorTorque = currentTorque * _driveInput.y;

        PlayerRb.AddForce(transform.forward * motorBoostCurve.Evaluate(PlayerRb.linearVelocity.magnitude) * _driveInput.y, ForceMode.Acceleration);
    }
    IEnumerator ChangeGear(int gearChange)
    {
        gearState = GearState.CheckingChange;
        if (currentGear + gearChange >= 0)
        {
            if (gearChange > 0)
            {
                //increase the gear
                yield return new WaitForSeconds(0.7f);
                if (currentEngineRPM < increaseGearRPM || currentGear >= gearRatios.Length - 1)
                {
                    gearState = GearState.Running;
                    yield break;
                }
            }
            if (gearChange < 0)
            {
                //decrease the gear
                yield return new WaitForSeconds(0.1f);

                if (currentEngineRPM > decreaseGearRPM || currentGear <= 0)
                {
                    gearState = GearState.Running;
                    yield break;
                }
            }
            gearState = GearState.Changing;
            yield return new WaitForSeconds(changeGearTime);
            currentGear += gearChange;
        }

        if (gearState != GearState.Neutral)
            gearState = GearState.Running;

        _upShiftRoutine = null;
        _downShiftRoutine = null;
    }
    private void ApplyBrakes(bool autoBrake = false)
    {
        wheelColliders.WheelFL.brakeTorque = autoBrake ? 1 : _brakeInput * brakeForce;
        wheelColliders.WheelFR.brakeTorque = autoBrake ? 1 : _brakeInput * brakeForce;
        wheelColliders.WheelBL.brakeTorque = autoBrake ? 1 : _brakeInput * brakeForce * 0.6f;
        wheelColliders.WheelBR.brakeTorque = autoBrake ? 1 : _brakeInput * brakeForce * 0.6f;
    }
    private void ApplySteering()
    {
        _steerAngle = _driveInput.x * steerMultiplier;

        if (_slipAngle < 120f)
        {
            _steerAngle += Vector3.SignedAngle(transform.forward, PlayerRb.linearVelocity + transform.forward, Vector3.up);
        }

        _steerAngle = Mathf.Clamp(_steerAngle, -maxSteering, maxSteering);

        wheelColliders.WheelFL.steerAngle = _steerAngle;
        wheelColliders.WheelFR.steerAngle = _steerAngle;
    }
    private void ApplyWheelPos()
    {
        UpdateWheel(wheelColliders.WheelBL, wheelMeshes.WheelBL);
        UpdateWheel(wheelColliders.WheelBR, wheelMeshes.WheelBR);
        UpdateWheel(wheelColliders.WheelFL, wheelMeshes.WheelFL);
        UpdateWheel(wheelColliders.WheelFR, wheelMeshes.WheelFR);
    }
    private void UpdateWheel(WheelCollider col, MeshRenderer mesh)
    {
        Vector3 pos;
        Quaternion rot;
        col.GetWorldPose(out pos, out rot);
        mesh.transform.position = pos;
        mesh.transform.rotation = rot;
    }

    private void HandleAudio()
    {
        AudioManager.root.SetRTPC(AudioRTPC.Engine_RPM, currentEngineRPM);
        AudioManager.root.SetRTPC(AudioRTPC.Engine_Throttle, _driveInput.y > 0f? 1f : 0f);
    }
}
[Serializable]
public class WheelColliders
{
    public WheelCollider WheelBL;
    public WheelCollider WheelBR;
    public WheelCollider WheelFL;
    public WheelCollider WheelFR;
}
[Serializable]
public class WheelMeshes
{
    public MeshRenderer WheelBL;
    public MeshRenderer WheelBR;
    public MeshRenderer WheelFL;
    public MeshRenderer WheelFR;
}