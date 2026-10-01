Shader "Custom/VAT"
{
    Properties
    {
        _MainTex    ("Albedo Texture", 2D) = "white" {}
        _Color      ("Colour Tint", Color) = (1, 1, 1, 1)
        _PosTex     ("Position Texture", 2D) = "black" {}
        _TexHeight  ("Texture Height (total frames)", Float) = 1
        // Per-instance animation data is supplied via a ComputeBuffer (_VATInstanceData)
        // set on the MaterialPropertyBlock each frame by VATRenderer.
        // float4 layout: (startFrame, frameCount, animTime, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // unity_InstanceID only exists in the INSTANCING_ON variant.
            #if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(UNITY_STEREO_INSTANCING_ENABLED)
                #define VAT_INSTANCE_ID unity_InstanceID
            #else
                #define VAT_INSTANCE_ID 0
            #endif

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_PosTex);
            SAMPLER(sampler_PosTex);

            // Per-instance animation data uploaded by VATRenderer each LateUpdate.
            // Indexed by unity_InstanceID (resets to 0 per draw call / batch).
            // float4: x=startFrame, y=frameCount, z=animTime (0-1), w=unused
            StructuredBuffer<float4> _VATInstanceData;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _TexHeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0        : TEXCOORD0;   // original mesh UVs — albedo lookup
                float2 uv2        : TEXCOORD1;   // UV2.x = normalised vertex index
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float2 uv0         : TEXCOORD1;
                float  fogCoord    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Vertex Shader
            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // Read per-instance animation data from the compute buffer.
                float4 inst      = _VATInstanceData[VAT_INSTANCE_ID];
                float startFrame = inst.x;
                float frameCount = inst.y;
                float animTime   = inst.z;

                float frameF  = startFrame + animTime * max(frameCount - 1.0, 0.0);
                float texU    = IN.uv2.x;

                float frame0  = floor(frameF);
                float frame1  = min(frame0 + 1.0, startFrame + frameCount - 1.0);
                float frac_t  = frameF - frame0;

                float3 pos0 = SAMPLE_TEXTURE2D_LOD(
                    _PosTex, sampler_PosTex,
                    float2(texU, (frame0 + 0.5) / _TexHeight), 0).xyz;
                float3 pos1 = SAMPLE_TEXTURE2D_LOD(
                    _PosTex, sampler_PosTex,
                    float2(texU, (frame1 + 0.5) / _TexHeight), 0).xyz;

                float3 posOS = lerp(pos0, pos1, frac_t);

                float3 posWS    = TransformObjectToWorld(posOS);
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.positionWS  = posWS;
                OUT.uv0         = TRANSFORM_TEX(IN.uv0, _MainTex);
                OUT.fogCoord    = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv0) * _Color;

                float3 normalWS = normalize(cross(ddy(IN.positionWS), ddx(IN.positionWS)));

                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 ambient = SampleSH(normalWS);
                float3 lighting = mainLight.color * NdotL + ambient;

                uint additionalLightCount = GetAdditionalLightsCount();
                for (uint i = 0; i < additionalLightCount; i++)
                {
                    Light light = GetAdditionalLight(i, IN.positionWS);
                    lighting += light.color * light.distanceAttenuation * saturate(dot(normalWS, light.direction));
                }

                float3 col = albedo.rgb * lighting;
                col = MixFog(col, IN.fogCoord);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster - uses VAT position so shadows match geometry
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // unity_InstanceID only exists in the INSTANCING_ON variant.
            #if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(UNITY_STEREO_INSTANCING_ENABLED)
                #define VAT_INSTANCE_ID unity_InstanceID
            #else
                #define VAT_INSTANCE_ID 0
            #endif

            TEXTURE2D(_PosTex);
            SAMPLER(sampler_PosTex);

            StructuredBuffer<float4> _VATInstanceData;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _TexHeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv2        : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vertShadow(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float4 inst      = _VATInstanceData[VAT_INSTANCE_ID];
                float startFrame = inst.x;
                float frameCount = inst.y;
                float animTime   = inst.z;

                float frameF  = startFrame + animTime * max(frameCount - 1.0, 0.0);
                float texU    = IN.uv2.x;

                float frame0  = floor(frameF);
                float frame1  = min(frame0 + 1.0, startFrame + frameCount - 1.0);
                float frac_t  = frameF - frame0;

                float3 pos0 = SAMPLE_TEXTURE2D_LOD(
                    _PosTex, sampler_PosTex,
                    float2(texU, (frame0 + 0.5) / _TexHeight), 0).xyz;
                float3 pos1 = SAMPLE_TEXTURE2D_LOD(
                    _PosTex, sampler_PosTex,
                    float2(texU, (frame1 + 0.5) / _TexHeight), 0).xyz;

                float3 posOS = lerp(pos0, pos1, frac_t);
                OUT.positionCS = TransformObjectToHClip(posOS);
                return OUT;
            }

            half4 fragShadow(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
