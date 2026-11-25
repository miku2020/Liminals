/*
File: Portal.cs
Desc: Portal
Auth: Semi
*/

using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Portal : MonoBehaviour
{
    [SerializeField] private Portal linked;    
    [SerializeField] private int recursionLimit = 5;
    [SerializeField] private float nearClipOffset = 0.05f;
    [SerializeField] private float nearClipLimit = 0.2f;

    private MeshFilter meshFilter;
    private MeshRenderer mesh;
    private Camera mainCam;
    private Camera portalCam;
    private RenderTexture portalTex;
    private List<PortableObject> travellers = new();

    private void Awake()
    {   
        mainCam = Camera.current;
        portalCam = this.GetComponentInChildren<Camera>(true);
        portalCam.enabled = false;
        mesh = this.GetComponent<MeshRenderer>();
        meshFilter = this.GetComponent<MeshFilter>();
        mesh.material.SetInt("displayMask", 1);
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += PrePortalRender;
        RenderPipelineManager.beginCameraRendering += Render;
        RenderPipelineManager.endCameraRendering += PostPortalRender;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= PrePortalRender;
        RenderPipelineManager.beginCameraRendering -= Render;
        RenderPipelineManager.endCameraRendering -= PostPortalRender;
    }

    private void CreateRenderTex()
    {
        if (portalTex == null || portalTex.width != Screen.width || portalTex.height != Screen.height)
        {
            if (portalTex != null) portalTex.Release();

            portalTex = new(Screen.width, Screen.height, 32, RenderTextureFormat.ARGB32);
            portalCam.targetTexture = portalTex;

            linked.mesh.material.SetTexture("_BaseMap", portalTex);
        }
    }

    // Sets the thickness of the portal screen so as not to clip with camera near plane when player goes through
    private float ProtectScreenFromClipping(Vector3 viewPoint)
    {
        float halfHeight = mainCam.nearClipPlane * Mathf.Tan(mainCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfWidth = halfHeight * mainCam.aspect;
        float dstToNearClipPlaneCorner = new Vector3(halfWidth, halfHeight, mainCam.nearClipPlane).magnitude;
        float screenThickness = dstToNearClipPlaneCorner;

        Transform screenT = mesh.transform;
        bool camFacingSameDirAsPortal = Vector3.Dot(transform.forward, transform.position - viewPoint) > 0;
        screenT.localScale = new Vector3(screenT.localScale.x, screenT.localScale.y, screenThickness);
        screenT.localPosition = Vector3.forward * screenThickness * ((camFacingSameDirAsPortal) ? 0.5f : -0.5f);
        return screenThickness;
    }

    // Use custom projection matrix to align portal camera's near clip plane with the surface of the portal
    private void SetNearClipPlane()
    {
        Transform clipPlane = transform;
        int dot = Math.Sign(Vector3.Dot(clipPlane.forward, transform.position - portalCam.transform.position));

        Vector3 camSpacePos = portalCam.worldToCameraMatrix.MultiplyPoint(clipPlane.position);
        Vector3 camSpaceNormal = portalCam.worldToCameraMatrix.MultiplyVector(clipPlane.forward) * dot;
        float camSpaceDst = -Vector3.Dot(camSpacePos, camSpaceNormal) + nearClipOffset;
        
        if (Math.Abs(camSpaceDst) > nearClipLimit)
        {
            Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);
            portalCam.projectionMatrix = mainCam.CalculateObliqueMatrix(clipPlaneCameraSpace);
        }
        else portalCam.projectionMatrix = mainCam.projectionMatrix;
    }

    private void PrePortalRender(ScriptableRenderContext SRC, Camera camera)
    {
        foreach (var travlr in travellers)
        {
            UpdateSliceParams(travlr);
        }
    }

    private void PostPortalRender(ScriptableRenderContext SRC, Camera camera)
    {
        foreach (var travlr in travellers)
        {
            UpdateSliceParams(travlr);
        }
        ProtectScreenFromClipping(mainCam.transform.position);
    }

    private void Render(ScriptableRenderContext SRC, Camera camera)
    {
        if (!CameraUtility.VisibleFromCamera(linked.mesh, mainCam))
        {
            return;
        }

        CreateRenderTex();

        var localToWorldMatrix = mainCam.transform.localToWorldMatrix;
        var renderPositions = new Vector3[recursionLimit];
        var renderRotations = new Quaternion[recursionLimit];

        int startIndex = 0;
        portalCam.projectionMatrix = mainCam.projectionMatrix;
        for (int i = 0; i < recursionLimit; i++)
        {
            if (i > 0)
            {
                if (!CameraUtility.BoundsOverlap(meshFilter, linked.meshFilter, portalCam)) break;
            }

            localToWorldMatrix = transform.localToWorldMatrix * linked.transform.worldToLocalMatrix * localToWorldMatrix;
            int renderOrderIndex = recursionLimit - i - 1;
            renderPositions[renderOrderIndex] = localToWorldMatrix.GetColumn(3);
            renderRotations[renderOrderIndex] = localToWorldMatrix.rotation;

            portalCam.transform.SetPositionAndRotation(renderPositions[renderOrderIndex], renderRotations[renderOrderIndex]);
            startIndex = renderOrderIndex;
        }

        mesh.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        linked.mesh.material.SetInt("displayMask", 0);

        for (int i = startIndex; i < recursionLimit; i++)
        {
            portalCam.transform.SetPositionAndRotation(renderPositions[i], renderRotations[i]);
            SetNearClipPlane();
            HandleClipping();

            UniversalRenderPipeline.RenderSingleCamera(SRC, portalCam);

            /* Doesn't work properly with recursive portals
            UniversalRenderPipeline.SingleCameraRequest request = new();
            if (RenderPipeline.SupportsRenderRequest(portalCam, request))
            {
                request.destination = portalTex;
                RenderPipeline.SubmitRenderRequest(portalCam, request);
            }
            else Debug.Log(portalCam.name + " doesn't suppost render request");
            */

            if (i == startIndex) linked.mesh.material.SetInt("displayMask", 1); ;            
        }
        mesh.shadowCastingMode = ShadowCastingMode.On;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Object " +  other.name + " enter trigger.");
        var traveller = other.GetComponent<PortableObject>();
        if (traveller) OnPortableEnter(traveller);
    }

    public void OnPortableEnter(PortableObject traveller)
    {
        if (!travellers.Contains(traveller))
        {
            traveller.EnterPortalCollider();
            traveller.previousOffsetFromPortal = traveller.transform.position - transform.position;
            travellers.Add(traveller);
            Debug.Log(traveller.name + " enters portal.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Object " + other.name + " exits trigger.");
        var traveller = other.GetComponent<PortableObject>();
        if (traveller && travellers.Contains(traveller))
        {
            traveller.ExitPortalCollider();
            travellers.Remove(traveller);
            Debug.Log(traveller.name + " exits portal.");
        }
    }

    private void HandleClipping()
    {
        const float hideDst = -1000;
        const float showDst = 1000;
        float portalThickness = linked.ProtectScreenFromClipping(portalCam.transform.position);

        foreach (var traveller in travellers)
        {
            bool sameSidePortalAsCam = linked.SameSideOfPortal(traveller.transform.position, PortalCamPos);
            traveller.SetSliceOffsetDst((sameSidePortalAsCam) ? hideDst : showDst, false);

            int cloneLinkedPortalSide = -PortalSide(traveller.transform.position);
            bool cloneSameSideAsCam = linked.PortalSide(PortalCamPos) == cloneLinkedPortalSide;

            traveller.SetSliceOffsetDst((cloneSameSideAsCam) ? portalThickness : -portalThickness, true);
        }

        var camPortalOffset = PortalCamPos - transform.position;
        foreach (var linkedTravlr in linked.travellers)
        {
            var travlrPos = linkedTravlr.graphicsObj.transform.position;
            var clonePos = linkedTravlr.graphicsClone.transform.position;

            bool cloneSameSideAsCam = linked.PortalSide(travlrPos) != PortalSide(PortalCamPos);
            linkedTravlr.SetSliceOffsetDst((cloneSameSideAsCam) ? hideDst : showDst, true);

            bool trvlrSameSideAsCam = linked.SameSideOfPortal(linkedTravlr.transform.position, PortalCamPos);
            linkedTravlr.SetSliceOffsetDst((trvlrSameSideAsCam) ? portalThickness : -portalThickness, false);
        }
    }

    private void UpdateSliceParams(PortableObject traveller)
    {
        int side = PortalSide(traveller.transform.position);
        Vector3 sliceNormal = transform.forward * -side;
        Vector2 cloneSliceNormal = linked.transform.forward * side;

        Vector3 slicePos = transform.position;
        Vector3 cloneSlicePos = linked.transform.position;

        float sliceOffset = 0f;
        float cloneSliceOffset = 0f;
        float portalThickness = mesh.transform.localScale.z;

        bool mainCamSameSideAsTrvlr = SameSideOfPortal(mainCam.transform.position, traveller.transform.position);
        if (!mainCamSameSideAsTrvlr) sliceOffset = -portalThickness;

        bool mainCamSameSideAsClone = side != linked.PortalSide(mainCam.transform.position);
        if (!mainCamSameSideAsClone) cloneSliceOffset = -portalThickness;

        for (int i = 0; i < traveller.materials.Length; i++)
        {
            traveller.materials[i].SetVector("sliceCentre", slicePos);
            traveller.materials[i].SetVector("sliceNormal", sliceNormal);
            traveller.materials[i].SetFloat("sliceOffset", sliceOffset);

            traveller.cloneMaterials[i].SetVector("sliceCenter", cloneSlicePos);
            traveller.cloneMaterials[i].SetVector("sliceNormal", cloneSliceNormal);
            traveller.cloneMaterials[i].SetFloat("sliceOffset", cloneSliceOffset);
        }
    }

    private void HandleTravellers()
    {
        for (int i = 0; i < travellers.Count; i++)
        {
            PortableObject traveller = travellers[i];
            var m = linked.transform.localToWorldMatrix * transform.worldToLocalMatrix * traveller.transform.localToWorldMatrix;

            Vector3 offsetFromPortal = traveller.transform.position - transform.position;
            int portalSide = Math.Sign(Vector3.Dot(offsetFromPortal, transform.forward));
            int portalSideOld = Math.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));

            if (portalSide != portalSideOld)
            {
                var positionOld = traveller.transform.position;
                var rotationOld = traveller.transform.rotation;
                
                traveller.Teleport(transform, linked.transform, m.GetColumn(3), m.rotation);
                traveller.graphicsClone.transform.SetPositionAndRotation(positionOld, rotationOld);

                linked.OnPortableEnter(traveller);
                travellers.RemoveAt(i);
                i--;
            }
            else
            {
                traveller.graphicsClone.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                traveller.previousOffsetFromPortal = offsetFromPortal;
            }
        }
    }

    private void LateUpdate()
    {
        HandleTravellers();
    }

    private void OnValidate()
    {
        if (linked != null) linked.linked = this;
    }

    private Vector3 PortalCamPos { get { return portalCam.transform.position;  }  }

    private int PortalSide(Vector3 pos)
    {
        return Math.Sign(Vector3.Dot(pos-transform.position, transform.forward));
    }

    private bool SameSideOfPortal(Vector3 pos1, Vector3 pos2)
    {
        return PortalSide(pos1) == PortalSide(pos2);
    }
}
