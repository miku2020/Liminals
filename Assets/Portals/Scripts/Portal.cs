using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Portal : MonoBehaviour
{
    [SerializeField] private Portal linked;
    private MeshRenderer mesh;
    private Camera mainCam;
    private Camera portalCam;
    private RenderTexture portalTex;
    [SerializeField] private int recursionLimit = 5;

    [SerializeField] private float nearClipOffset = 0.05f;
    [SerializeField] private float nearClipLimit = 0.2f;
    private MeshFilter meshFilter;

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
        RenderPipelineManager.beginCameraRendering += Render;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Render;
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

    private void Render(ScriptableRenderContext SRC, Camera camera)
    {
        if (!CameraUtility.VisibleFromCamera(linked.mesh, mainCam))
        {
            linked.mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
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


            UniversalRenderPipeline.SingleCameraRequest request = new();
            if (RenderPipeline.SupportsRenderRequest(portalCam, request))
            {
                request.destination = portalTex;
                RenderPipeline.SubmitRenderRequest(portalCam, request);
            }
            else Debug.Log(portalCam.name + " doesn't suppost render request");

            if (i == startIndex) linked.mesh.material.SetInt("displayMask", 1);
        }
        mesh.shadowCastingMode = ShadowCastingMode.On;
    }

    private void OnValidate()
    {
        if (linked != null) linked.linked = this;
    }
}
