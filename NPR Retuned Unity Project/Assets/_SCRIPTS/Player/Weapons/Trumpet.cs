using UnityEngine;

public class Trumpet : VanWeapon
{
    [Header("Valve Settings")]
    [SerializeField] private float valveMoveSpeed;
    [SerializeField] private Transform valve01;
    [SerializeField] private Transform valve02;
    [SerializeField] private Transform valve03;
    [Header("Laser Settings")]
    [SerializeField] private int laserResolution;
    [SerializeField] private float beamCurveSpeed;
    [SerializeField] private LineRenderer laserBeam;
    private float _timer;
    private float _palmXRot;
    private Vector3 _valve01Target;
    private Vector3 _valve02Target;
    private Vector3 _valve03Target;
    protected override void Start()
    {
        base.Start();

        _valve01Target = valve01.localPosition;
        _valve02Target = valve02.localPosition;
        _valve03Target = valve03.localPosition;

        laserBeam.positionCount = laserResolution;
    }
    protected override void ToggleWeapon()
    {
        base.ToggleWeapon();
    }
    protected override void AimWeapon()
    {
        base.AimWeapon();
    }
    protected override void FireWeapon()
    {
        base.FireWeapon();

        _timer += Time.deltaTime;

        valve01.localPosition = Vector3.Lerp(valve01.localPosition, _valve01Target, Time.deltaTime / valveMoveSpeed * 2.5f);
        valve02.localPosition = Vector3.Lerp(valve02.localPosition, _valve02Target, Time.deltaTime / valveMoveSpeed * 2.5f);
        valve03.localPosition = Vector3.Lerp(valve03.localPosition, _valve03Target, Time.deltaTime / valveMoveSpeed * 2.5f);

        PalmTargets[0].localRotation = Quaternion.Lerp(PalmTargets[0].localRotation, Quaternion.Euler(_palmXRot, 0, 0), Time.deltaTime / valveMoveSpeed * 2.5f);

        if (_timer >= valveMoveSpeed)
        {
            int r = Random.Range(0, 2);

            _palmXRot = 0f;

            _valve01Target.y = (r == 0) ? 0.6f : 0.5f;
            _palmXRot += (r == 0) ? 0f : -5f;

            r = Random.Range(0, 2);
            _valve02Target.y = (r == 0) ? 0.6f : 0.5f;
            _palmXRot += (r == 0) ? 0f : -5f;

            r = Random.Range(0, 2);
            _valve03Target.y = (r == 0) ? 0.6f : 0.5f;
            _palmXRot += (r == 0) ? 0f : -5f;

            _timer = 0f;
        }

        transform.localPosition = Random.insideUnitSphere * 0.01f;

        for (int i = 0; i < laserResolution; i++)
        {
            float t = (float)i / (laserResolution - 1);

            if (i == 0) laserBeam.SetPosition(i, laserBeam.transform.position);
            else if (i == laserResolution - 1) laserBeam.SetPosition(i, HitTarget);
            else
            {
                float lerpSpeed = Mathf.Lerp(beamCurveSpeed * 0.25f, beamCurveSpeed, Mathf.Abs(0.5f - t) * 2f);

                Vector3 targetPos = Vector3.Lerp(laserBeam.transform.position, HitTarget, t);
                laserBeam.SetPosition(i, Vector3.Lerp(laserBeam.GetPosition(i), targetPos + Random.insideUnitSphere * 0.1f, Time.deltaTime * lerpSpeed));
            }
        }
    }
    protected override void StopFireWeapon()
    {
        base.StopFireWeapon();

        for (int i = 0; i < laserResolution; i++)
        {
            laserBeam.SetPosition(i, laserBeam.transform.position);
        }
    }
}