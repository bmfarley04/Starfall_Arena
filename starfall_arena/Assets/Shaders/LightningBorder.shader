Shader "UI/LightningBorder"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Border Shape)]
        _BorderWidth ("Border Width", Range(0.005, 0.1)) = 0.03
        _CornerRadius ("Corner Radius", Range(0.0, 0.5)) = 0.06
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.02)) = 0.005

        [Header(Lightning)]
        _LightningColor ("Lightning Core Color", Color) = (1, 0.95, 0.8, 1)
        _GlowColor ("Glow Color", Color) = (1, 0.6, 0.1, 1)
        _ArcCount ("Arc Layers", Range(1, 5)) = 3
        _ArcAmplitude ("Arc Amplitude", Range(0.005, 0.08)) = 0.025
        _ArcFrequency ("Arc Frequency", Range(5, 60)) = 20
        _ArcSpeed ("Arc Speed", Range(0.1, 10)) = 3.0
        _ArcThickness ("Arc Thickness", Range(0.001, 0.02)) = 0.005
        _GlowRadius ("Glow Radius", Range(0.005, 0.08)) = 0.025
        _GlowIntensity ("Glow Intensity", Range(0.5, 5.0)) = 2.0
        _Flicker ("Flicker Intensity", Range(0.0, 1.0)) = 0.3

        [Header(Border Glow)]
        _BorderGlow ("Border Glow Intensity", Range(0.0, 3.0)) = 1.0
        _BorderGlowWidth ("Border Glow Width", Range(0.01, 0.15)) = 0.06

        // Unity UI stencil/masking support
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _BorderWidth;
            float _CornerRadius;
            float _EdgeSoftness;

            float4 _LightningColor;
            float4 _GlowColor;
            float _ArcCount;
            float _ArcAmplitude;
            float _ArcFrequency;
            float _ArcSpeed;
            float _ArcThickness;
            float _GlowRadius;
            float _GlowIntensity;
            float _Flicker;

            float _BorderGlow;
            float _BorderGlowWidth;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            // Hash functions for pseudo-random noise
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Value noise for smooth lightning displacement
            float valueNoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f); // smoothstep
                return lerp(hash11(i), hash11(i + 1.0), u) * 2.0 - 1.0;
            }

            // Fractal noise - layered for jagged lightning look
            float fractalNoise(float x, int octaves)
            {
                float value = 0.0;
                float amplitude = 1.0;
                float frequency = 1.0;
                float maxVal = 0.0;

                for (int i = 0; i < octaves; i++)
                {
                    value += valueNoise(x * frequency) * amplitude;
                    maxVal += amplitude;
                    amplitude *= 0.5;
                    frequency *= 2.17;
                }
                return value / maxVal;
            }

            // Rounded rectangle SDF (signed distance field)
            // Returns negative inside, positive outside
            float roundedRectSDF(float2 p, float2 halfSize, float radius)
            {
                float2 d = abs(p) - halfSize + radius;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - radius;
            }

            // Get the closest point parameter along the border perimeter (0-1)
            // This maps each pixel to a "position along the edge" for noise sampling
            float getEdgeParameter(float2 uv, float2 halfSize, float radius)
            {
                float2 p = uv - 0.5; // center

                // Compute angle-based parameter for smooth traversal
                float angle = atan2(p.y, p.x);
                // Normalize to 0-1
                float t = (angle + 3.14159265) / (2.0 * 3.14159265);

                // Add perimeter-length weighting based on rectangle shape
                // This prevents lightning from bunching at corners
                float2 dir = normalize(p + 0.0001);
                float2 absDir = abs(dir);
                float scale = max(absDir.x / halfSize.x, absDir.y / halfSize.y);
                t += scale * 0.1;

                return t;
            }

            // Compute a single lightning arc contribution
            float lightningArc(float edgeParam, float distFromEdge, float seed, float time)
            {
                // Noise-based displacement perpendicular to edge
                float noiseInput = edgeParam * _ArcFrequency + time * _ArcSpeed + seed * 137.0;
                float displacement = fractalNoise(noiseInput, 4) * _ArcAmplitude;

                // Add sharp jagged detail
                float detail = fractalNoise(noiseInput * 3.7 + seed * 91.0, 3) * _ArcAmplitude * 0.3;
                displacement += detail;

                // Distance from the arc path
                float arcDist = abs(distFromEdge - displacement);

                // Sharp core
                float core = smoothstep(_ArcThickness, 0.0, arcDist);

                // Soft glow
                float glow = smoothstep(_GlowRadius, 0.0, arcDist) * 0.5;

                // Flicker based on noise
                float flicker = 1.0 - _Flicker * (0.5 + 0.5 * valueNoise(time * 7.0 + seed * 53.0));

                return (core + glow) * flicker;
            }

            // Branch arc - smaller arcs that fork off the main one
            float branchArc(float edgeParam, float distFromEdge, float seed, float time)
            {
                float noiseInput = edgeParam * _ArcFrequency * 0.7 + time * _ArcSpeed * 1.3 + seed * 200.0;

                // Branch trigger - only appear intermittently
                float branchTrigger = smoothstep(0.6, 0.8, abs(fractalNoise(noiseInput * 0.3, 2)));

                // Branch extends outward from the border
                float branchDisplacement = fractalNoise(noiseInput * 2.0, 3) * _ArcAmplitude * 1.5;
                float arcDist = abs(distFromEdge - branchDisplacement);

                float core = smoothstep(_ArcThickness * 0.7, 0.0, arcDist) * 0.6;
                float glow = smoothstep(_GlowRadius * 0.6, 0.0, arcDist) * 0.3;

                float flicker = 1.0 - _Flicker * (0.5 + 0.5 * valueNoise(time * 11.0 + seed * 77.0));

                return (core + glow) * branchTrigger * flicker;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float time = _Time.y;

                // Aspect ratio correction
                // For a UI Image, UVs go 0-1 in both axes regardless of rect size
                // We work in UV space directly

                float2 center = uv - 0.5;
                float2 halfSize = float2(0.5, 0.5);

                // Signed distance to rounded rectangle border
                float dist = roundedRectSDF(center, halfSize - _BorderWidth * 0.5, _CornerRadius);

                // Distance from the border edge (0 at the border line)
                float borderDist = abs(dist) - _BorderWidth * 0.5;

                // Signed distance specifically (negative = inside border band)
                float distFromBorderCenter = dist; // 0 at the border center line

                // === Border base glow ===
                float borderBase = smoothstep(_BorderGlowWidth, 0.0, abs(dist));
                float3 borderColor = _GlowColor.rgb * borderBase * _BorderGlow;
                float borderAlpha = borderBase * _BorderGlow * 0.4;

                // === Lightning arcs ===
                float edgeParam = getEdgeParameter(uv, halfSize, _CornerRadius);

                float totalLightning = 0.0;
                float totalGlow = 0.0;

                int arcCount = (int)_ArcCount;
                for (int a = 0; a < arcCount; a++)
                {
                    float seed = (float)a;

                    // Main arc - follows the border
                    float arc = lightningArc(edgeParam, dist, seed, time);
                    totalLightning += arc;

                    // Branch arcs - fork outward
                    float branch = branchArc(edgeParam, dist * 0.8, seed + 0.5, time);
                    totalLightning += branch * 0.5;
                }

                totalLightning = saturate(totalLightning);

                // === Composite ===
                // Lightning core: white-yellow
                float3 lightningCore = _LightningColor.rgb * totalLightning;
                // Lightning glow: orange/gold
                float3 lightningGlow = _GlowColor.rgb * totalLightning * _GlowIntensity * 0.5;

                float3 finalColor = borderColor + lightningCore + lightningGlow;
                float finalAlpha = saturate(borderAlpha + totalLightning);

                // Apply vertex color and tint
                finalColor *= i.color.rgb;
                finalAlpha *= i.color.a;

                // Premultiplied alpha (Unity UI blending)
                finalColor *= finalAlpha;

                fixed4 col = fixed4(finalColor, finalAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
