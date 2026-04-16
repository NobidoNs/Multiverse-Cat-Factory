struct ScharrOperators
{
    float3x3 x;
    float3x3 y;
};

ScharrOperators GetEdgeDetectionKernels()
{
    ScharrOperators kernels;
    kernels.x = float3x3(-3, -10, -3,
                          0,   0,  0,
                          3,  10,  3);
    kernels.y = float3x3(-3, 0, 3,
                        -10, 0, 10,
                         -3, 0, 3);
    return kernels;
}

void DepthBaseOutLines_float(float2 screenUV, float2 px, out float outlines)
{
    outlines = 0;
    #if defined(UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED)
        ScharrOperators kernels = GetEdgeDetectionKernels();
        float gx = 0;
        float gy = 0;
        
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) 
                    continue;
                    
                float2 offset = float2(i, j) * px;
                float d = SampleSceneDepth(screenUV + offset);
                gx += d * kernels.x[j + 1][i + 1];
                gy += d * kernels.y[j + 1][i + 1];
            }
        }
        
        float g = sqrt(gx * gx + gy * gy);
        outlines = step(.02, g);
    #endif
}