using System.Collections;
using UnityEngine;

public class TreeManager : Singleton<TreeManager>
{
    [SerializeField] private FoliageGenerator f;
    [SerializeField] private float fallDuration = 0.75f;
    [SerializeField] private float fallAngle = 85f;

    public IEnumerator FellTree(GameObject treeObj, Vector3 direction)
    {
        treeObj.transform.GetChild(0).GetComponent<Collider>().enabled = false;

        Transform tree = treeObj.transform;

        Vector3 planarDir = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        Vector3 fallAxis = Vector3.Cross(Vector3.up, planarDir).normalized;

        Quaternion startRot = tree.rotation;
        Quaternion endRot = Quaternion.AngleAxis(fallAngle, fallAxis) * startRot;

        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            tree.rotation = Quaternion.Slerp(startRot, endRot, Mathf.SmoothStep(0f, 1f, elapsed / fallDuration));
            yield return null;
        }

        tree.rotation = endRot;

        f.TryReturnFoliage(treeObj);
        treeObj.transform.GetChild(0).GetComponent<Collider>().enabled = true;
    }
}
