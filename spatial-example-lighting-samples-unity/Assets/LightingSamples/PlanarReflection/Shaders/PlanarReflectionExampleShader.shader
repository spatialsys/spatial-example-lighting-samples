Shader "PlanarReflectionExampleShader"
{
    Properties
    {
        [Header(Surface)]
        _BaseColor ("Base color", Color) = (1, 1, 1, 1)
        _BaseMap ("Texture", 2D) = "white" { }
        [Normal] _BumpMap ("Normal", 2D) = "bump" { }
        _BumpMapScale ("Normal Scale", Float) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0

        [Header(Reflection)]
        _ReflectionFrenelPow ("Frenel Power", Float) = 3
        _ReflectionFresnelOffset ("Fresnel Offset", Range(0, 1)) = 0
        _ReflectionNormalStrength ("Normal Strength", Float) = 0.01
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "./PlanarReflection.hlsl" // Make sure the path is correct

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varying
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0; // xy: albedo, zw: normal
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 tangentWS : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                float fogCoord : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                half4 _BaseColor;
                float _BumpMapScale;
                float _Metallic;
                
                float _ReflectionFrenelPow;
                float _ReflectionFresnelOffset;
                float _ReflectionNormalStrength;
            CBUFFER_END

            Varying vert(Attributes IN, uint vertexID : SV_VertexID)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varying OUT = (Varying)0;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vertexInput.positionCS;
                OUT.positionWS = vertexInput.positionWS;
                OUT.screenPos = vertexInput.positionNDC;

                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normal.xyz, IN.tangent);
                OUT.normalWS = normalInput.normalWS;
                OUT.tangentWS = normalInput.tangentWS;
                OUT.bitangentWS = normalInput.bitangentWS;

                OUT.uv.xy = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.uv.zw = TRANSFORM_TEX(IN.uv, _BumpMap);

                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);

                return OUT;
            }

            half4 frag(Varying IN) : SV_Target
            {
                float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv.zw), _BumpMapScale);
                float3 normalWS = normalTS.x * IN.tangentWS + normalTS.y * IN.bitangentWS + normalTS.z * IN.normalWS;
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                // Fresnel
                half NV = dot(viewDirWS, normalWS);
                half fresnel = saturate(1 - NV);
                fresnel = saturate(pow(fresnel, _ReflectionFrenelPow) + _ReflectionFresnelOffset);

                float2 screenPos = IN.screenPos.xy / IN.screenPos.w;
                float3 planarReflection;
                PLANAR_REFLECTION_OUTPUT(screenPos, normalWS, _ReflectionNormalStrength, planarReflection);
                planarReflection = lerp(planarReflection * fresnel * saturate(normalWS.y), planarReflection, _Metallic);

                // Lighting
                float NL = saturate(dot(normalWS, _MainLightPosition.xyz));
                half3 ambient = SampleSH(normalWS);
                half diffuse = NL * _MainLightColor.rgb;
                half shadow = MainLightRealtimeShadow(IN.positionCS);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                float3 lighting = diffuse * shadow + ambient;

                half4 color = half4(albedo.rgb * lighting, albedo.a);
                color.rgb += planarReflection;

                color.rgb = MixFog(color.rgb, IN.fogCoord);
                return color;
            }
            ENDHLSL
        }
    }
}
