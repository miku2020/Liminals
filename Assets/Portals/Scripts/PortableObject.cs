using UnityEngine;

public class PortableObject : MonoBehaviour
{
    public Vector3 previousOffsetFromPortal { get; set; }

    public virtual void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
    }

    public virtual void EnterPortalCollider() { }

    public virtual void ExitPortalCollider() { }
}
