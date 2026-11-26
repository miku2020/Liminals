Shader "Custom/Slice"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] sliceNormal("Slice Normal", Vector) = (0.0, 0.0, 0.0)
        [HideInInspector] sliceCentre("Slice Centre", Vector) = (0.0, 0.0, 0.0)
        [HideInInspector] sliceOffset("Slice Offset", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Geometry" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        LOD 200

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float3 sliceNormal;
                float3 sliceCentre;
                float3 sliceOffset;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionWS = posInputs.positionWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {                
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                float3 adjustedCentre = sliceCentre + sliceNormal * sliceOffset;
                float3 offsetToSliceCentre = adjustedCentre - IN.positionWS;
                clip(dot(offsetToSliceCentre, sliceNormal));
                return color;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
