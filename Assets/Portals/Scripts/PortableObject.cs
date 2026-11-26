/*
File: PortableObject.cs
Desc: Object teleporting between portals
Auth: Semi
*/
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PortableObject : MonoBehaviour
{
    public GameObject graphicsClone {  get; set; } = null;
    public Vector3 previousOffsetFromPortal { get; set; }

    public Material[] materials {  get; set; }
    public Material[] cloneMaterials { get; set; }

    private void Start()
    {
    }

    public virtual void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        Vector3 relativeVelocity;

        transform.position = pos;
        transform.rotation = rot;

    }

    public virtual void EnterPortalCollider() 
    {
        if (graphicsClone == null)
        {
            graphicsClone = Instantiate(this.gameObject);
            Destroy(graphicsClone.GetComponent<PortableObject>());
            Destroy(graphicsClone.GetComponent<Rigidbody>());
            Destroy(graphicsClone.GetComponent<Collider>());            
            graphicsClone.transform.parent = this.gameObject.transform.parent;
            graphicsClone.transform.localScale = this.gameObject.transform.localScale;
            materials = GetMaterials(this.gameObject);
            cloneMaterials = GetMaterials(graphicsClone);
        }
        else graphicsClone.SetActive(true);
    }

    public virtual void ExitPortalCollider() 
    {
        graphicsClone.SetActive(false);

        for (int i = 0; i < materials.Length; i++)
        {
            materials[i].SetVector("sliceNormal", Vector3.zero);
        }
    }

    public void SetSliceOffsetDst(float dst, bool clone)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            if (clone) cloneMaterials[i].SetFloat("sliceOffset", dst);
            else materials[i].SetFloat("sliceOffset", dst);
        }
    }

    private Material[] GetMaterials(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        var matList = new List<Material>();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material mat in renderer.materials) matList.Add(mat);
        }

        return matList.ToArray();
    }
}
