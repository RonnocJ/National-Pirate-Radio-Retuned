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
    public bool InAutopilot;
    public float BrakeInput;
    public Vector2 DriveInput;
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
    [SerializeField] private float slopeMultiplier;
    [SerializeField] private LayerMask terrainMask;
    [SerializeField] private AnimationCurve motorBoostCurve;
    [Header("AutoPilot Settings")]
    [SerializeField] private VehicleAutopilot autopilot;
    [Header("Steering Settings")]
    [SerializeField] private float steerMultiplier;
    [SerializeField] private float maxSteering;
    [Header("Wheel References")]
    [SerializeField] private WheelColliders wheelColliders;
    [SerializeField] private WheelMeshes wheelMeshes;
    [Header("Misc References")]
    [SerializeField] private Transform needleTr;
    private bool _stopController;
    private GearState gearState;
    private float _steerAngle;
    private float _slipAngle => Vector3.Angle(transform.forward, PlayerRb.linearVelocity - transform.forward);
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

        if (!GameManager.root.NewGame) RegisterAutopilotActions();
    }
    public void RegisterAutopilotActions()
    {
        PInputManager.root.actions[PlayerActionType.Find].bAction += () =>
        {
            if (GameManager.root.CurrentPState == PlayerState.Weapon && (!GameManager.root.NewGame || Tutorial.root.Iteration > 2))
            {
                InAutopilot = !InAutopilot;
                if (InAutopilot) autopilot.RebuildDrivePath();
            }
        };
    }
    private void FixedUpdate()
    {
        PlayerRb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);

        HandleAudio();

        if (!InAutopilot)
        {
            DriveInput = PInputManager.root.actions[PlayerActionType.Drive].v2Value;
            BrakeInput = PInputManager.root.actions[PlayerActionType.Brake].fValue;
        }
        else
        {
            var inputs = autopilot.DriveToTarget(0.5f);

            DriveInput = inputs.driveInput;
            BrakeInput = inputs.brakeInput;
        }

        if (GameManager.root.CurrentPState is PlayerState.Start or PlayerState.Dead)
        {
            DriveInput = Vector2.zero;
            BrakeInput = 0;
        }

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
        if (Mathf.Abs(DriveInput.y) > 0)
        {
            gearState = GearState.Running;
        }

        if (gearState == GearState.Neutral && Mathf.Abs(DriveInput.y) > 0)
        {
            gearState = GearState.Running;
        }

        if (currentEngineRPM < idleRPM + 200 && DriveInput.y == 0 && currentGear == 0)
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

        currentWheelRPM = Mathf.Abs((wheelColliders.WheelBL.rpm + wheelColliders.WheelBR.rpm) / 2f) * gearRatios[currentGear] * differentialRatio;

        float speed = PlayerRb.linearVelocity.magnitude;
        float coupling = Mathf.Clamp01(Mathf.Lerp(minCoupling, 1f, Mathf.InverseLerp(0.1f, Mathf.Max(0.1f, clutchLockSpeed), speed)));

        float rpmTarget = Mathf.Lerp(Mathf.Lerp(idleRPM, redLine, Mathf.Clamp01(Mathf.Abs(DriveInput.y))), Mathf.Max(idleRPM - 100f, currentWheelRPM), coupling);
        currentEngineRPM = Mathf.Lerp(currentEngineRPM <= 1f ? idleRPM : currentEngineRPM, rpmTarget, Time.deltaTime * rpmResponse);
        currentEngineRPM = Mathf.Clamp(currentEngineRPM, idleRPM, redLine * 1.1f);

        currentTorque = hpToRPMCurve.Evaluate(currentEngineRPM / redLine) * motorForce * gearRatios[currentGear] * differentialRatio * 5252f / Mathf.Max(100f, currentEngineRPM);

        wheelColliders.WheelBL.motorTorque = currentTorque * DriveInput.y * PlayerStats.root.VehicleSpeed;
        wheelColliders.WheelBR.motorTorque = currentTorque * DriveInput.y * PlayerStats.root.VehicleSpeed;

        PlayerRb.AddForce(transform.forward * motorBoostCurve.Evaluate(speed) * DriveInput.y * PlayerStats.root.VehicleSpeed, ForceMode.Acceleration);

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit slopeHit, Mathf.Infinity, terrainMask))
        {
            float slopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
            Vector3 driveDirOnPlane = Vector3.ProjectOnPlane(transform.forward * Mathf.Sign(DriveInput.y), slopeHit.normal).normalized;
            float downhillAlongDrive = Vector3.Dot(Vector3.ProjectOnPlane(Vector3.down, slopeHit.normal).normalized, driveDirOnPlane);

            if (DriveInput.y != 0f && downhillAlongDrive < 0f && slopeAngle > 0.01f)
            {
                PlayerRb.AddForce(driveDirOnPlane * slopeMultiplier * slopeAngle * -downhillAlongDrive, ForceMode.Acceleration);
            }
        }

        needleTr.localRotation = Quaternion.Euler(Vector3.right * 13 + Vector3.forward * -((PlayerRb.linearVelocity.magnitude * 4) - 140));
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
        wheelColliders.WheelFL.brakeTorque = autoBrake ? 1 : BrakeInput * brakeForce;
        wheelColliders.WheelFR.brakeTorque = autoBrake ? 1 : BrakeInput * brakeForce;
        wheelColliders.WheelBL.brakeTorque = autoBrake ? 1 : BrakeInput * brakeForce * 0.6f;
        wheelColliders.WheelBR.brakeTorque = autoBrake ? 1 : BrakeInput * brakeForce * 0.6f;
    }
    private void ApplySteering()
    {
        _steerAngle = DriveInput.x * steerMultiplier;

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
        AudioManager.root.SetRTPC(AudioRTPC.Engine_Throttle, Mathf.Abs(DriveInput.y) > 0f ? 1f : 0f);
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
