/*
File: PortableObject.cs
Desc: Object teleporting between portals
Auth: Semi
*/
using UnityEngine;
using System.Collections.Generic;

public class PortableObject : MonoBehaviour
{
    public GameObject graphicsObj;
    public GameObject graphicsClone {  get; set; } = null;
    public Vector3 previousOffsetFromPortal { get; set; }

    public Material[] materials {  get; set; }
    public Material[] cloneMaterials { get; set; }

    public virtual void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
    }

    public virtual void EnterPortalCollider() 
    {
        if (graphicsClone == null)
        {
            graphicsClone = Instantiate(graphicsObj);
            graphicsClone.transform.parent = graphicsObj.transform.parent;
            graphicsClone.transform.localScale = graphicsObj.transform.localScale;
            materials = GetMaterials(graphicsObj);
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
