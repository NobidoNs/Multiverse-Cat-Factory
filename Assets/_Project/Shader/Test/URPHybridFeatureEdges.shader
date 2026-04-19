Shader "Custom/URP/HybridFeatureEdges"
{
    Properties
    {
        [Header(Color)]
        _EdgeColor("Edge Color", Color) = (0, 0, 0, 1)

        [Header(Screen Space)]
        _EdgeWidth("Edge Width", Range(0.25, 8.0)) = 1.5
        _EdgeSoftness("Edge Softness", Range(0.0, 3.0)) = 1.1
        _DepthOffset("Depth Offset", Range(0.0, 0.01)) = 0.001
        _EdgeOpacity("Edge Opacity", Range(0, 1)) = 1.0
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
            Name "HybridFeatureEdges"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EdgeColor;
                float _EdgeWidth;
                float _EdgeSoftness;
                float _DepthOffset;
                float _EdgeOpacity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 otherOS : TEXCOORD0;
                float2 sideAndEnd : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float edgeCoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float3 otherWS = TransformObjectToWorld(input.otherOS);

                float4 clipPos = TransformWorldToHClip(positionWS);
                float4 clipOther = TransformWorldToHClip(otherWS);

                float2 ndcPos = clipPos.xy / max(clipPos.w, 1e-5);
                float2 ndcOther = clipOther.xy / max(clipOther.w, 1e-5);

                float2 lineDir = ndcOther - ndcPos;
                float lineLength = length(lineDir);
                if (lineLength > 1e-5)
                {
                    lineDir /= lineLength;
                }
                else
                {
                    lineDir = float2(1.0, 0.0);
                }

                float2 lineNormal = float2(-lineDir.y, lineDir.x);
                float2 ndcPerPixel = 2.0 / _ScreenParams.xy;

                clipPos.xy += lineNormal * (input.sideAndEnd.x * _EdgeWidth * ndcPerPixel) * clipPos.w;
                clipPos.z -= _DepthOffset * clipPos.w;

                output.positionCS = clipPos;
                output.edgeCoord = input.sideAndEnd.x;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float edgeMask = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, abs(input.edgeCoord));

                half4 color = _EdgeColor;
                color.a *= edgeMask * _EdgeOpacity;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
