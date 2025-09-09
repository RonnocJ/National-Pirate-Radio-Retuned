using UnityEngine;
using System;
using Unity.Collections;
public class VanController : Singleton<VanController>
{
    public float Speed;
    public Rigidbody PlayerRb;
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float motorForce;
    [SerializeField] private float maxSpeed;
    [SerializeField] private AnimationCurve motorBoostCurve;
    [SerializeField] private float brakeForce;
    [SerializeField] private float maxSteering;
    [SerializeField] private AnimationCurve steerCurve;
    [SerializeField] private WheelColliders wheelColliders;
    [SerializeField] private WheelMeshes wheelMeshes;
    private float _steerAngle;
    private float _speed => wheelColliders.WheelBR.rpm * wheelColliders.WheelBR.radius * 2f * Mathf.PI / 10f;
    private float _slipAngle => Vector3.Angle(transform.forward, PlayerRb.linearVelocity - transform.forward);
    private float _brakeInput => PInputManager.root.actions[PlayerActionType.Brake].fValue;
    private Vector2 _driveInput => PInputManager.root.actions[PlayerActionType.Drive].v2Value;
    private void Start()
    {
        PlayerRb = GetComponent<Rigidbody>();
        PlayerRb.maxLinearVelocity = maxSpeed;
    }
    private void FixedUpdate()
    {
        PlayerRb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);

        ApplyMotor();
        ApplySteering();
        ApplyBrakes();
        ApplyWheelPos();

        Speed = _speed;
    }
    private void ApplyMotor()
    {
        wheelColliders.WheelBL.motorTorque = _driveInput.y * motorForce;
        wheelColliders.WheelBR.motorTorque = _driveInput.y * motorForce;

        PlayerRb.AddForce(transform.forward * motorBoostCurve.Evaluate(PlayerRb.linearVelocity.magnitude) * _driveInput.y, ForceMode.Acceleration);
    }
    private void ApplySteering()
    {
        _steerAngle = steerCurve.Evaluate(_speed) * _driveInput.x;

        if (_slipAngle < 120f)
        {
            _steerAngle += Vector3.SignedAngle(transform.forward, PlayerRb.linearVelocity + transform.forward, Vector3.up);
        }

        _steerAngle = Mathf.Clamp(_steerAngle, -maxSteering, maxSteering);

        wheelColliders.WheelFL.steerAngle = _steerAngle;
        wheelColliders.WheelFR.steerAngle = _steerAngle;
    }
    private void ApplyBrakes()
    {
        wheelColliders.WheelFL.brakeTorque = _brakeInput * brakeForce;
        wheelColliders.WheelFR.brakeTorque = _brakeInput * brakeForce;
        wheelColliders.WheelBL.brakeTorque = _brakeInput * brakeForce * 0.6f;
        wheelColliders.WheelBR.brakeTorque = _brakeInput * brakeForce * 0.6f;
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