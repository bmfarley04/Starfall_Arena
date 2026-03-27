Shader "Custom/LightningBolt3D"
{
    // Point-to-point lightning bolt for world-space 3D geometry.
    // Pair with LightningBolt3D.cs which builds and orients the billboard quad
    // and keeps _BoltLength in sync via MaterialPropertyBlock.
    //
    // Noise math and bolt/branch generation ported from LightningCrackle.shader.
    // UV conventions: uv.x = 0..1 along bolt axis, uv.y = 0..1 across quad width
    //                 (0.5 = bolt center line).
    // All amplitude/radius/thickness properties are in UV units (0..1 = full quad width).

    Properties
    {
        [Header(Lightning Appearance)]
        _BoltColor      ("Bolt Core Color",  Color) = (1, 0.97, 0.9, 1)
        _BoltGlowColor  ("Bolt Glow Color",  Color) = (1, 0.5,  0.08, 1)
        _CoreThickness  ("Core Thickness (UV)",   Range(0.001, 0.05)) = 0.008
        _GlowRadius     ("Glow Radius (UV)",      Range(0.005, 0.5))  = 0.08
        _GlowIntensity  ("Glow Intensity",         Range(0.3,  4.0))  = 1.8
        _CoreIntensity  ("Core Brightness",        Range(1.0,  8.0))  = 3.0

        [Header(Bolt Scaling)]
        _BoltLength ("Bolt World Length (set by script)", Float) = 5.0
        _RefLength  ("Reference Length (world units)",    Float) = 5.0

        [Header(Lightning Shape)]
        _WanderAmp      ("Wander Amplitude (UV)",          Range(0.005, 0.3))  = 0.06
        _WanderFreq     ("Wander Frequency",               Range(1.0,   8.0))  = 2.5
        _ZigzagAmp      ("Zigzag Amplitude (UV)",          Range(0.002, 0.15)) = 0.03
        _ZigzagFreq     ("Zigzag Frequency",               Range(4.0,   25.0)) = 13.0
        _DetailAmp      ("Detail Amplitude (UV)",          Range(0.001, 0.05)) = 0.01
        _DetailFreq     ("Detail Frequency",               Range(10.0,  60.0)) = 30.0
        _MicroAmp       ("Micro Crackle Amplitude (UV)",   Range(0.0005,0.02)) = 0.004
        _MicroFreq      ("Micro Crackle Frequency",        Range(20.0, 120.0)) = 65.0
        _MaxDisplacement("Max Displacement Clamp (UV)",    Range(0.02,  0.45)) = 0.28

        [Header(Animation)]
        _Speed      ("Morph Speed",  Range(0.1, 4.0))  = 1.2
        _ReseedRate ("Reseed Rate",  Range(0.05, 2.0)) = 0.4
        _Flicker    ("Flicker Amount", Range(0.0, 0.5)) = 0.12
        [HideInInspector] _ExternalIntensity ("External Intensity", Range(0.0, 8.0)) = 1.0

        [Header(Branching)]
        _BranchesPerBolt    ("Max Branches Per Bolt",              Range(0,   8))    = 3
        _BranchChance       ("Branch Probability",                 Range(0.0, 1.0))  = 0.55
        _BranchLengthMin    ("Branch Length Min (UV)",             Range(0.01, 0.3)) = 0.05
        _BranchLengthMax    ("Branch Length Max (UV)",             Range(0.01, 0.3)) = 0.15
        _BranchThicknessMin ("Branch Thickness Min (UV)",          Range(0.0005, 0.015)) = 0.002
        _BranchThicknessMax ("Branch Thickness Max (UV)",          Range(0.0005, 0.015)) = 0.007
        _BranchGlowRadius   ("Branch Glow Radius (UV)",            Range(0.003, 0.08))   = 0.022
        _BranchOutwardBias  ("Outward Bias",                       Range(0.3,  1.0))  = 0.8
        _BranchForwardBias  ("Forward Bias (0=perp, 1=parallel)",  Range(0.0,  1.0))  = 0.65
        _BranchAngleSpread  ("Angle Spread",                       Range(0.05, 1.5))  = 0.4
        _SubBranchChance    ("Sub-branch Probability",             Range(0.0,  1.0))  = 0.3
        _SubBranchLengthMin ("Sub-branch Length Min (UV)",         Range(0.005, 0.15)) = 0.02
        _SubBranchLengthMax ("Sub-branch Length Max (UV)",         Range(0.005, 0.15)) = 0.06

        [Header(Cylindrical Depth)]
        _CylinderHighlight  ("Cylinder Highlight Strength",   Range(0.0,  1.0))  = 0.6
        _SpecularOffset     ("Specular Offset (UV)",          Range(-0.03, 0.03)) = 0.004
        _SpecularIntensity  ("Specular Intensity",            Range(0.0,  4.0))  = 2.0

        [Header(End Fade)]
        _EndFadeWidth ("End Fade Width", Range(0.01, 0.25)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "IgnoreProjector"= "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull     Off    // Visible from both sides; billboard can be viewed either way
        Lighting Off
        ZWrite   Off
        ZTest    LEqual
        Blend SrcAlpha One  // Additive: naturally composites glowing energy, no sorting issues

        Pass
        {
            Name "LightningBolt3D"
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            // --- Uniforms ---

            float4 _BoltColor, _BoltGlowColor;
            float  _CoreThickness, _GlowRadius, _GlowIntensity, _CoreIntensity;
            float  _BoltLength, _RefLength;

            float  _WanderAmp, _WanderFreq;
            float  _ZigzagAmp, _ZigzagFreq;
            float  _DetailAmp, _DetailFreq;
            float  _MicroAmp,  _MicroFreq;
            float  _MaxDisplacement;

            float  _Speed, _ReseedRate, _Flicker;
            float  _ExternalIntensity;

            float  _BranchesPerBolt, _BranchChance;
            float  _BranchLengthMin,    _BranchLengthMax;
            float  _BranchThicknessMin, _BranchThicknessMax;
            float  _BranchGlowRadius;
            float  _BranchOutwardBias, _BranchForwardBias, _BranchAngleSpread;
            float  _SubBranchChance, _SubBranchLengthMin, _SubBranchLengthMax;

            float  _CylinderHighlight, _SpecularOffset, _SpecularIntensity;
            float  _EndFadeWidth;

            #define MAX_BRANCHES 8

            // ============================================================
            // Vertex shader — pass UV straight through, no UI transform needed
            // ============================================================

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.texcoord;
                return o;
            }

            // ============================================================
            // Noise — verbatim from LightningCrackle.shader
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

            // ============================================================
            // Fractal displacement — ported to UV space.
            //
            // The _BoltLength / _RefLength ratio (lenScale) stretches or
            // compresses the spatial frequency of the noise so that the
            // visual density of wiggles stays constant across different
            // bolt lengths.  Amplitudes are already in UV units.
            // ============================================================

            float fractalDisplacement(float t, float seed, float time)
            {
                float lenScale = _BoltLength / max(_RefLength, 0.001);

                float t1 = time * _Speed * 0.61  + seed * 41.3;
                float t2 = time * _Speed * 0.87  + seed * 73.1;
                float t3 = time * _Speed * 1.23  + seed * 107.7;
                float t4 = time * _Speed * 1.71  + seed * 151.3;

                float tn = t * lenScale;

                float reseedPhase = time * _ReseedRate;
                float reseedA     = floor(reseedPhase) * 7.13;
                float reseedB     = reseedA + 7.13;
                float rb          = frac(reseedPhase);
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

                return clamp(lerp(valA, valB, rb), -_MaxDisplacement, _MaxDisplacement);
            }

            float branchDisplacement(float t, float seed, float time)
            {
                float t3 = time * _Speed * 1.1 + seed * 83.0;
                float t4 = time * _Speed * 1.5 + seed * 127.0;
                float val = 0.0;
                val += semiSharpNoise(t * _ZigzagFreq * 0.8 + t3) * _ZigzagAmp * 0.5;
                val += steppedNoise(t * _DetailFreq * 0.9 + t4)    * _DetailAmp * 0.4;
                val += steppedNoise(t * _MicroFreq  * 0.7 + time * _Speed * 2.0 + seed * 173.0) * _MicroAmp * 0.3;
                return val;
            }

            // ============================================================
            // Cylindrical depth helper.
            //
            // Models the bolt core as a cylinder.  Returns the cosine of the
            // angle between the pixel's surface normal and the "toward camera"
            // direction.  1.0 at the centre highlight, 0.0 at the silhouette
            // edges — identical to Lambertian N·L for a front-lit cylinder.
            // ============================================================

            float cylinderFacing(float dist, float radius)
            {
                float nd = saturate(dist / max(radius, 0.0001));
                return sqrt(max(0.0, 1.0 - nd * nd));
            }

            // ============================================================
            // Main bolt evaluation (UV space)
            //
            //   uv.x  = 0..1 along bolt axis (start → end)
            //   uv.y  = 0..1 across quad width  (0.5 = centre line)
            //
            // Returns float3(core, glow, specular).
            // ============================================================

            float3 evaluateBolt(float2 uv, float seed, float time)
            {
                float t    = uv.x;
                float perp = uv.y - 0.5;   // centred: -0.5 .. +0.5

                float boltOffset = fractalDisplacement(t, seed, time);
                float dist       = abs(perp - boltOffset);

                // --- Core: thin, bright, hard-edged ---
                float core = saturate(1.0 - dist / _CoreThickness) * _CoreIntensity;

                // --- Glow: wide, soft halo ---
                float coreStrength = saturate(1.0 - dist / (_CoreThickness * 4.0));
                float glow = exp(-dist * dist / (_GlowRadius * _GlowRadius));
                glow *= smoothstep(0.0, 0.15, coreStrength);

                // --- Cylindrical depth shading ---
                // The facing value is 1 at the bolt centre and 0 at the cylinder edge.
                // Applying it to core/glow makes the centre pop relative to the edges,
                // creating the illusion of a tube instead of a flat ribbon.
                float facing = cylinderFacing(dist, _CoreThickness * 2.0);
                float shade  = lerp(1.0, facing, _CylinderHighlight);
                core *= shade;
                glow *= lerp(1.0, 0.55 + 0.45 * facing, _CylinderHighlight);

                // --- Specular highlight ---
                // A very thin, bright line slightly offset from the bolt centre.
                // Simulates a point light glancing across the curved cylinder surface;
                // tweak _SpecularOffset to move it left/right across the bolt width.
                float specDist  = abs(perp - boltOffset - _SpecularOffset);
                float specular  = saturate(1.0 - specDist / (_CoreThickness * 0.5))
                                * _SpecularIntensity
                                * facing;

                // --- End fade: taper bolt in at start and out at end ---
                float endFade = smoothstep(0.0,               _EndFadeWidth, t)
                              * smoothstep(1.0, 1.0 - _EndFadeWidth,         t);
                core     *= endFade;
                glow     *= endFade;
                specular *= endFade;

                return float3(core, glow, specular);
            }

            // ============================================================
            // Branch evaluation (UV space)
            // brOrigin / brDirN / brLen are all in UV-space coordinates.
            // ============================================================

            float2 evaluateBranch(float2 uv, float2 brOrigin, float2 brDirN,
                                   float brLen, float brThickness, float seed, float time)
            {
                float2 brPerpN = float2(-brDirN.y, brDirN.x);
                float2 toPixel = uv - brOrigin;
                float  along   = dot(toPixel, brDirN);
                float  perp    = dot(toPixel, brPerpN);

                // Early out: pixel is clearly outside this branch segment
                float margin = max(brLen * 0.05, 0.003);
                if (along < -margin || along > brLen + margin)
                    return float2(0, 0);

                float t          = along / (brLen + 0.001);
                float boltOffset = branchDisplacement(t, seed, time);
                float dist       = abs(perp - boltOffset);

                float core = saturate(1.0 - dist / brThickness) * _CoreIntensity * 0.55;

                float corePresence = saturate(1.0 - dist / (brThickness * 3.0));
                float glow = exp(-dist * dist / (_BranchGlowRadius * _BranchGlowRadius)) * 0.45;
                glow *= smoothstep(0.0, 0.1, corePresence);

                float taper = smoothstep(0.0, 0.08, t) * smoothstep(1.0, 0.5, t);
                core *= taper;
                glow *= taper;

                return float2(core, glow);
            }

            // ============================================================
            // Fragment shader
            // ============================================================

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv   = i.uv;
                float  time = _Time.y;
                float  seed = 0.0;

                // --- Main bolt ---
                float3 boltResult = evaluateBolt(uv, seed, time);
                float  totalCore  = boltResult.x;
                float  totalGlow  = boltResult.y;
                float  totalSpec  = boltResult.z;

                // Flicker modulates the entire bolt (core + glow + spec together)
                float flicker = 1.0 - _Flicker * (0.5 + 0.5 * sin(time * 13.0 + seed * 29.0));
                flicker *= 0.92 + 0.08 * hash11(floor(time * 5.0) + seed * 11.0);
                totalCore *= flicker;
                totalGlow *= flicker;
                totalSpec *= flicker;

                // --- Branches ---
                float brReseedPhase = time * _ReseedRate;
                float brReseedA     = floor(brReseedPhase) * 7.13 + seed;
                float brReseedB     = brReseedA + 7.13;
                float brBlend       = frac(brReseedPhase);
                brBlend = brBlend * brBlend * (3.0 - 2.0 * brBlend);

                int maxBranches = (int)_BranchesPerBolt;

                for (int br = 0; br < MAX_BRANCHES; br++)
                {
                    if (br >= maxBranches) break;

                    float brSeed         = seed * 71.3 + (float)br * 37.1;
                    float brCoreBlended  = 0.0;
                    float brGlowBlended  = 0.0;

                    for (int phase = 0; phase < 2; phase++)
                    {
                        float phaseSeed   = (phase == 0) ? brReseedA : brReseedB;
                        float phaseWeight = (phase == 0) ? (1.0 - brBlend) : brBlend;

                        if (hash11(phaseSeed + brSeed * 13.7) > _BranchChance) continue;

                        // Origin: a point riding on the displaced main bolt
                        float  brT          = hash11(phaseSeed + brSeed * 51.3);
                        float  mainDispAtT  = fractalDisplacement(brT, seed, time);
                        float2 brOrigin     = float2(brT, 0.5 + mainDispAtT);

                        // Direction: blend between along-bolt and outward
                        float2 edgeDir  = float2(1, 0);
                        float2 outwardN = float2(0, 1);
                        float2 perpComp = (hash11(phaseSeed + brSeed * 61.0) < _BranchOutwardBias) ? outwardN : -outwardN;
                        float2 fwdComp  = (hash11(phaseSeed + brSeed * 53.0) < 0.5) ? edgeDir : -edgeDir;
                        float2 brBase   = normalize(fwdComp * _BranchForwardBias + perpComp * (1.0 - _BranchForwardBias));

                        float  brAngle = (hash11(phaseSeed + brSeed * 91.0) - 0.5) * _BranchAngleSpread;
                        float  cs = cos(brAngle), sn = sin(brAngle);
                        float2 brDirN = normalize(float2(brBase.x * cs - brBase.y * sn,
                                                          brBase.x * sn + brBase.y * cs));

                        float brLen   = lerp(_BranchLengthMin,    _BranchLengthMax,    hash11(phaseSeed + brSeed * 67.0));
                        float brThick = lerp(_BranchThicknessMin, _BranchThicknessMax, hash11(phaseSeed + brSeed * 79.0));

                        float2 brResult = evaluateBranch(uv, brOrigin, brDirN, brLen, brThick, brSeed + phaseSeed, time);
                        float  brCore   = brResult.x * phaseWeight;
                        float  brGlow   = brResult.y * phaseWeight;

                        // Sub-branch
                        if (hash11(phaseSeed + brSeed * 113.0) < _SubBranchChance)
                        {
                            float  subT      = hash11(phaseSeed + brSeed * 143.0) * 0.7;
                            float  subOffset = branchDisplacement(subT, brSeed + phaseSeed, time);
                            float2 brPerpN   = float2(-brDirN.y, brDirN.x);
                            float2 subOrigin = brOrigin + brDirN * brLen * subT + brPerpN * subOffset;

                            float  subAngle = (hash11(phaseSeed + brSeed * 179.0) - 0.5) * _BranchAngleSpread * 1.3;
                            float  scs = cos(subAngle), ssn = sin(subAngle);
                            float2 subDir = normalize(float2(brDirN.x * scs - brDirN.y * ssn,
                                                              brDirN.x * ssn + brDirN.y * scs));

                            float  subLen   = lerp(_SubBranchLengthMin, _SubBranchLengthMax, hash11(phaseSeed + brSeed * 197.0));
                            float  subThick = lerp(_BranchThicknessMin, _BranchThicknessMax, hash11(phaseSeed + brSeed * 209.0)) * 0.7;

                            float2 subResult = evaluateBranch(uv, subOrigin, subDir, subLen, subThick,
                                                               brSeed + phaseSeed + 500.0, time);
                            brCore = max(brCore, subResult.x * 0.6 * phaseWeight);
                            brGlow = max(brGlow, subResult.y * 0.5 * phaseWeight);
                        }

                        brCoreBlended += brCore;
                        brGlowBlended += brGlow;
                    }

                    totalCore = max(totalCore, brCoreBlended * flicker);
                    totalGlow = max(totalGlow, brGlowBlended * flicker);
                }

                // --- Final colour assembly ---
                float3 finalColor = _BoltColor.rgb     * totalCore
                                  + _BoltGlowColor.rgb * totalGlow * _GlowIntensity * 0.35
                                  + float3(1.0, 1.0, 1.0) * totalSpec;   // pure-white specular gleam

                // Alpha drives the SrcAlpha blend factor.
                // Core contributes most (it's the hard visible line),
                // glow/specular contribute subtly.
                float alpha = saturate(totalCore * 0.5 + totalGlow * 0.3 + totalSpec * 0.25);

                finalColor *= _ExternalIntensity;
                alpha = saturate(alpha * _ExternalIntensity);

                // Discard fully dark fragments for fill-rate savings on large quads.
                clip(alpha - 0.001);

                return fixed4(finalColor, alpha);
            }
            ENDCG
        }
    }
}
