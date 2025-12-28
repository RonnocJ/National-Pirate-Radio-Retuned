using UnityEngine;

public class AntennaBuffBeam : MonoBehaviour
{
    public AntennaTower Source;
    public Enemy ETarget;
    public VanController VTarget;
    [SerializeField] private LayerMask obstacleMask;
    private Transform _origin => transform.parent;
    void Update()
    {
        var target = (ETarget != null) ? ETarget.transform : (VTarget != null) ? VTarget.transform : null;

        if(target == null) return;

        if (Physics.SphereCast(_origin.position, 0.5f, (target.position - _origin.position).normalized, out RaycastHit hit, Vector3.Distance(_origin.position, target.position) + 1f, obstacleMask) && hit.transform == target)
        {
            transform.localScale = new Vector3(1, Vector3.Distance(_origin.position, target.position) / 2f, 1);
            transform.position = (_origin.position + target.position) / 2f;
            transform.up = (target.position - _origin.position).normalized;
        }
        else
        {
            transform.localScale = new Vector3(1, 0.1f, 1);
            transform.position = _origin.position;
        }
    }

    public void BuffEnemy()
    {
        ETarget.Health *= 1.5f;
    }
    public void DebuffEnemy()
    {
        ETarget.Health /= 1.5f;
        ETarget = null;
    }
}
