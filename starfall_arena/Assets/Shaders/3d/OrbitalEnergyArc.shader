Shader "Starfall/3D/OrbitalEnergyArc"
{
    Properties
    {
        [HDR]_BoltColor("Bolt Core Color", Color) = (9.0, 8.6, 7.8, 1.0)
        [HDR]_BoltGlowColor("Bolt Red Glow Color", Color) = (6.0, 0.2, 0.08, 1.0)
        _CoreThickness("Core Thickness", Range(0.001, 0.12)) = 0.018
        _GlowRadius("Glow Radius", Range(0.01, 0.5)) = 0.12
        _CoreIntensity("Core Intensity", Range(0.1, 12.0)) = 4.5
        _GlowIntensity("Glow Intensity", Range(0.1, 12.0)) = 2.6
        _BranchChance("Branch Chance", Range(0.0, 1.0)) = 0.75
        _BranchIntensity("Branch Intensity", Range(0.0, 4.0)) = 1.0
        _Flicker("Flicker", Range(0.0, 0.8)) = 0.22
        _EndFadeWidth("End Fade Width", Range(0.005, 0.3)) = 0.055
        [HideInInspector]_ExternalIntensity("External Intensity", Range(0.0, 8.0)) = 1.0
        [HideInInspector]_BoltLength("Bolt World Length", Float) = 5.0
        [HideInInspector]_ArcSeed("Arc Seed", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "OrbitalEnergyArc"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BoltColor;
                half4 _BoltGlowColor;
                half _CoreThickness;
                half _GlowRadius;
                half _CoreIntensity;
                half _GlowIntensity;
                half _BranchChance;
                half _BranchIntensity;
                half _Flicker;
                half _EndFadeWidth;
                half _ExternalIntensity;
                float _BoltLength;
                float _ArcSeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float hash11(float value)
            {
                value = frac(value * 0.1031);
                value *= value + 33.33;
                value *= value + value;
                return frac(value);
            }

            float signedHash(float value)
            {
                return hash11(value) * 2.0 - 1.0;
            }

            float jaggedCenter(float x, float seed)
            {
                float joints = 7.0;
                float cell = floor(x * joints);
                float t = frac(x * joints);
                float a = signedHash(seed + cell * 17.13) * 0.09;
                float b = signedHash(seed + (cell + 1.0) * 17.13) * 0.09;
                return 0.5 + lerp(a, b, saturate(t));
            }

            float branchMask(float2 uv, float seed, float branchIndex)
            {
                float show = step(hash11(seed + branchIndex * 31.7), _BranchChance);
                float originX = lerp(0.12, 0.88, hash11(seed + branchIndex * 43.1));
                float side = hash11(seed + branchIndex * 47.3) < 0.5 ? -1.0 : 1.0;
                float length = lerp(0.12, 0.26, hash11(seed + branchIndex * 53.9));
                float angle = lerp(0.35, 0.82, hash11(seed + branchIndex * 59.2)) * side;
                float2 origin = float2(originX, jaggedCenter(originX, seed));
                float2 dir = normalize(float2(length, angle * length));
                float2 perp = float2(-dir.y, dir.x);
                float2 toPixel = uv - origin;
                float along = dot(toPixel, dir);
                float across = abs(dot(toPixel, perp));
                float inLength = step(0.0, along) * step(along, length);
                float taper = saturate(1.0 - along / max(length, 0.001));
                float thickness = lerp(0.008, 0.018, hash11(seed + branchIndex * 61.7));
                return show * inLength * saturate(1.0 - across / thickness) * taper * taper;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y + _ArcSeed;
                float seed = floor(time * 13.0) * 19.37 + _ArcSeed * 7.11;
                float center = jaggedCenter(uv.x, seed);
                float dist = abs(uv.y - center);

                float core = saturate(1.0 - dist / max(_CoreThickness, 0.0001)) * _CoreIntensity;
                float glow = exp(-dist * dist / max(_GlowRadius * _GlowRadius, 0.0001)) * _GlowIntensity;
                float branches = max(branchMask(uv, seed, 1.0), branchMask(uv, seed, 2.0)) * _BranchIntensity;

                float endFade = smoothstep(0.0, _EndFadeWidth, uv.x) * (1.0 - smoothstep(1.0 - _EndFadeWidth, 1.0, uv.x));
                float flicker = 1.0 - _Flicker * (0.5 + 0.5 * sin(time * 35.0 + hash11(seed) * 6.2831));
                flicker *= lerp(0.82, 1.12, hash11(floor(time * 23.0) + _ArcSeed));

                core *= endFade * flicker;
                glow *= endFade * flicker;
                branches *= endFade * flicker;

                float3 color = _BoltColor.rgb * (core + branches)
                             + _BoltGlowColor.rgb * (glow * 0.42 + branches * 0.5);
                float alpha = saturate(core * 0.35 + glow * 0.18 + branches * 0.45);
                color *= _ExternalIntensity;
                alpha = saturate(alpha * _ExternalIntensity);

                clip(alpha - 0.001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
