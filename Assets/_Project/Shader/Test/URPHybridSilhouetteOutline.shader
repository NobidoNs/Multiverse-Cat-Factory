Shader "Custom/URP/HybridSilhouetteOutline"
{
    Properties
    {
        [Header(Color)]
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)

        [Header(Model Space)]
        _ModelWidth("Model Width", Range(0.0001, 0.2)) = 0.02

        [Header(Screen Clamp)]
        _MinPixelWidth("Min Pixel Width", Range(0.0, 16.0)) = 1.0
        _MaxPixelWidth("Max Pixel Width", Range(0.0, 32.0)) = 4.0
        _DepthOffset("Depth Offset", Range(0.0, 0.01)) = 0.001
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "HybridSilhouetteOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _ModelWidth;
                float _MinPixelWidth;
                float _MaxPixelWidth;
                float _DepthOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float4 clipBase = TransformWorldToHClip(positionWS);

                float3 expandedPositionWS = positionWS + normalWS * _ModelWidth;
                float4 clipModelExpanded = TransformWorldToHClip(expandedPositionWS);

                float2 ndcBase = clipBase.xy / max(clipBase.w, 1e-5);
                float2 ndcExpanded = clipModelExpanded.xy / max(clipModelExpanded.w, 1e-5);

                float2 ndcDelta = ndcExpanded - ndcBase;
                float ndcDeltaLength = length(ndcDelta);

                float2 outlineDir = ndcDeltaLength > 1e-5 ? ndcDelta / ndcDeltaLength : float2(1.0, 0.0);

                float2 pixelsPerNdc = _ScreenParams.xy * 0.5;
                float modelPixelWidth = ndcDeltaLength * length(pixelsPerNdc);

                float clampedPixelWidth = clamp(modelPixelWidth, _MinPixelWidth, _MaxPixelWidth);
                float2 ndcPerPixel = 2.0 / _ScreenParams.xy;

                clipBase.xy += outlineDir * (clampedPixelWidth * ndcPerPixel) * clipBase.w;
                clipBase.z -= _DepthOffset * clipBase.w;

                output.positionCS = clipBase;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
