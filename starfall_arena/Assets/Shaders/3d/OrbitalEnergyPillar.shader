Shader "Starfall/3D/OrbitalEnergyPillar"
{
    Properties
    {
        [HDR]_CoreColor("Core HDR Color", Color) = (7.5, 7.2, 6.8, 1.0)
        [HDR]_EdgeColor("Red Edge HDR Color", Color) = (5.5, 0.16, 0.06, 1.0)
        [HDR]_CrackleColor("Crackle HDR Color", Color) = (13.0, 1.1, 0.7, 1.0)
        _Brightness("Global Brightness", Range(0.0, 20.0)) = 2.8
        _Opacity("Opacity", Range(0.0, 2.0)) = 1.0
        _Reveal("Reveal", Range(0.0, 1.0)) = 1.0
        [HideInInspector]_LayerMode("Layer Mode", Range(0.0, 2.0)) = 1.0
        [HideInInspector]_LayerIntensity("Layer Intensity", Range(0.0, 4.0)) = 1.0

        [Header(Shape)]
        _FresnelPower("Fresnel Power", Range(0.25, 12.0)) = 2.1
        _FresnelStrength("Fresnel Strength", Range(0.0, 8.0)) = 2.4
        _CoreFill("Core Fill", Range(0.0, 1.0)) = 0.72
        _ShellFill("Shell Fill", Range(0.0, 1.0)) = 0.18
        _HaloFill("Halo Fill", Range(0.0, 1.0)) = 0.08

        [Header(Turbulence)]
        _NoiseScale("Noise Scale", Range(0.1, 40.0)) = 8.5
        _NoiseSpeed("Noise Speed", Float) = 1.4
        _NoiseContrast("Noise Contrast", Range(0.25, 6.0)) = 2.6
        _DarkGapThreshold("Dark Gap Threshold", Range(0.0, 1.0)) = 0.52
        _DarkGapSoftness("Dark Gap Softness", Range(0.001, 0.5)) = 0.18
        _DarkGapStrength("Dark Gap Strength", Range(0.0, 1.0)) = 0.72
        _VerticalBandScale("Vertical Band Scale", Range(0.01, 4.0)) = 0.22
        _VerticalBandStrength("Vertical Band Strength", Range(0.0, 1.0)) = 0.22

        [Header(Crackle)]
        _CrackleScale("Crackle Scale", Range(0.1, 120.0)) = 38.0
        _CrackleSpeed("Crackle Speed", Float) = 6.5
        _CrackleThreshold("Crackle Threshold", Range(0.0, 1.0)) = 0.58
        _CrackleSharpness("Crackle Sharpness", Range(0.5, 12.0)) = 4.0
        _CrackleStrength("Crackle Strength", Range(0.0, 6.0)) = 2.4

        [Header(Animation)]
        _PulseSpeed("Pulse Speed", Float) = 2.1
        _PulseStrength("Pulse Strength", Range(0.0, 2.0)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "OrbitalEnergyPillar"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define STARFALL_TWO_PI 6.28318530718

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _EdgeColor;
                half4 _CrackleColor;
                half _Brightness;
                half _Opacity;
                half _Reveal;
                half _LayerMode;
                half _LayerIntensity;
                half _FresnelPower;
                half _FresnelStrength;
                half _CoreFill;
                half _ShellFill;
                half _HaloFill;
                float _NoiseScale;
                float _NoiseSpeed;
                half _NoiseContrast;
                half _DarkGapThreshold;
                half _DarkGapSoftness;
                half _DarkGapStrength;
                float _VerticalBandScale;
                half _VerticalBandStrength;
                float _CrackleScale;
                float _CrackleSpeed;
                half _CrackleThreshold;
                half _CrackleSharpness;
                half _CrackleStrength;
                float _PulseSpeed;
                half _PulseStrength;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2d(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                value += noise2d(p) * amplitude;
                p = p * 2.03 + 17.1;
                amplitude *= 0.5;
                value += noise2d(p) * amplitude;
                p = p * 2.01 + 31.7;
                amplitude *= 0.5;
                value += noise2d(p) * amplitude;
                p = p * 2.11 + 7.3;
                amplitude *= 0.5;
                value += noise2d(p) * amplitude;
                return saturate(value);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDirectionWS)), _FresnelPower) * _FresnelStrength;

                float time = _Time.y;
                float angle01 = frac(atan2(input.positionOS.z, input.positionOS.x) / STARFALL_TWO_PI + 0.5);
                float vertical = input.positionWS.y * _VerticalBandScale;
                float verticalUv = input.uv.y;

                float broad = fbm(float2(angle01 * _NoiseScale + time * 0.07, vertical - time * _NoiseSpeed));
                float broad2 = fbm(float2(angle01 * (_NoiseScale * 0.47) + 9.7, vertical * 1.8 + time * (_NoiseSpeed * 0.62)));
                float turbulent = saturate((broad * 0.7 + broad2 * 0.3) * _NoiseContrast);
                float darkGap = smoothstep(_DarkGapThreshold, _DarkGapThreshold + _DarkGapSoftness, turbulent);
                float darkPreserve = lerp(1.0 - _DarkGapStrength, 1.0, darkGap);

                float fine = fbm(float2(angle01 * _CrackleScale + 13.7, vertical * 2.15 - time * _CrackleSpeed));
                float fineRidge = 1.0 - abs(fine * 2.0 - 1.0);
                float crackleMask = pow(saturate((fineRidge - _CrackleThreshold) / max(0.001, 1.0 - _CrackleThreshold)), _CrackleSharpness);
                crackleMask *= _CrackleStrength * darkGap;

                float bands = 0.5 + 0.5 * sin((vertical * 1.6 + broad * 2.0 - time * _NoiseSpeed) * STARFALL_TWO_PI);
                float bandMask = lerp(1.0, bands, _VerticalBandStrength);
                float endFade = smoothstep(0.0, 0.035, verticalUv) * (1.0 - smoothstep(0.965, 1.0, verticalUv));
                float pulse = 1.0 + sin(time * _PulseSpeed * STARFALL_TWO_PI) * _PulseStrength;

                half layerMode = _LayerMode;
                half reveal = saturate(_Reveal);
                half layerIntensity = max(0.0h, _LayerIntensity);

                half alpha;
                half3 color;

                if (layerMode < 0.5h)
                {
                    float coreTexture = lerp(0.82, 1.0, turbulent) * lerp(0.9, 1.0, crackleMask);
                    alpha = saturate((_CoreFill + 0.22h * fresnel) * _Opacity * reveal * endFade);
                    color = _CoreColor.rgb * coreTexture;
                    color += _CrackleColor.rgb * crackleMask * 0.24h;
                }
                else if (layerMode < 1.5h)
                {
                    float shellTexture = saturate((_ShellFill + fresnel + turbulent * 0.45) * darkPreserve * bandMask);
                    alpha = saturate(shellTexture * _Opacity * reveal * endFade);
                    color = lerp(_EdgeColor.rgb, _CoreColor.rgb, saturate(crackleMask * 0.7 + fresnel * 0.25));
                    color *= lerp(0.32, 1.0, darkPreserve);
                    color += _CrackleColor.rgb * crackleMask;
                }
                else
                {
                    float haloTexture = saturate(_HaloFill + fresnel * 0.72 + turbulent * 0.12);
                    alpha = saturate(haloTexture * _Opacity * reveal * endFade * 0.55h);
                    color = _EdgeColor.rgb * 0.48h + _CoreColor.rgb * 0.12h;
                    color *= lerp(0.45, 1.0, darkPreserve);
                }

                color *= _Brightness * layerIntensity * pulse * reveal;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
