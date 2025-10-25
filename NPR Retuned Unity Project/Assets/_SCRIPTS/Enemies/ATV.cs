using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public class ATV : EnemyVehicle
{
    [Header("Weapon Settings")]
    [SerializeField] private float spinSpeed;
    [SerializeField] private float fireInterval;
    [SerializeField] private float coolDown;
    [SerializeField] private float impactRadius;
    [SerializeField] private LayerMask mask;
    [SerializeField] private Transform speakerTr;
    [Header("Visual Settings")]
    [SerializeField] private Material staticMat;
    [SerializeField] private VolumeProfile vol;
    [SerializeField] private AnimationCurve staticCurve;
    private Vignette _vignette;
    private float _accel = 1;
    private float _fireTimer;
    private Vector3 _playerPos => VanController.root.transform.position;
    void Start()
    {
        if (vol.TryGet(out Vignette v))
        {
            _vignette = v;
        }
    }
    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (_destroyed) return;

        UpdateBlast();
    }

    private void UpdateBlast()
    {
        if (Vector3.Distance(_playerPos, transform.position) > 160)
        {
            if (_fireTimer > 0 && AudioManager.root.IsPlaying(AudioEvent.playATVAttack, gameObject, 1))
            {
                AudioManager.root.StopSound(AudioEvent.playATVAttack, gameObject, 1);
                _fireTimer = 0;
            }
            return;
        }
        else if (Mathf.Abs(_fireTimer) < 0.01f)
        {
            AudioManager.root.PlaySound(AudioEvent.playATVAttack, gameObject, 1);
        }

        _fireTimer += Time.fixedDeltaTime;
        if (_fireTimer > 0)
        {
            speakerTr.localEulerAngles += Vector3.up * spinSpeed * _accel;
            _accel *= 1.01f;
        }

        if (_fireTimer > fireInterval)
        {
            _fireTimer = -coolDown;
            var cols = Physics.OverlapSphere(transform.position, impactRadius, mask);

            foreach (var c in cols)
            {
                if (c.CompareTag("Player"))
                {
                    if (ATVManager.cr == null) ATVManager.cr = spawner.StartCoroutine(StaticHit());
                    else
                    {
                        spawner.StopCoroutine(ATVManager.cr);
                        ATVManager.cr = spawner.StartCoroutine(StaticHit());
                    }

                    break;
                }
            }

        }
        else _accel = 1;
    }

    private IEnumerator StaticHit()
    {
        AudioManager.root.PlaySound(AudioEvent.playATVBlast, Camera.main.gameObject, 1);

        float elapsed = 0;
        while (elapsed < 3f)
        {
            staticMat.SetFloat("_Intensity", staticCurve.Evaluate(elapsed));
            _vignette.intensity.value = (staticCurve.Evaluate(elapsed) * 0.25f) + 0.25f;
            elapsed += Time.deltaTime;
            yield return null;
        }

        staticMat.SetFloat("_Intensity", 0);
        _vignette.intensity.value = 0.25f;

        ATVManager.cr = null;
    }
    public override void DestroyEnemy(bool killedByPlayer)
    {
        base.DestroyEnemy(killedByPlayer);
        AudioManager.root.StopSound(AudioEvent.playATVAttack, gameObject, 1);
    }
    private void OnDestroy()
    {
        if (staticMat != null) staticMat.SetFloat("_Intensity", 0);
        if (_vignette != null) _vignette.intensity.value = 0.25f;
    }
}