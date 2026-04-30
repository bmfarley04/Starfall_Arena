Shader "Starfall/3D/InvasionAbilityWarningSphere"
{
    Properties
    {
        [Header(Color)]
        [HDR]_RimColor("Rim HDR Color", Color) = (6.0, 0.42, 0.04, 1.0)
        [HDR]_CoreColor("Core HDR Color", Color) = (1.85, 0.18, 0.02, 1.0)
        [HDR]_HotColor("White Hot HDR Color", Color) = (9.0, 5.2, 1.15, 1.0)

        [Header(Runtime Control)]
        _Reveal("Reveal", Range(0.0, 1.0)) = 1.0
        _ExternalIntensity("External Intensity", Range(0.0, 8.0)) = 1.0
        _Opacity("Opacity", Range(0.0, 2.0)) = 1.0

        [Header(Ring Shape)]
        _FresnelPower("Fresnel Power", Range(0.25, 12.0)) = 2.25
        _FresnelStrength("Fresnel Strength", Range(0.0, 8.0)) = 3.6
        _RingThickness("Ring Thickness", Range(0.02, 1.0)) = 0.42
        _InnerFill("Inner Fill", Range(0.0, 1.0)) = 0.055

        [Header(Noise)]
        _NoiseScale("Noise Scale", Range(0.5, 60.0)) = 14.0
        _NoiseSpeed("Noise Speed", Float) = 0.55
        _NoiseContrast("Noise Contrast", Range(0.25, 8.0)) = 3.2
        _EdgeBreakup("Edge Breakup", Range(0.0, 1.0)) = 0.48

        [Header(Pulse)]
        _PulseSpeed("Pulse Speed", Float) = 1.6
        _PulseStrength("Pulse Strength", Range(0.0, 2.0)) = 0.18
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
            Name "InvasionAbilityWarningSphere"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 directionOS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half4 _CoreColor;
                half4 _HotColor;
                half _Reveal;
                half _ExternalIntensity;
                half _Opacity;
                half _FresnelPower;
                half _FresnelStrength;
                half _RingThickness;
                half _InnerFill;
                float _NoiseScale;
                float _NoiseSpeed;
                half _NoiseContrast;
                half _EdgeBreakup;
                float _PulseSpeed;
                half _PulseStrength;
            CBUFFER_END

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3d(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i + float3(0.0, 0.0, 0.0));
                float n100 = hash31(i + float3(1.0, 0.0, 0.0));
                float n010 = hash31(i + float3(0.0, 1.0, 0.0));
                float n110 = hash31(i + float3(1.0, 1.0, 0.0));
                float n001 = hash31(i + float3(0.0, 0.0, 1.0));
                float n101 = hash31(i + float3(1.0, 0.0, 1.0));
                float n011 = hash31(i + float3(0.0, 1.0, 1.0));
                float n111 = hash31(i + float3(1.0, 1.0, 1.0));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);
                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);
                return lerp(nxy0, nxy1, u.z);
            }

            float fbm3d(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                value += noise3d(p) * amplitude;
                p = p * 2.03 + float3(17.1, 31.7, 7.3);
                amplitude *= 0.5;
                value += noise3d(p) * amplitude;
                p = p * 2.11 + float3(11.3, 5.7, 23.9);
                amplitude *= 0.5;
                value += noise3d(p) * amplitude;
                p = p * 2.07 + float3(41.2, 13.4, 19.8);
                amplitude *= 0.5;
                value += noise3d(p) * amplitude;
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
                output.directionOS = normalize(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float viewFacing = saturate(abs(dot(normalWS, viewDirectionWS)));

                float fresnelBase = pow(saturate(1.0 - viewFacing), _FresnelPower);
                float ringStart = saturate(1.0 - _RingThickness);
                float ringMask = smoothstep(ringStart, 1.0, fresnelBase);
                float fresnel = saturate(ringMask * _FresnelStrength);

                float time = _Time.y;
                float3 noiseCoord = input.directionOS * _NoiseScale;
                noiseCoord += float3(time * _NoiseSpeed, -time * _NoiseSpeed * 0.73, time * _NoiseSpeed * 0.41);
                float broadNoise = fbm3d(noiseCoord);
                float fineNoise = fbm3d(noiseCoord * 2.37 + float3(8.1, 19.4, -3.7));

                // Keep contrast centered around 0.5. Multiplying then saturating flattens
                // most sphere samples to white, which hides the intended flame breakup.
                float rawNoise = saturate(broadNoise * 0.64 + fineNoise * 0.36);
                float turbulent = saturate((rawNoise - 0.5) * _NoiseContrast + 0.5);
                float ridgeNoise = 1.0 - abs(turbulent * 2.0 - 1.0);

                float breakupThreshold = lerp(0.18, 0.48, 1.0 - _EdgeBreakup);
                float flameBreakup = smoothstep(breakupThreshold, min(1.0, breakupThreshold + 0.32), ridgeNoise);
                float darkPocket = smoothstep(0.18, 0.82, turbulent);
                float breakupMask = lerp(1.0, lerp(0.18, 1.2, flameBreakup), _EdgeBreakup);
                float darkGapMask = lerp(1.0, lerp(0.22, 1.0, darkPocket), _EdgeBreakup);

                float hotMask = pow(saturate(flameBreakup * ringMask), 2.2);
                float innerNoise = lerp(0.2, 1.0, turbulent);
                float shellMask = saturate((fresnel * breakupMask + _InnerFill * innerNoise) * darkGapMask);
                float pulse = 1.0 + sin(time * _PulseSpeed * 6.28318530718) * _PulseStrength;

                half3 color = lerp(_CoreColor.rgb, _RimColor.rgb, saturate(fresnel));
                color = lerp(color, _HotColor.rgb, saturate(hotMask * 1.35));
                color *= lerp(0.45, 1.25, turbulent);
                color *= shellMask * pulse * _ExternalIntensity * _Reveal;

                half alpha = saturate(shellMask * _Opacity * _Reveal);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
