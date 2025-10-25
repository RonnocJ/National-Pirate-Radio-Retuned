using UnityEngine;
public enum PinColor
{
    Red,
    Green,
    Blue
}
public class Pinslot : Interactable
{
    [SerializeField] private PinColor color;
    public UpgradeWheel AttachedWheel;
    [SerializeField] private Collider interactCol;
    private ConnectorCable cable;
    void Awake()
    {
        Color matColor = Color.black;

        if (color == PinColor.Red) matColor = Color.red;
        else if (color == PinColor.Green) matColor = Color.green;
        else if (color == PinColor.Blue) matColor = Color.blue;

        var matBlock = new MaterialPropertyBlock();
        matBlock.SetColor("_BaseColor", matColor);
        GetComponent<MeshRenderer>().SetPropertyBlock(matBlock);
    }
    void OnTriggerStay(Collider col)
    {
        if (col.CompareTag("Connector") && cable == null)
        {
            cable = col.GetComponent<ConnectorCable>();

            if (!cable.GetComponent<Grabbable>().Grabbed || cable.Color != color)
            {
                cable = null;
                return;
            }

            cable.Active = false;
            MouseMover.root.ForceRelease();

            cable.transform.position = transform.position + (Vector3.up * 0.125f);
            cable.transform.GetChild(0).position = transform.position;
            cable.transform.GetChild(0).rotation = Quaternion.Euler(170, 0, 0);

            AudioManager.root.PlaySound(AudioEvent.playPinboardInsert, gameObject);

            AttachedWheel = cable.AttachedWheel;
            AttachedWheel.UpgradeLevel++;

            interactCol.enabled = true;
            Enabled = true;
        }
    }
    public override void OnClick()
    {
        base.OnClick();

        cable.Rb.isKinematic = false;
        cable.Rb.AddForce((Vector3.up + Vector3.forward) * 5000);
        cable.Active = true;
        cable.transform.GetChild(0).position = cable.transform.position;
        cable.GetComponentInChildren<SphereCollider>().enabled = true;

        AudioManager.root.PlaySound(AudioEvent.playPinboardEject, gameObject);

        AttachedWheel.UpgradeLevel--;

        cable = null;
        AttachedWheel = null;

        interactCol.enabled = false;
        Enabled = false;
    }
}
