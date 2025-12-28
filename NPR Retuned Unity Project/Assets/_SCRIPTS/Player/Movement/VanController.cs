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
    [SerializeField] private float initalBoostForce;
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
    [SerializeField] private WheelEffects wheelParticles;
    [Header("Wheel FX Settings")]
    [SerializeField] private float particleSlipThreshold;
    [SerializeField] private LayerMask grassMask;
    [Header("Misc References")]
    [SerializeField] private Transform needleTr;
    [SerializeField] private Material trunkMat;
    [SerializeField] private ParticleSystem initialBoostParticles;
    private bool _stopController;
    private bool _boosted;
    private GearState gearState;
    private float _steerAngle;
    private float _slipAngle => Vector3.Angle(transform.forward, PlayerRb.linearVelocity);
    private Coroutine _upShiftRoutine;
    private Coroutine _downShiftRoutine;
    private void Start()
    {
        PlayerRb = GetComponent<Rigidbody>();
        PlayerRb.maxLinearVelocity = maxSpeed;

        PInputManager.root.actions[PlayerActionType.Drive].onV2ValueChange += ApplyInitialBoost;

        AudioManager.root.PlaySound(AudioEvent.playVanEngine, gameObject);
        AudioManager.root.SetSwitch(AudioSwitch.Engine_BREAK_Started, gameObject);

        AudioManager.root.PlaySound(AudioEvent.playTireSqueal, wheelMeshes.WheelBL.gameObject, 1);
        AudioManager.root.PlaySound(AudioEvent.playTireSqueal, wheelMeshes.WheelBR.gameObject, 1);
        AudioManager.root.PlaySound(AudioEvent.playTireSqueal, wheelMeshes.WheelFL.gameObject, 1);
        AudioManager.root.PlaySound(AudioEvent.playTireSqueal, wheelMeshes.WheelFR.gameObject, 1);

        GameManager.root.OnPauseSwitch += c => { if (c) InAutopilot = false; };
        GameManager.root.OnPStateSwitch += c => { if (c != PlayerState.Weapon) InAutopilot = false; };

        VanDamage.root.OnPlayerDie += () =>
        {
            _stopController = true;
            AudioManager.root.SetSwitch(AudioSwitch.Engine_BREAK_Stopped, gameObject);
        };

        if (!PlayerStats.root.NewGame) RegisterAutopilotActions();
    }
    public void RegisterAutopilotActions()
    {
        PInputManager.root.actions[PlayerActionType.Find].bAction += () =>
        {
            if (GameManager.root.CurrentPState == PlayerState.Weapon && (!PlayerStats.root.NewGame || Tutorial.root.Iteration > 2) && !GameManager.root.Paused)
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
            ApplyWheels();
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
        currentEngineRPM = Mathf.Lerp(currentEngineRPM <= 1f ? idleRPM : currentEngineRPM, rpmTarget, Time.fixedDeltaTime * rpmResponse);
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
    private void ApplyInitialBoost(Vector2 driveInput)
    {
        if (GameManager.root.CurrentPState is PlayerState.Start or PlayerState.Dead) return;
        if (_stopController) return;
        if (InAutopilot) return;

        if (!_boosted && driveInput.y > 0f && PlayerRb.linearVelocity.magnitude < 10f)
        {
            PlayerRb.AddForce(transform.forward * initalBoostForce, ForceMode.Impulse);
            initialBoostParticles.Play();
        }
        if (driveInput.y > 0f) _boosted = true;
        else if (driveInput.y <= 0f) _boosted = false;
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
        float input = autoBrake ? 1f : BrakeInput;
        wheelColliders.WheelFL.brakeTorque = input * brakeForce;
        wheelColliders.WheelFR.brakeTorque = input * brakeForce;
        wheelColliders.WheelBL.brakeTorque = input * brakeForce * 0.6f;
        wheelColliders.WheelBR.brakeTorque = input * brakeForce * 0.6f;

        trunkMat.SetColor("_EmissionColor", ((input * 800f) + 0.05f) * Color.red);
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
    private void ApplyWheels()
    {
        UpdateWheel(wheelColliders.WheelBL, wheelMeshes.WheelBL, wheelParticles.WheelBL);
        UpdateWheel(wheelColliders.WheelBR, wheelMeshes.WheelBR, wheelParticles.WheelBR);
        UpdateWheel(wheelColliders.WheelFL, wheelMeshes.WheelFL, wheelParticles.WheelFL);
        UpdateWheel(wheelColliders.WheelFR, wheelMeshes.WheelFR, wheelParticles.WheelFR);
    }
    private void UpdateWheel(WheelCollider col, MeshRenderer mesh, WheelFX fx)
    {
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.transform.position = pos;
        mesh.transform.rotation = rot;

        fx.Smoke.transform.parent.position = pos - mesh.transform.forward - (mesh.transform.up * 0.25f);
        fx.Smoke.transform.parent.localRotation = Quaternion.Euler(-30f, col.steerAngle + 180f, 0f);

        if (col.GetGroundHit(out WheelHit hit))
        {
            float slipAmount = Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);

            if (slipAmount >= particleSlipThreshold)
            {
                if (!fx.Smoke.isPlaying) fx.Smoke.Play();
            }
            else if (fx.Smoke.isPlaying)
            {
                fx.Smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (hit.collider.gameObject.layer == 6)
            {
                if (!fx.Dirt.isPlaying) fx.Dirt.Play();

                ParticleSystem.EmissionModule emission = fx.Dirt.emission;
                emission.rateOverTime = PlayerRb.linearVelocity.magnitude * 10f;
            }
            else
            {
                fx.Dirt.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            AudioManager.root.SetRTPC(AudioRTPC.WheelFriction, slipAmount, false, AudioEvent.playTireSqueal, mesh.gameObject);
        }
        else
        {
            fx.Smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            fx.Dirt.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            AudioManager.root.SetRTPC(AudioRTPC.WheelFriction, 0, false, AudioEvent.playTireSqueal, mesh.gameObject);
        }
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
[Serializable]
public class WheelEffects
{
    public WheelFX WheelBL;
    public WheelFX WheelBR;
    public WheelFX WheelFL;
    public WheelFX WheelFR;
}
[Serializable]
public class WheelFX
{
    public ParticleSystem Smoke;
    public ParticleSystem Dirt;
}