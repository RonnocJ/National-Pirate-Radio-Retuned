using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyVehicle : Enemy
{
    public float BrakeInput;
    public Vector2 DriveInput;
    [Header("Stats (Readonly)")]
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
    [SerializeField] private float maxSpeed;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float slopeMultiplier;
    [SerializeField] private LayerMask terrainMask;
    [SerializeField] private AnimationCurve motorBoostCurve;
    [Header("Steering Settings")]
    [SerializeField] private float lateralCorrectionForce;
    [SerializeField] private float steerMultiplier;
    [SerializeField] private float maxSteering;
    [Header("Autopilot Settings")]
    [SerializeField] private VehicleAutopilot autopilot;
    [Header("Wheel References")]
    [SerializeField] private WheelColliders wheelColliders;
    [SerializeField] private WheelMeshes wheelMeshes;
    [Header("Visual References")]
    [SerializeField] private MeshRenderer[] bodyRens;
    [SerializeField] private MeshRenderer[] wheelRens;
    private GearState gearState;
    private float _steerAngle;
    private float _slipAngle => Vector3.Angle(transform.forward, _rb.linearVelocity - transform.forward);
    private Coroutine _upShiftRoutine;
    private Coroutine _downShiftRoutine;
    private void Start()
    {
        _rb.maxLinearVelocity = maxSpeed;
    }
    public override void Spawn()
    {
        base.Spawn();
        _repathTimer = repathInterval;
    }
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        _rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);

        var inputs = autopilot.DriveToTarget(0.5f, _target.position);

        DriveInput = inputs.driveInput;
        BrakeInput = inputs.brakeInput;

        ApplyMotor();
        ApplyBrakes();
        ApplySteering();
        ApplyWheelPos();
        HandleAudio();
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

        float speed = _rb.linearVelocity.magnitude;
        float coupling = Mathf.Clamp01(Mathf.Lerp(minCoupling, 1f, Mathf.InverseLerp(0.1f, Mathf.Max(0.1f, clutchLockSpeed), speed)));

        float rpmTarget = Mathf.Lerp(Mathf.Lerp(idleRPM, redLine, Mathf.Clamp01(Mathf.Abs(DriveInput.y))), Mathf.Max(idleRPM - 100f, currentWheelRPM), coupling);
        currentEngineRPM = Mathf.Lerp(currentEngineRPM <= 1f ? idleRPM : currentEngineRPM, rpmTarget, Time.deltaTime * rpmResponse);
        currentEngineRPM = Mathf.Clamp(currentEngineRPM, idleRPM, redLine * 1.1f);

        currentTorque = hpToRPMCurve.Evaluate(currentEngineRPM / redLine) * motorForce * gearRatios[currentGear] * differentialRatio * 5252f / Mathf.Max(100f, currentEngineRPM);

        wheelColliders.WheelBL.motorTorque = currentTorque * DriveInput.y;
        wheelColliders.WheelBR.motorTorque = currentTorque * DriveInput.y;

        if (_rb.isKinematic) return;

        _rb.AddForce(transform.forward * motorBoostCurve.Evaluate(speed) * DriveInput.y, ForceMode.Acceleration);

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit slopeHit, Mathf.Infinity, terrainMask))
        {
            float slopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
            Vector3 driveDirOnPlane = Vector3.ProjectOnPlane(transform.forward * Mathf.Sign(DriveInput.y), slopeHit.normal).normalized;
            float downhillAlongDrive = Vector3.Dot(Vector3.ProjectOnPlane(Vector3.down, slopeHit.normal).normalized, driveDirOnPlane);

            if (DriveInput.y != 0f && downhillAlongDrive < 0f && slopeAngle > 0.01f)
            {
                _rb.AddForce(driveDirOnPlane * slopeMultiplier * slopeAngle * -downhillAlongDrive, ForceMode.Acceleration);
            }
        }
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
        _rb.AddForce(transform.right * DriveInput.x * lateralCorrectionForce, ForceMode.Impulse);
        _steerAngle = DriveInput.x * steerMultiplier;

        if (_slipAngle < 120f)
        {
            _steerAngle += Vector3.SignedAngle(transform.forward, _rb.linearVelocity + transform.forward, Vector3.up);
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
        /*AudioManager.root.SetRTPC(AudioRTPC.Engine_RPM, currentEngineRPM);
        AudioManager.root.SetRTPC(AudioRTPC.Engine_Throttle, Mathf.Abs(DriveInput.y) > 0f ? 1f : 0f);*/
    }

    public override void DestroyEnemy(bool killedByPlayer)
    {
        if (_destroyed) return;
        
        base.DestroyEnemy(killedByPlayer);

        StartCoroutine(Dissolve());
        ExplosionManager.root.SparkExplode(transform.position);
    }
    private IEnumerator Dissolve()
    {
        float timer = 0f;

        while (timer <= 1f)
        {
            MaterialPropertyBlock bBlock = new MaterialPropertyBlock();
            MaterialPropertyBlock wBlock = new MaterialPropertyBlock();

            bBlock.SetFloat("_Dissolve", timer / 1f);
            wBlock.SetFloat("_Dissolve", timer / 1f);

            foreach (var b in bodyRens)
            {
                b.SetPropertyBlock(bBlock);
            }

            foreach (var w in wheelRens)
            {
                w.SetPropertyBlock(wBlock);
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }
}
