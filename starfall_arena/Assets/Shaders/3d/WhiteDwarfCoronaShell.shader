Shader "Starfall/3D/WhiteDwarf/CoronaShell"
{
    Properties
    {
        [Header(Color And Brightness)]
        [HDR]_CoronaColor("Corona HDR Color", Color) = (0.75, 2.6, 7.0, 1.0)
        [HDR]_WhiteHotColor("White Hot HDR Color", Color) = (4.6, 4.8, 5.2, 1.0)
        _Brightness("Brightness", Range(0.0, 20.0)) = 2.4
        _Opacity("Opacity", Range(0.0, 2.0)) = 0.72

        [Header(Fresnel Shape)]
        _FresnelPower("Fresnel Power", Range(0.25, 12.0)) = 3.4
        _InnerFill("Inner Fill", Range(0.0, 1.0)) = 0.08
        _OuterFadePower("Outer Fade Power", Range(0.25, 8.0)) = 1.25

        [Header(Shimmer)]
        _NoiseScale("Noise Scale", Range(0.5, 40.0)) = 7.5
        _ShimmerStrength("Shimmer Strength", Range(0.0, 2.0)) = 0.38
        _ShimmerSpeed("Shimmer Speed", Float) = 0.18
        _ExternalPulseIntensity("External Pulse Intensity", Range(0.0, 4.0)) = 1.0
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
            Name "WhiteDwarfCorona"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Front

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
                half4 _CoronaColor;
                half4 _WhiteHotColor;
                half _Brightness;
                half _Opacity;
                half _FresnelPower;
                half _InnerFill;
                half _OuterFadePower;
                float _NoiseScale;
                half _ShimmerStrength;
                float _ShimmerSpeed;
                half _ExternalPulseIntensity;
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
                half viewFacing = saturate(dot(normalWS, viewDirectionWS));
                half fresnel = pow(saturate(1.0h - viewFacing), _FresnelPower);

                float time = _Time.y;
                float shimmerNoise = noise3d(input.directionOS * _NoiseScale + float3(time * _ShimmerSpeed, -time * _ShimmerSpeed * 0.7, time * _ShimmerSpeed * 0.35));
                half shimmer = 1.0h + (shimmerNoise - 0.5h) * _ShimmerStrength * _ExternalPulseIntensity;
                half coronaMask = saturate((fresnel + _InnerFill) * shimmer);
                coronaMask = pow(coronaMask, _OuterFadePower);

                half3 color = lerp(_CoronaColor.rgb, _WhiteHotColor.rgb, saturate(fresnel * 0.65h)) * _Brightness * coronaMask;
                half alpha = saturate(coronaMask * _Opacity);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
