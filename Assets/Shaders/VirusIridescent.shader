Shader "Custom/VirusIridescent"
{
    Properties
    {
        _BaseColor         ("Base Color",          Color)        = (0.85, 0.04, 0.04, 1)
        _EmissionColor     ("Emission Color",      Color)        = (1.0,  0.08, 0.08, 1)
        _EmissionIntensity ("Emission Intensity",  Range(0, 6))  = 2.0
        _FresnelPower      ("Fresnel Power",       Range(0.5, 8)) = 2.5
        _ShimmerSpeed      ("Shimmer Speed",       Range(0.1, 10)) = 2.0
        _ShimmerHueRange   ("Shimmer Hue Range",   Range(0, 1.5)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }
        LOD 200

        // ── Forward Lit ───────────────────────────────────────────────
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float  _EmissionIntensity;
                float  _FresnelPower;
                float  _ShimmerSpeed;
                float  _ShimmerHueRange;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Rodrigues rotation in RGB space — rotates hue by `angle` radians
            float3 HueRotate(float3 col, float angle)
            {
                float3 k = float3(0.57735, 0.57735, 0.57735);
                float  c = cos(angle);
                float  s = sin(angle);
                return col * c + cross(k, col) * s + k * dot(k, col) * (1.0 - c);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = normInputs.normalWS;
                OUT.viewDirWS   = GetWorldSpaceViewDir(posInputs.positionWS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // Fresnel — brighter / more shifted at silhouette edges
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                // Time pulses (both active in editor and at runtime)
                float t       = _Time.y * _ShimmerSpeed;
                float breathe = sin(t * 0.4 + 0.1) * 0.5 + 0.5;
                float sparkle = sin(t + IN.positionWS.y * 8.0) * 0.5 + 0.5;

                // Hue rotation: red → orange-red / purple-red at edges and over time
                float hueAngle = (fresnel * 0.7 + breathe * 0.25 + sparkle * 0.05) * _ShimmerHueRange;
                float3 shifted = HueRotate(_EmissionColor.rgb, hueAngle);
                float3 emColor = lerp(_EmissionColor.rgb, shifted, fresnel * 0.9 + breathe * 0.1);

                // Pulse amplitude
                float emPulse = 0.75 + breathe * 0.40 + fresnel * 0.60 + sparkle * 0.10;

                // Pure emission — unaffected by scene lighting or ambient
                float3 finalCol = emColor * _EmissionIntensity * emPulse;
                return float4(finalCol, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow Caster ─────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest  LEqual
            ColorMask 0
            Cull  Back

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
