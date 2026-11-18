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

    private void Awake()
    {   
        mainCam = Camera.current;
        portalCam = this.GetComponentInChildren<Camera>(true);
        portalCam.enabled = false;
        mesh = this.GetComponent<MeshRenderer>();
        
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

    private static bool VisibleFromCamera(Renderer renderer, Camera camera)
    {
        Plane[] fustrumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
        return GeometryUtility.TestPlanesAABB(fustrumPlanes, renderer.bounds);
    }

    private void SetNearClipPlane()
    {
        Transform clipPlane = transform;
        int dot = Math.Sign(Vector3.Dot(clipPlane.forward, transform.position - portalCam.transform.position));

        Vector3 camSpacePos = portalCam.worldToCameraMatrix.MultiplyPoint(clipPlane.position);
        Vector3 camSpaceNormal = portalCam.worldToCameraMatrix.MultiplyVector(clipPlane.forward) * dot;
        float camSpaceDst = -Vector3.Dot(camSpacePos, camSpaceNormal);
        Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);

        portalCam.projectionMatrix = mainCam.CalculateObliqueMatrix(clipPlaneCameraSpace);
    }

    private void Render(ScriptableRenderContext SRC, Camera camera)
    {
        if (!VisibleFromCamera(linked.mesh, camera)) return;
        mesh.enabled = false;
        CreateRenderTex();

        var m = transform.localToWorldMatrix * linked.transform.worldToLocalMatrix * mainCam.transform.localToWorldMatrix;
        portalCam.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);


        SetNearClipPlane();
        UniversalRenderPipeline.SingleCameraRequest request = new();

        if (RenderPipeline.SupportsRenderRequest(portalCam, request))
        {
            request.destination = portalTex; 
            RenderPipeline.SubmitRenderRequest(portalCam, request);
        }
        mesh.enabled = true;
    }
}
