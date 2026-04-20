Shader "Hidden/URP_FullscreenOutline"
{
    Properties
    {
        [Header(Appearance)]
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _Thickness("Thickness", Range(0, 5)) = 1

        [Header(Detection Settings)]
        _DepthThreshold("Depth Threshold", Range(0.005, 1)) = 0.1
        _NormalThreshold("Normal Threshold", Range(0.01, 10)) = 0.4
        _ScreenMargin("Screen Margin", Range(0, 10)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "OutlinePass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Texture declarations for Unity 6 Blit API
            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float4 _OutlineColor;
            float _ColorIntensity;
            float _Thickness;
            float _DepthThreshold;
            float _NormalThreshold;
            float _ScreenMargin;

            // Reconstruct World Space Position from Depth buffer
            float3 GetWorldPos(float2 uv)
            {
                float depth = SampleSceneDepth(uv);
                float2 ndc = uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                    ndc.y *= -1.0;
                #endif
                float4 worldPos = mul(UNITY_MATRIX_I_VP, float4(ndc, depth, 1.0));
                return worldPos.xyz / worldPos.w;
            }

            // Get Depth and Geometric Normal data
            void GetDepthAndGeometricNormal(float2 uv, out float depth, out float3 normal)
            {
                depth = SampleSceneDepth(uv);

                // Calculate the world position of the current pixel
                float3 posWS = GetWorldPos(uv);

                // Use screen-space derivatives (DDX/DDY) to calculate the face normal.
                // This ignores Normal Maps as the data is derived solely from the depth geometry.
                float3 dX = ddx(posWS);
                float3 dY = ddy(posWS);
                normal = normalize(cross(dY, dX));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Generate full-screen triangle
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);

                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float2 texelSize = _ScreenParams.zw - 1.0;

                // Load scene color first (needed for early return)
                float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                // Define a safe margin to avoid sampling screen borders
                float2 margin = texelSize * _ScreenMargin;

                // Early exit for pixels too close to screen borders
                if (uv.x < margin.x || uv.x > 1.0 - margin.x || uv.y < margin.y || uv.y > 1.0 - margin.y)
                {
                    return sceneColor;
                }

                float2 offset = texelSize * _Thickness;

                float depthCenter;
                float3 normalCenter;
                GetDepthAndGeometricNormal(uv, depthCenter, normalCenter);

                float depth[4];
                float3 normal[4];

                // Sample 4 neighbor points for gradient analysis (Cross pattern)
                GetDepthAndGeometricNormal(uv + float2(offset.x, 0),  depth[0], normal[0]);
                GetDepthAndGeometricNormal(uv + float2(-offset.x, 0), depth[1], normal[1]);
                GetDepthAndGeometricNormal(uv + float2(0, offset.y),  depth[2], normal[2]);
                GetDepthAndGeometricNormal(uv + float2(0, -offset.y), depth[3], normal[3]);

                // 1. Depth-based detection (Silhouettes and overlaps)
                float depthEdge = 0;
                for (int i = 0; i < 4; i++)
                {
                    depthEdge += abs(depthCenter - depth[i]);
                }
                depthEdge = step(_DepthThreshold * 0.1, depthEdge);

                // 2. Normal-based detection (Internal mesh angles/edges)
                float normalEdge = 0;
                for (int j = 0; j < 4; j++)
                {
                    float3 normalDiff = normalCenter - normal[j];
                    normalEdge += dot(normalDiff, normalDiff);
                }
                normalEdge = step(_NormalThreshold, normalEdge);

                float edgeMask = saturate(depthEdge + normalEdge);

                // Final output: blend outline with the scene
                return lerp(sceneColor, _OutlineColor, edgeMask * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "None"
}