Shader "Starfall/3D/BluePlasmaLightningRibbon"
{
    Properties
    {
        [HDR]_CoreColor("Core HDR Color", Color) = (8.0, 9.2, 10.0, 1.0)
        [HDR]_GlowColor("Blue Glow HDR Color", Color) = (0.55, 2.2, 7.5, 1.0)
        _CoreThickness("Core Thickness", Range(0.001, 0.2)) = 0.045
        _GlowRadius("Glow Radius", Range(0.005, 0.5)) = 0.18
        _CoreIntensity("Core Intensity", Range(0.1, 16.0)) = 6.5
        _GlowIntensity("Glow Intensity", Range(0.1, 16.0)) = 4.0
        _Flicker("Shader Flicker", Range(0.0, 0.8)) = 0.18
        _EndFadeWidth("End Fade Width", Range(0.005, 0.35)) = 0.045
        [HideInInspector]_ExternalIntensity("External Intensity", Range(0.0, 12.0)) = 1.0
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
            Name "BluePlasmaLightningRibbon"
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
                half4 _CoreColor;
                half4 _GlowColor;
                half _CoreThickness;
                half _GlowRadius;
                half _CoreIntensity;
                half _GlowIntensity;
                half _Flicker;
                half _EndFadeWidth;
                half _ExternalIntensity;
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
                float t = input.uv.x;
                float dist = abs(input.uv.y - 0.5);
                float core = saturate(1.0 - dist / max(_CoreThickness, 0.0001)) * _CoreIntensity;
                float glow = exp(-dist * dist / max(_GlowRadius * _GlowRadius, 0.0001)) * _GlowIntensity;
                float endFade = smoothstep(0.0, _EndFadeWidth, t) * (1.0 - smoothstep(1.0 - _EndFadeWidth, 1.0, t));
                float flicker = 1.0 - _Flicker * (0.5 + 0.5 * sin((_Time.y + t * 0.11) * 53.0));

                core *= endFade * flicker;
                glow *= endFade * flicker;
                float3 color = _CoreColor.rgb * core + _GlowColor.rgb * glow * 0.38;
                float alpha = saturate(core * 0.28 + glow * 0.18) * _ExternalIntensity;
                color *= _ExternalIntensity;
                clip(alpha - 0.001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
