Shader "UI/LightningBorder2"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Lightning Bolts)]
        _BoltColor ("Bolt Core Color", Color) = (1, 0.95, 0.85, 1)
        _BoltGlowColor ("Bolt Glow Color", Color) = (1, 0.55, 0.1, 1)
        _EdgeInset ("Border Position (UV inset)", Range(0.01, 0.15)) = 0.055
        _BoltsPerEdge ("Bolts Per Edge", Range(1, 8)) = 4
        _BoltSegments ("Segments Per Bolt", Range(4, 20)) = 12
        _BoltAmplitude ("Zigzag Amplitude", Range(0.01, 0.12)) = 0.045
        _BoltThickness ("Core Thickness", Range(0.001, 0.015)) = 0.004
        _BoltGlowRadius ("Glow Radius", Range(0.005, 0.08)) = 0.03
        _BoltGlowIntensity ("Glow Intensity", Range(0.5, 5.0)) = 2.5
        _BoltSpeed ("Reshape Speed", Range(0.5, 8.0)) = 2.5
        _Flicker ("Flicker Amount", Range(0.0, 0.6)) = 0.25

        [Header(Branching)]
        _BranchChance ("Branch Probability", Range(0.0, 1.0)) = 0.5
        _BranchLength ("Branch Length", Range(0.02, 0.12)) = 0.05
        _BranchThickness ("Branch Thickness", Range(0.001, 0.01)) = 0.003
        _BranchGlowRadius ("Branch Glow Radius", Range(0.003, 0.05)) = 0.018

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

            float4 _BoltColor;
            float4 _BoltGlowColor;
            float _EdgeInset;
            float _BoltsPerEdge;
            float _BoltSegments;
            float _BoltAmplitude;
            float _BoltThickness;
            float _BoltGlowRadius;
            float _BoltGlowIntensity;
            float _BoltSpeed;
            float _Flicker;

            float _BranchChance;
            float _BranchLength;
            float _BranchThickness;
            float _BranchGlowRadius;

            #define MAX_BOLTS_PER_EDGE 8
            #define MAX_SEGS 20

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

            // Stepped hash - gives sharp, non-interpolated random values
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // Distance from point to line segment
            float distToSegment(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float2 ap = p - a;
                float t = saturate(dot(ap, ab) / (dot(ab, ab) + 0.00001));
                return length(p - (a + t * ab));
            }

            // Trace a zigzag bolt along an edge and return min distance to any segment.
            // startUV, endUV: the two corners of the edge the bolt runs along
            // perpDir: direction perpendicular to the edge (for zigzag displacement)
            // seed: unique random seed
            // timeSeed: time-varying seed for reshaping
            // numSegs: number of zigzag segments
            // amplitude: zigzag amplitude
            float traceBoltAlongEdge(float2 pixelUV, float2 startUV, float2 endUV,
                                     float2 perpDir, float seed, float timeSeed, int numSegs, float amplitude)
            {
                float minDist = 1000.0;
                float2 prev = startUV;

                for (int s = 0; s < MAX_SEGS; s++)
                {
                    if (s >= numSegs) break;

                    float frac_s = (float)(s + 1) / (float)numSegs;
                    float2 basePos = lerp(startUV, endUV, frac_s);

                    // Sharp zigzag: stepped hash gives hard angle changes
                    float offset = (hash11(timeSeed + seed * 31.71 + (float)s * 17.13) - 0.5) * 2.0 * amplitude;

                    float2 pos = basePos + perpDir * offset;
                    minDist = min(minDist, distToSegment(pixelUV, prev, pos));
                    prev = pos;
                }
                return minDist;
            }

            // Trace a short branch bolt extending outward from a point
            float traceBranch(float2 pixelUV, float2 origin, float2 dir, float seed, float timeSeed)
            {
                float minDist = 1000.0;
                float2 prev = origin;
                float2 perp = float2(-dir.y, dir.x);

                int branchSegs = 4;
                float segLen = _BranchLength / (float)branchSegs;

                for (int s = 0; s < 4; s++)
                {
                    float lateral = (hash11(timeSeed + seed * 47.3 + (float)s * 23.7) - 0.5) * _BoltAmplitude * 0.8;
                    float2 next = prev + dir * segLen + perp * lateral;
                    minDist = min(minDist, distToSegment(pixelUV, prev, next));
                    prev = next;
                }
                return minDist;
            }

            // Process one edge: traces all bolts along it plus branches
            // Returns float2(coreIntensity, glowIntensity)
            float2 processEdge(float2 pixelUV, float2 corner1, float2 corner2, float2 perpDir,
                               float edgeSeed, float time)
            {
                float totalCore = 0.0;
                float totalGlow = 0.0;

                int boltCount = (int)_BoltsPerEdge;
                int numSegs = (int)_BoltSegments;

                float2 edgeDir = normalize(corner2 - corner1);
                float edgeLen = length(corner2 - corner1);

                for (int b = 0; b < MAX_BOLTS_PER_EDGE; b++)
                {
                    if (b >= boltCount) break;

                    float seed = edgeSeed * 100.0 + (float)b;

                    // Time seed: changes at _BoltSpeed rate, each bolt on its own schedule
                    float timeSeed = floor(time * _BoltSpeed + seed * 7.3) * 13.7 + seed;

                    // Each bolt covers a portion of the edge with some overlap
                    float boltSpan = 1.0 / (float)boltCount;
                    float boltStart = (float)b * boltSpan - boltSpan * 0.15;
                    float boltEnd = boltStart + boltSpan + boltSpan * 0.3;
                    boltStart = saturate(boltStart);
                    boltEnd = saturate(boltEnd);

                    float2 startPt = lerp(corner1, corner2, boltStart);
                    float2 endPt = lerp(corner1, corner2, boltEnd);

                    // Trace the main bolt
                    float d = traceBoltAlongEdge(pixelUV, startPt, endPt, perpDir,
                                                  seed, timeSeed, numSegs, _BoltAmplitude);

                    float core = smoothstep(_BoltThickness, _BoltThickness * 0.15, d);
                    float glow = exp(-d / _BoltGlowRadius);

                    // Flicker
                    float flicker = 1.0 - _Flicker * (0.5 + 0.5 * sin(time * 12.0 + seed * 29.0));
                    flicker *= (0.85 + 0.15 * hash11(floor(time * 15.0) + seed * 11.0));

                    core *= flicker;
                    glow *= flicker;

                    // Branches: fork off the main bolt at random segments
                    for (int br = 0; br < 3; br++)
                    {
                        float brSeed = seed * 71.3 + (float)br * 37.1;
                        float brChance = hash11(timeSeed + brSeed * 13.7);
                        if (brChance > _BranchChance) continue;

                        // Pick a segment along the bolt to branch from
                        float brFrac = hash11(timeSeed + brSeed * 51.3);
                        float2 branchPt = lerp(startPt, endPt, brFrac);
                        // Offset to match the main bolt's zigzag at that point
                        int brSeg = (int)(brFrac * (float)numSegs);
                        float brOffset = (hash11(timeSeed + seed * 31.71 + (float)brSeg * 17.13) - 0.5) * 2.0 * _BoltAmplitude;
                        branchPt += perpDir * brOffset;

                        // Branch direction: mostly outward (perpendicular) with some along-edge
                        float brAngle = (hash11(timeSeed + brSeed * 91.0) - 0.5) * 1.2;
                        float2 brDir = perpDir * sign(brOffset + 0.001);
                        // Rotate slightly
                        float cs = cos(brAngle);
                        float sn = sin(brAngle);
                        brDir = float2(brDir.x * cs - brDir.y * sn, brDir.x * sn + brDir.y * cs);

                        float brDist = traceBranch(pixelUV, branchPt, brDir, brSeed, timeSeed);
                        float brCore = smoothstep(_BranchThickness, _BranchThickness * 0.1, brDist) * 0.7;
                        float brGlow = exp(-brDist / _BranchGlowRadius) * 0.5;

                        core = max(core, brCore * flicker);
                        glow = max(glow, brGlow * flicker);
                    }

                    totalCore = max(totalCore, core);
                    totalGlow = max(totalGlow, glow);
                }

                return float2(totalCore, totalGlow);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float time = _Time.y;

                // ======= SAMPLE ORIGINAL TEXTURE =======
                fixed4 texColor = (tex2D(_MainTex, uv) + _TextureSampleAdd) * i.color;

                // ======= LIGHTNING ON ALL 4 EDGES =======
                // Edge positions based on _EdgeInset (where the border sits in UV space)
                float inset = _EdgeInset;

                // Corner positions of the border rectangle
                float2 TL = float2(inset, 1.0 - inset);      // top-left
                float2 TR = float2(1.0 - inset, 1.0 - inset); // top-right
                float2 BR = float2(1.0 - inset, inset);        // bottom-right
                float2 BL = float2(inset, inset);              // bottom-left

                float totalCore = 0.0;
                float totalGlow = 0.0;

                // Top edge (left to right, perpendicular = up/down = Y)
                float2 topResult = processEdge(uv, TL, TR, float2(0, 1), 1.0, time);
                totalCore = max(totalCore, topResult.x);
                totalGlow = max(totalGlow, topResult.y);

                // Bottom edge
                float2 botResult = processEdge(uv, BL, BR, float2(0, -1), 2.0, time);
                totalCore = max(totalCore, botResult.x);
                totalGlow = max(totalGlow, botResult.y);

                // Left edge (bottom to top, perpendicular = left/right = X)
                float2 leftResult = processEdge(uv, BL, TL, float2(-1, 0), 3.0, time);
                totalCore = max(totalCore, leftResult.x);
                totalGlow = max(totalGlow, leftResult.y);

                // Right edge
                float2 rightResult = processEdge(uv, BR, TR, float2(1, 0), 4.0, time);
                totalCore = max(totalCore, rightResult.x);
                totalGlow = max(totalGlow, rightResult.y);

                // ======= COMPOSITE =======
                // Lightning color: bright core + colored glow
                float3 lightning = _BoltColor.rgb * totalCore
                                 + _BoltGlowColor.rgb * totalGlow * _BoltGlowIntensity * 0.3;
                float lightningAlpha = saturate(totalCore + totalGlow * 0.4);

                // Add lightning on top of original texture (additive over the base)
                float3 finalColor = texColor.rgb + lightning;
                float finalAlpha = saturate(texColor.a + lightningAlpha);

                // Premultiplied alpha for UI blending
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
