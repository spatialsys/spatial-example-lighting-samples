#ifndef PLANAR_REFLECTION_INCLUDED
#define PLANAR_REFLECTION_INCLUDED

TEXTURE2D(_PlanarReflectionTexture);
SAMPLER(sampler_PlanarReflectionTexture);
#if defined(_PLANAR_REFLECTION_VR)
    TEXTURE2D(_PlanarReflectionTextureRight);
    SAMPLER(sampler_PlanarReflectionTextureRight);
#endif

float4 _PlanarReflectionTextureRect = float4(0, 0, 1, 1); // Use this if camera rect is not (0,0,1,1)

#define PLANAR_REFLECTION_OUTPUT(screenUV, worldNormal, normalDistort, color) PlanarReflection(screenUV, worldNormal, normalDistort, color);

void PlanarReflection(float2 screenUV, float3 worldNormal, float normalDistort, out float3 color)
{
    // Adjust screenUV by camera's rect size
    screenUV *= _PlanarReflectionTextureRect.zw;
    screenUV += (1 - _PlanarReflectionTextureRect.zw) * 0.5;
    float2 reflectionUV = screenUV + worldNormal.zx * normalDistort;
    color = SAMPLE_TEXTURE2D(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, reflectionUV).rgb;
    #if defined(_PLANAR_REFLECTION_VR) // VR Stereo
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        half3 skyColorRight = SAMPLE_TEXTURE2D(_PlanarReflectionTextureRight, sampler_PlanarReflectionTextureRight, reflectionUV).rgb;
        color = lerp(skyColor, skyColorRight, unity_StereoEyeIndex);
    #endif
}

// For ShaderGraph
void PlanarReflection_float(float2 screenUV, float3 worldNormal, float normalDistort, out float3 color)
{
    PlanarReflection(screenUV, worldNormal, normalDistort, color);
}
void PlanarReflection_half(float2 screenUV, float3 worldNormal, float normalDistort, out float3 color)
{
    PlanarReflection(screenUV, worldNormal, normalDistort, color);
}

#endif
