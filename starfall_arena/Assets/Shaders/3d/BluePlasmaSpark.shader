Shader "Starfall/3D/BluePlasmaSpark"
{
    Properties
    {
        [HDR]_SparkColor("Spark Core Color", Color) = (7.5, 9.0, 10.0, 1.0)
        [HDR]_GlowColor("Spark Glow Color", Color) = (0.45, 2.0, 7.0, 1.0)
        _GlowRadius("Glow Radius", Range(0.05, 1.0)) = 0.45
        _CoreRadius("Core Radius", Range(0.01, 0.5)) = 0.11
        _Flicker("Flicker", Range(0.0, 1.0)) = 0.38
        [HideInInspector]_ExternalIntensity("External Intensity", Range(0.0, 8.0)) = 1.0
        [HideInInspector]_SparkSeed("Spark Seed", Float) = 0.0
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
            Name "BluePlasmaSpark"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
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
                half4 _SparkColor;
                half4 _GlowColor;
                half _GlowRadius;
                half _CoreRadius;
                half _Flicker;
                half _ExternalIntensity;
                float _SparkSeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float dist = length(centered);
                float core = saturate(1.0 - dist / max(_CoreRadius, 0.001));
                float glow = exp(-dist * dist / max(_GlowRadius * _GlowRadius, 0.001));
                float flicker = 1.0 - _Flicker * (0.5 + 0.5 * sin(_Time.y * 37.0 + _SparkSeed));
                float alpha = saturate(core * 0.7 + glow * 0.35) * _ExternalIntensity * flicker;
                float3 color = _SparkColor.rgb * core * 2.0 + _GlowColor.rgb * glow * 0.45;
                color *= _ExternalIntensity * flicker;
                clip(alpha - 0.001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
