Shader "UI/LightningCrackle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Lightning Appearance)]
        _BoltColor ("Bolt Core Color", Color) = (1, 0.97, 0.9, 1)
        _BoltGlowColor ("Bolt Glow Color", Color) = (1, 0.5, 0.08, 1)
        _CoreThickness ("Core Thickness (px)", Range(0.3, 4.0)) = 1.2
        _GlowRadius ("Glow Radius (px)", Range(3.0, 50.0)) = 22.0
        _GlowIntensity ("Glow Intensity", Range(0.3, 4.0)) = 1.8
        _CoreIntensity ("Core Brightness", Range(1.0, 5.0)) = 3.0

        [Header(Rect Size  Auto set by LightningRectSize script)]
        _RectWidth ("Rect Width (px)", Float) = 400
        _RectHeight ("Rect Height (px)", Float) = 600

        [Header(Edge Positioning)]
        _EdgeLeft ("Edge Left (UV)", Range(0.0, 0.4)) = 0.12
        _EdgeRight ("Edge Right (UV)", Range(0.6, 1.0)) = 0.88
        _EdgeBottom ("Edge Bottom (UV)", Range(0.0, 0.4)) = 0.08
        _EdgeTop ("Edge Top (UV)", Range(0.6, 1.0)) = 0.92

        [Header(Lightning Shape)]
        _BoltsPerEdge ("Bolts Per Edge", Range(1, 5)) = 3
        _RefEdgeLength ("Reference Edge Length (px)", Range(200, 1200)) = 600.0
        _WanderAmp ("Wander Amplitude (px)", Range(2.0, 40.0)) = 15.0
        _WanderFreq ("Wander Frequency", Range(1.0, 5.0)) = 2.5
        _ZigzagAmp ("Zigzag Amplitude (px)", Range(1.0, 20.0)) = 7.0
        _ZigzagFreq ("Zigzag Frequency", Range(6.0, 25.0)) = 13.0
        _DetailAmp ("Detail Amplitude (px)", Range(0.3, 8.0)) = 3.0
        _DetailFreq ("Detail Frequency", Range(15.0, 50.0)) = 30.0
        _MicroAmp ("Micro Crackle Amplitude (px)", Range(0.1, 4.0)) = 1.5
        _MicroFreq ("Micro Crackle Frequency", Range(30.0, 120.0)) = 65.0
        _MaxDisplacement ("Max Displacement Clamp (px)", Range(10.0, 80.0)) = 35.0

        [Header(Animation)]
        _Speed ("Morph Speed", Range(0.1, 4.0)) = 1.2
        _ReseedRate ("Reseed Rate", Range(0.05, 2.0)) = 0.4
        _Flicker ("Flicker Amount", Range(0.0, 0.4)) = 0.12

        [Header(Branching)]
        _BranchesPerBolt ("Max Branches Per Bolt", Range(0, 8)) = 4
        _BranchChance ("Branch Probability", Range(0.0, 1.0)) = 0.55
        _BranchLength ("Branch Length (px)", Range(8.0, 80.0)) = 35.0
        _BranchThickness ("Branch Core Thickness (px)", Range(0.2, 3.0)) = 0.8
        _BranchGlowRadius ("Branch Glow Radius (px)", Range(2.0, 35.0)) = 14.0
        _BranchOutwardBias ("Outward Bias", Range(0.3, 1.0)) = 0.8
        _BranchAngleSpread ("Angle Spread", Range(0.2, 1.2)) = 0.6
        _SubBranchChance ("Sub-branch Probability", Range(0.0, 1.0)) = 0.3
        _SubBranchLength ("Sub-branch Length (px)", Range(4.0, 35.0)) = 15.0

        [Header(Corner Overlap)]
        _EdgeExtend ("Edge Extension Past Corners", Range(0.0, 0.1)) = 0.03
        _EndFadeWidth ("End Fade Width", Range(0.01, 0.1)) = 0.04

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
        Blend SrcAlpha One
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

            float4 _BoltColor, _BoltGlowColor;
            float _CoreThickness, _GlowRadius, _GlowIntensity, _CoreIntensity;

            float _RectWidth, _RectHeight;

            float _EdgeLeft, _EdgeRight, _EdgeBottom, _EdgeTop;

            float _BoltsPerEdge, _RefEdgeLength;
            float _WanderAmp, _WanderFreq;
            float _ZigzagAmp, _ZigzagFreq;
            float _DetailAmp, _DetailFreq;
            float _MicroAmp, _MicroFreq;
            float _MaxDisplacement;

            float _Speed, _ReseedRate, _Flicker;

            float _BranchesPerBolt, _BranchChance, _BranchLength;
            float _BranchThickness, _BranchGlowRadius;
            float _BranchOutwardBias, _BranchAngleSpread;
            float _SubBranchChance, _SubBranchLength;

            float _EdgeExtend, _EndFadeWidth;

            #define MAX_BOLTS 5
            #define MAX_BRANCHES 8

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

            // ============================================================
            // Noise
            // ============================================================

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float smoothNoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                return lerp(hash11(i), hash11(i + 1.0), u) * 2.0 - 1.0;
            }

            float steppedNoise(float x)
            {
                return hash11(floor(x)) * 2.0 - 1.0;
            }

            float semiSharpNoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * f;
                return lerp(hash11(i), hash11(i + 1.0), u) * 2.0 - 1.0;
            }

            // UV to pixel using explicit rect dimensions
            float2 uvToPixel(float2 uv)
            {
                return uv * float2(_RectWidth, _RectHeight);
            }

            // ============================================================
            // Fractal displacement (edge-length normalized, clamped)
            // ============================================================

            float fractalDisplacement(float t, float seed, float time, float edgeLenScale)
            {
                float t1 = time * _Speed * 0.61 + seed * 41.3;
                float t2 = time * _Speed * 0.87 + seed * 73.1;
                float t3 = time * _Speed * 1.23 + seed * 107.7;
                float t4 = time * _Speed * 1.71 + seed * 151.3;

                float tn = t * edgeLenScale;

                float reseedPhase = time * _ReseedRate;
                float reseedA = floor(reseedPhase) * 7.13;
                float reseedB = reseedA + 7.13;
                float rb = frac(reseedPhase);
                rb = rb * rb * (3.0 - 2.0 * rb);

                float valA = 0.0, valB = 0.0;

                valA += smoothNoise(tn * _WanderFreq + t1 + reseedA) * _WanderAmp;
                valB += smoothNoise(tn * _WanderFreq + t1 + reseedB) * _WanderAmp;

                valA += semiSharpNoise(tn * _ZigzagFreq + t2 + reseedA) * _ZigzagAmp;
                valB += semiSharpNoise(tn * _ZigzagFreq + t2 + reseedB) * _ZigzagAmp;

                valA += steppedNoise(tn * _DetailFreq + t3 + reseedA) * _DetailAmp;
                valB += steppedNoise(tn * _DetailFreq + t3 + reseedB) * _DetailAmp;

                valA += steppedNoise(tn * _MicroFreq + t4 + reseedA) * _MicroAmp;
                valB += steppedNoise(tn * _MicroFreq + t4 + reseedB) * _MicroAmp;

                float val = lerp(valA, valB, rb);
                return clamp(val, -_MaxDisplacement, _MaxDisplacement);
            }

            float branchDisplacement(float t, float seed, float time)
            {
                float t3 = time * _Speed * 1.1 + seed * 83.0;
                float t4 = time * _Speed * 1.5 + seed * 127.0;
                float val = 0.0;
                val += semiSharpNoise(t * _ZigzagFreq * 0.8 + t3) * _ZigzagAmp * 0.5;
                val += steppedNoise(t * _DetailFreq * 0.9 + t4) * _DetailAmp * 0.4;
                val += steppedNoise(t * _MicroFreq * 0.7 + time * _Speed * 2.0 + seed * 173.0) * _MicroAmp * 0.3;
                return val;
            }

            // ============================================================
            // Per-pixel bolt evaluation
            // ============================================================

            float2 evaluateBolt(float2 pixelPos, float2 pxStart, float2 pxEnd,
                                float2 edgeDir, float2 outwardDir, float edgePixelLen,
                                float seed, float time)
            {
                float2 toPixel = pixelPos - pxStart;
                float along = dot(toPixel, edgeDir);
                float perp = dot(toPixel, outwardDir);

                float t = along / (edgePixelLen + 0.001);

                float edgeLenScale = edgePixelLen / _RefEdgeLength;
                float boltOffset = fractalDisplacement(t, seed, time, edgeLenScale);
                float dist = abs(perp - boltOffset);

                float core = saturate(1.0 - dist / _CoreThickness) * _CoreIntensity;

                float coreStrength = saturate(1.0 - dist / (_CoreThickness * 4.0));
                float glow = exp(-dist * dist / (_GlowRadius * _GlowRadius));
                glow *= smoothstep(0.0, 0.15, coreStrength);

                float endFade = smoothstep(-_EdgeExtend, _EndFadeWidth, t)
                              * smoothstep(1.0 + _EdgeExtend, 1.0 - _EndFadeWidth, t);
                core *= endFade;
                glow *= endFade;

                return float2(core, glow);
            }

            float2 evaluateBranch(float2 pixelPos, float2 brOriginPx, float2 brDirN,
                                   float brLenPx, float seed, float time)
            {
                float2 brPerpN = float2(-brDirN.y, brDirN.x);
                float2 toPixel = pixelPos - brOriginPx;
                float along = dot(toPixel, brDirN);
                float perp = dot(toPixel, brPerpN);

                if (along < -2.0 || along > brLenPx + 2.0)
                    return float2(0, 0);

                float t = along / (brLenPx + 0.001);
                float boltOffset = branchDisplacement(t, seed, time);
                float dist = abs(perp - boltOffset);

                float core = saturate(1.0 - dist / _BranchThickness) * _CoreIntensity * 0.55;

                float corePresence = saturate(1.0 - dist / (_BranchThickness * 3.0));
                float glow = exp(-dist * dist / (_BranchGlowRadius * _BranchGlowRadius)) * 0.45;
                glow *= smoothstep(0.0, 0.1, corePresence);

                float taper = smoothstep(0.0, 0.08, t) * smoothstep(1.0, 0.5, t);
                core *= taper;
                glow *= taper;

                return float2(core, glow);
            }

            // ============================================================
            // Process one edge
            // ============================================================

            float2 processEdge(float2 pixelPos, float2 pxStart, float2 pxEnd,
                               float2 outwardN, float edgeSeed, float time)
            {
                float2 edgeVec = pxEnd - pxStart;
                float edgePixelLen = length(edgeVec);
                float2 edgeDir = edgeVec / (edgePixelLen + 0.001);

                float2 extStart = pxStart - edgeDir * (_EdgeExtend * edgePixelLen);
                float2 extEnd = pxEnd + edgeDir * (_EdgeExtend * edgePixelLen);
                float extLen = edgePixelLen * (1.0 + 2.0 * _EdgeExtend);

                float totalCore = 0.0;
                float totalGlow = 0.0;

                int boltCount = (int)_BoltsPerEdge;

                for (int b = 0; b < MAX_BOLTS; b++)
                {
                    if (b >= boltCount) break;

                    float seed = edgeSeed * 100.0 + (float)b * 17.31;

                    float2 bolt = evaluateBolt(pixelPos, extStart, extEnd, edgeDir, outwardN,
                                               extLen, seed, time);
                    float core = bolt.x;
                    float glow = bolt.y;

                    float flicker = 1.0 - _Flicker * (0.5 + 0.5 * sin(time * 13.0 + seed * 29.0));
                    flicker *= (0.92 + 0.08 * hash11(floor(time * 5.0) + seed * 11.0));
                    core *= flicker;
                    glow *= flicker;

                    int maxBranches = (int)_BranchesPerBolt;
                    float branchTimeSeed = floor(time * _ReseedRate) * 7.13 + seed;
                    float edgeLenScale = edgePixelLen / _RefEdgeLength;

                    for (int br = 0; br < MAX_BRANCHES; br++)
                    {
                        if (br >= maxBranches) break;

                        float brSeed = seed * 71.3 + (float)br * 37.1;
                        float brChance = hash11(branchTimeSeed + brSeed * 13.7);
                        if (brChance > _BranchChance) continue;

                        float brT = hash11(branchTimeSeed + brSeed * 51.3);
                        float2 brOriginBase = lerp(extStart, extEnd, brT);
                        float mainDisp = fractalDisplacement(brT, seed, time, edgeLenScale);
                        float2 brOriginPx = brOriginBase + outwardN * mainDisp;

                        float outwardRoll = hash11(branchTimeSeed + brSeed * 61.0);
                        float2 brBaseDir;
                        if (outwardRoll < _BranchOutwardBias)
                            brBaseDir = outwardN;
                        else
                            brBaseDir = -outwardN;

                        float brAngle = (hash11(branchTimeSeed + brSeed * 91.0) - 0.5) * _BranchAngleSpread;
                        float cs = cos(brAngle);
                        float sn = sin(brAngle);
                        float2 brDirN = normalize(float2(brBaseDir.x * cs - brBaseDir.y * sn,
                                                          brBaseDir.x * sn + brBaseDir.y * cs));

                        float brLen = _BranchLength * (0.3 + 0.7 * hash11(branchTimeSeed + brSeed * 67.0));

                        float2 brResult = evaluateBranch(pixelPos, brOriginPx, brDirN, brLen, brSeed, time);
                        core = max(core, brResult.x * flicker);
                        glow = max(glow, brResult.y * flicker);

                        float subChance = hash11(branchTimeSeed + brSeed * 113.0);
                        if (subChance < _SubBranchChance)
                        {
                            float subT = hash11(branchTimeSeed + brSeed * 143.0) * 0.7;
                            float2 subOrigin = brOriginPx + brDirN * brLen * subT;
                            float subOffset = branchDisplacement(subT, brSeed, time);
                            float2 subPerp = float2(-brDirN.y, brDirN.x);
                            subOrigin += subPerp * subOffset;

                            float subAngle = (hash11(branchTimeSeed + brSeed * 179.0) - 0.5) * _BranchAngleSpread * 1.3;
                            float2 subDir = brDirN;
                            float scs = cos(subAngle);
                            float ssn = sin(subAngle);
                            subDir = normalize(float2(subDir.x * scs - subDir.y * ssn,
                                                       subDir.x * ssn + subDir.y * scs));

                            float subLen = _SubBranchLength * (0.3 + 0.7 * hash11(branchTimeSeed + brSeed * 197.0));

                            float2 subResult = evaluateBranch(pixelPos, subOrigin, subDir, subLen,
                                                               brSeed + 500.0, time);
                            core = max(core, subResult.x * flicker * 0.6);
                            glow = max(glow, subResult.y * flicker * 0.5);
                        }
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

                // Convert UV to pixel space using actual rect dimensions
                float2 pixelPos = uvToPixel(uv);

                float2 pxTL = uvToPixel(float2(_EdgeLeft, _EdgeTop));
                float2 pxTR = uvToPixel(float2(_EdgeRight, _EdgeTop));
                float2 pxBR = uvToPixel(float2(_EdgeRight, _EdgeBottom));
                float2 pxBL = uvToPixel(float2(_EdgeLeft, _EdgeBottom));

                float boltCore = 0.0;
                float boltGlow = 0.0;
                float2 r;

                // Top edge: outward = up (+Y)
                r = processEdge(pixelPos, pxTL, pxTR, float2(0, 1), 1.0, time);
                boltCore = max(boltCore, r.x);
                boltGlow = max(boltGlow, r.y);

                // Bottom edge: outward = down (-Y)
                r = processEdge(pixelPos, pxBL, pxBR, float2(0, -1), 2.0, time);
                boltCore = max(boltCore, r.x);
                boltGlow = max(boltGlow, r.y);

                // Left edge: outward = left (-X)
                r = processEdge(pixelPos, pxBL, pxTL, float2(-1, 0), 3.0, time);
                boltCore = max(boltCore, r.x);
                boltGlow = max(boltGlow, r.y);

                // Right edge: outward = right (+X)
                r = processEdge(pixelPos, pxBR, pxTR, float2(1, 0), 4.0, time);
                boltCore = max(boltCore, r.x);
                boltGlow = max(boltGlow, r.y);

                float3 finalColor = _BoltColor.rgb * boltCore
                                  + _BoltGlowColor.rgb * boltGlow * _GlowIntensity * 0.35;
                float finalAlpha = saturate(boltCore * 0.5 + boltGlow * 0.3);

                finalColor *= i.color.rgb;
                finalAlpha *= i.color.a;

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
