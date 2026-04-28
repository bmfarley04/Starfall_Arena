Shader "Starfall/3D/WhiteDwarf/CoreSurface"
{
    Properties
    {
        [Header(Color And Brightness)]
        [HDR]_CoreColor("Core HDR Color", Color) = (3.8, 3.95, 4.15, 1.0)
        [HDR]_HotCellColor("Hot Cell HDR Color", Color) = (9.0, 8.6, 7.4, 1.0)
        [HDR]_BlueRimColor("Blue Rim HDR Color", Color) = (1.2, 3.8, 8.0, 1.0)
        _Brightness("Brightness", Range(0.0, 20.0)) = 4.5

        [Header(Surface Motion)]
        _NoiseScale("Noise Scale", Range(0.5, 40.0)) = 9.0
        _FlowSpeed("Flow Speed", Float) = 0.12
        _CellSharpness("Cell Sharpness", Range(0.25, 16.0)) = 6.0
        _RotationSpeed("Subtle Rotation Speed", Float) = 0.035

        [Header(Pulse)]
        _PulseStrength("Pulse Strength", Range(0.0, 2.0)) = 0.18
        _PulseSpeed("Pulse Speed", Float) = 0.55
        _ExternalPulseIntensity("External Pulse Intensity", Range(0.0, 4.0)) = 1.0

        [Header(Limb Shape)]
        _LimbDarkening("Limb Darkening", Range(0.0, 1.0)) = 0.28
        _RimPower("Rim Power", Range(0.25, 12.0)) = 3.2
        _RimStrength("Rim Strength", Range(0.0, 8.0)) = 1.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WhiteDwarfCore"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define STARFALL_PI 3.14159265359
            #define STARFALL_TWO_PI 6.28318530718

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
                half4 _CoreColor;
                half4 _HotCellColor;
                half4 _BlueRimColor;
                half _Brightness;

                float _NoiseScale;
                float _FlowSpeed;
                float _CellSharpness;
                float _RotationSpeed;

                half _PulseStrength;
                float _PulseSpeed;
                half _ExternalPulseIntensity;

                half _LimbDarkening;
                half _RimPower;
                half _RimStrength;
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
                p = p * 2.03 + 17.1;
                amplitude *= 0.5;

                value += noise3d(p) * amplitude;
                p = p * 2.01 + 31.7;
                amplitude *= 0.5;

                value += noise3d(p) * amplitude;
                return value;
            }

            float3 RotateAroundY(float3 value, float radians)
            {
                float s = sin(radians);
                float c = cos(radians);
                return float3(
                    value.x * c - value.z * s,
                    value.y,
                    value.x * s + value.z * c
                );
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
                float viewFacing = saturate(dot(normalWS, viewDirectionWS));

                float time = _Time.y;
                float3 surfaceDirection = RotateAroundY(normalize(input.directionOS), time * _RotationSpeed);

                float longitude01 = frac(atan2(surfaceDirection.z, surfaceDirection.x) / STARFALL_TWO_PI + 0.5);
                float latitude01 = asin(clamp(surfaceDirection.y, -1.0, 1.0)) / STARFALL_PI + 0.5;
                float lanePhase = longitude01 + latitude01 * 0.55 - time * _FlowSpeed;

                float broadNoise = fbm3d(surfaceDirection * _NoiseScale + float3(time * _FlowSpeed, 0.0, -time * _FlowSpeed * 0.65));
                float fineNoise = fbm3d(surfaceDirection * _NoiseScale * 2.35 + float3(11.7, time * _FlowSpeed * 1.6, 4.1));
                float laneWave = 0.5 + 0.5 * sin((lanePhase * 9.0 + broadNoise * 2.4) * STARFALL_TWO_PI);

                float plasmaCells = pow(saturate(1.0 - abs(broadNoise - fineNoise) * 1.95), _CellSharpness);
                float hotLanes = pow(saturate(laneWave), max(_CellSharpness * 0.65, 0.25));
                float surfaceHeat = saturate(plasmaCells * 0.85 + hotLanes * 0.45 + fineNoise * 0.25);

                half pulse = 1.0h + sin(time * _PulseSpeed * STARFALL_TWO_PI) * _PulseStrength * _ExternalPulseIntensity;
                half limb = lerp(1.0h - _LimbDarkening, 1.0h, viewFacing);
                half rim = pow(saturate(1.0h - viewFacing), _RimPower) * _RimStrength;

                half3 color = lerp(_CoreColor.rgb, _HotCellColor.rgb, surfaceHeat);
                color *= _Brightness * pulse * limb;
                color += _BlueRimColor.rgb * rim * (0.45h + surfaceHeat * 0.55h);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
