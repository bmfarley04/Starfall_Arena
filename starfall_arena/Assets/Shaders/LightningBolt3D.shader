Shader "Custom/LightningBolt3D"
{
    // Point-to-point lightning bolt for world-space 3D geometry.
    // Pair with LightningBolt3D.cs which builds and orients the billboard quad
    // and keeps _BoltLength in sync via MaterialPropertyBlock.
    //
    // The visual model here intentionally favors a dominant segmented strike:
    // a small number of joints, short angular breaks, and only tiny crackle on
    // top. The old "stack multiple full-length noise octaves" approach created
    // a spaghetti/ribbon silhouette because every layer distorted the whole trunk.

    Properties
    {
        [Header(Lightning Appearance)]
        _BoltColor      ("Bolt Core Color",  Color) = (1, 0.97, 0.9, 1)
        _BoltGlowColor  ("Bolt Glow Color",  Color) = (1, 0.5,  0.08, 1)
        _CoreThickness  ("Core Thickness (UV)",   Range(0.001, 0.05)) = 0.010
        _GlowRadius     ("Glow Radius (UV)",      Range(0.005, 0.5))  = 0.08
        _GlowIntensity  ("Glow Intensity",        Range(0.3,  4.0))   = 1.8
        _CoreIntensity  ("Core Brightness",       Range(1.0,  8.0))   = 3.0

        [Header(Bolt Scaling)]
        _BoltLength ("Bolt World Length (set by script)", Float) = 5.0
        _RefLength  ("Reference Length (world units)",    Float) = 5.0

        [Header(Main Trunk)]
        _MainKinkCount           ("Main Kink Count",            Range(1, 6))     = 4
        _MainOffsetMin           ("Main Offset Min (UV)",       Range(0.0, 0.08)) = 0.01
        _MainOffsetMax           ("Main Offset Max (UV)",       Range(0.005, 0.1)) = 0.035
        _MainJitterAmp           ("Main Jitter Amp (UV)",       Range(0.0, 0.01)) = 0.002
        _MainReseedRate          ("Main Reseed Rate",           Range(0.1, 4.0)) = 0.8
        _MainTransitionSharpness ("Main Transition Sharpness",  Range(0.5, 8.0)) = 3.5

        [Header(Animation)]
        _Flicker    ("Flicker Amount", Range(0.0, 0.6)) = 0.16
        [HideInInspector] _ExternalIntensity ("External Intensity", Range(0.0, 8.0)) = 1.0

        [Header(Branches)]
        _MaxBranches       ("Max Branches",              Range(0, 2))    = 1
        _BranchSpawnChance ("Branch Spawn Chance",       Range(0.0, 1.0)) = 0.45
        _BranchLengthMin   ("Branch Length Min (UV)",    Range(0.05, 0.4)) = 0.12
        _BranchLengthMax   ("Branch Length Max (UV)",    Range(0.05, 0.4)) = 0.28
        _BranchKinkCount   ("Branch Kink Count",         Range(0, 3))    = 1
        _BranchOutwardBias ("Branch Outward Bias",       Range(0.0, 1.0)) = 0.85
        _BranchAngleMin    ("Branch Angle Min (deg)",    Range(0.0, 80.0)) = 18.0
        _BranchAngleMax    ("Branch Angle Max (deg)",    Range(0.0, 85.0)) = 40.0
        _BranchThicknessMin ("Branch Thickness Min (UV)", Range(0.0005, 0.02)) = 0.002
        _BranchThicknessMax ("Branch Thickness Max (UV)", Range(0.0005, 0.02)) = 0.006
        _BranchGlowRadius   ("Branch Glow Radius (UV)",   Range(0.003, 0.08)) = 0.020

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

        Cull     Off
        Lighting Off
        ZWrite   Off
        ZTest    LEqual
        Blend SrcAlpha One

        Pass
        {
            Name "LightningBolt3D"
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"

            #define MAX_MAIN_KINKS 6
            #define MAX_BRANCHES 2
            #define MAX_BRANCH_KINKS 3

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

            float4 _BoltColor, _BoltGlowColor;
            float  _CoreThickness, _GlowRadius, _GlowIntensity, _CoreIntensity;
            float  _BoltLength, _RefLength;

            float  _MainKinkCount;
            float  _MainOffsetMin, _MainOffsetMax;
            float  _MainJitterAmp, _MainReseedRate, _MainTransitionSharpness;
            float  _Flicker, _ExternalIntensity;

            float  _MaxBranches, _BranchSpawnChance;
            float  _BranchLengthMin, _BranchLengthMax;
            float  _BranchKinkCount, _BranchOutwardBias;
            float  _BranchAngleMin, _BranchAngleMax;
            float  _BranchThicknessMin, _BranchThicknessMax, _BranchGlowRadius;

            float  _CylinderHighlight, _SpecularOffset, _SpecularIntensity;
            float  _EndFadeWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.texcoord;
                return o;
            }

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float smoothHash11(float p)
            {
                float i = floor(p);
                float f = frac(p);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(hash11(i), hash11(i + 1.0), u);
            }

            float symmetricHash(float p)
            {
                return hash11(p) * 2.0 - 1.0;
            }

            float steppedNoise(float x)
            {
                return hash11(floor(x)) * 2.0 - 1.0;
            }

            float safeNormalize2(float2 v, float2 fallback)
            {
                float lenSq = dot(v, v);
                if (lenSq <= 0.000001)
                {
                    return 0.0;
                }

                return rsqrt(lenSq);
            }

            float2 normalizeOrFallback(float2 v, float2 fallback)
            {
                float lenSq = dot(v, v);
                if (lenSq <= 0.000001)
                {
                    return fallback;
                }

                return v * rsqrt(lenSq);
            }

            float2 rotate2(float2 v, float radians)
            {
                float s = sin(radians);
                float c = cos(radians);
                return float2(v.x * c - v.y * s, v.x * s + v.y * c);
            }

            void GetMainControlPoint(int pointIndex, int kinkCount, float seed, out float x, out float y)
            {
                if (pointIndex <= 0)
                {
                    x = 0.0;
                    y = 0.0;
                    return;
                }

                if (pointIndex >= kinkCount + 1)
                {
                    x = 1.0;
                    y = 0.0;
                    return;
                }

                float prevX = 0.0;
                float prevY = 0.0;
                float minSpacing = 0.11;

                for (int j = 1; j <= MAX_MAIN_KINKS; j++)
                {
                    if (j > kinkCount)
                    {
                        break;
                    }

                    float remaining = (float)(kinkCount - j + 1);
                    float baseX = (float)j / ((float)kinkCount + 1.0);
                    float xJitter = symmetricHash(seed + (float)j * 17.13) * (0.07 / ((float)kinkCount + 1.0));
                    float currentX = baseX + xJitter;
                    float minX = prevX + minSpacing;
                    float maxX = 1.0 - remaining * minSpacing;
                    currentX = clamp(currentX, minX, maxX);

                    float sign = (fmod((float)j, 2.0) < 0.5) ? -1.0 : 1.0;
                    float magnitude = lerp(_MainOffsetMin, _MainOffsetMax, hash11(seed + (float)j * 31.73));
                    float currentY = sign * magnitude;
                    currentY += prevY * 0.12;
                    currentY = clamp(currentY, -_MainOffsetMax, _MainOffsetMax);

                    if (j == pointIndex)
                    {
                        x = currentX;
                        y = currentY;
                        return;
                    }

                    prevX = currentX;
                    prevY = currentY;
                }

                x = 1.0;
                y = 0.0;
            }

            float EvaluateSegmentedMainOffset(float t, float seed)
            {
                int kinkCount = clamp((int)round(_MainKinkCount), 1, MAX_MAIN_KINKS);

                float prevX = 0.0;
                float prevY = 0.0;

                for (int j = 1; j <= MAX_MAIN_KINKS; j++)
                {
                    if (j > kinkCount)
                    {
                        break;
                    }

                    float currentX;
                    float currentY;
                    GetMainControlPoint(j, kinkCount, seed, currentX, currentY);

                    if (t <= currentX)
                    {
                        float segT = saturate((t - prevX) / max(currentX - prevX, 0.0001));
                        return lerp(prevY, currentY, segT);
                    }

                    prevX = currentX;
                    prevY = currentY;
                }

                return lerp(prevY, 0.0, saturate((t - prevX) / max(1.0 - prevX, 0.0001)));
            }

            void GetBranchControlPoint(int pointIndex, int kinkCount, float seed, float maxOffset, out float x, out float y)
            {
                if (pointIndex <= 0)
                {
                    x = 0.0;
                    y = 0.0;
                    return;
                }

                if (pointIndex >= kinkCount + 1)
                {
                    x = 1.0;
                    y = 0.0;
                    return;
                }

                float prevX = 0.0;
                float prevY = 0.0;
                float minSpacing = 0.2;

                for (int j = 1; j <= MAX_BRANCH_KINKS; j++)
                {
                    if (j > kinkCount)
                    {
                        break;
                    }

                    float remaining = (float)(kinkCount - j + 1);
                    float baseX = (float)j / ((float)kinkCount + 1.0);
                    float xJitter = symmetricHash(seed + (float)j * 19.17) * (0.08 / ((float)kinkCount + 1.0));
                    float currentX = baseX + xJitter;
                    float minX = prevX + minSpacing;
                    float maxX = 1.0 - remaining * minSpacing;
                    currentX = clamp(currentX, minX, maxX);

                    float sign = (fmod((float)j, 2.0) < 0.5) ? -1.0 : 1.0;
                    float magnitude = lerp(maxOffset * 0.25, maxOffset, hash11(seed + (float)j * 29.31));
                    float currentY = sign * magnitude;
                    currentY += prevY * 0.08;
                    currentY = clamp(currentY, -maxOffset, maxOffset);

                    if (j == pointIndex)
                    {
                        x = currentX;
                        y = currentY;
                        return;
                    }

                    prevX = currentX;
                    prevY = currentY;
                }

                x = 1.0;
                y = 0.0;
            }

            float EvaluateSegmentedBranchOffset(float t, float seed, float maxOffset)
            {
                int kinkCount = clamp((int)round(_BranchKinkCount), 0, MAX_BRANCH_KINKS);
                if (kinkCount <= 0)
                {
                    return 0.0;
                }

                float prevX = 0.0;
                float prevY = 0.0;

                for (int j = 1; j <= MAX_BRANCH_KINKS; j++)
                {
                    if (j > kinkCount)
                    {
                        break;
                    }

                    float currentX;
                    float currentY;
                    GetBranchControlPoint(j, kinkCount, seed, maxOffset, currentX, currentY);

                    if (t <= currentX)
                    {
                        float segT = saturate((t - prevX) / max(currentX - prevX, 0.0001));
                        return lerp(prevY, currentY, segT);
                    }

                    prevX = currentX;
                    prevY = currentY;
                }

                return lerp(prevY, 0.0, saturate((t - prevX) / max(1.0 - prevX, 0.0001)));
            }

            float ComputeReseedBlend(float time)
            {
                float cycle = frac(time * _MainReseedRate);
                float holdFraction = 0.78;
                float transition = saturate((cycle - holdFraction) / max(1.0 - holdFraction, 0.001));
                transition = smoothstep(0.0, 1.0, transition);
                return pow(transition, max(_MainTransitionSharpness, 0.001));
            }

            float EvaluateMainBoltOffset(float t, float time)
            {
                float state = floor(time * _MainReseedRate);
                float seedA = state * 17.31;
                float seedB = (state + 1.0) * 17.31;
                float blend = ComputeReseedBlend(time);

                float offsetA = EvaluateSegmentedMainOffset(t, seedA);
                float offsetB = EvaluateSegmentedMainOffset(t, seedB);
                float offset = lerp(offsetA, offsetB, blend);

                float jitter = steppedNoise(t * 48.0 + time * 11.0 + seedA * 3.7) * _MainJitterAmp;
                return offset + jitter;
            }

            void EvaluateMainBoltState(float t, float time, out float offset, out float tangentX, out float tangentY)
            {
                float dt = 0.01;
                float t0 = saturate(t - dt);
                float t1 = saturate(t + dt);
                float offset0 = EvaluateMainBoltOffset(t0, time);
                float offset1 = EvaluateMainBoltOffset(t1, time);

                offset = EvaluateMainBoltOffset(t, time);
                tangentX = t1 - t0;
                tangentY = offset1 - offset0;
            }

            float cylinderFacing(float dist, float radius)
            {
                float nd = saturate(dist / max(radius, 0.0001));
                return sqrt(max(0.0, 1.0 - nd * nd));
            }

            float3 evaluateBolt(float2 uv, float time)
            {
                float t = uv.x;
                float perp = uv.y - 0.5;

                float boltOffset;
                float tangentX;
                float tangentY;
                EvaluateMainBoltState(t, time, boltOffset, tangentX, tangentY);

                float dist = abs(perp - boltOffset);
                float core = saturate(1.0 - dist / _CoreThickness) * _CoreIntensity;

                float coreStrength = saturate(1.0 - dist / (_CoreThickness * 4.0));
                float glow = exp(-dist * dist / (_GlowRadius * _GlowRadius));
                glow *= smoothstep(0.0, 0.15, coreStrength);

                float facing = cylinderFacing(dist, _CoreThickness * 2.0);
                float shade = lerp(1.0, facing, _CylinderHighlight);
                core *= shade;
                glow *= lerp(1.0, 0.55 + 0.45 * facing, _CylinderHighlight);

                float specDist = abs(perp - boltOffset - _SpecularOffset);
                float specular = saturate(1.0 - specDist / (_CoreThickness * 0.5))
                               * _SpecularIntensity
                               * facing;

                float endFade = smoothstep(0.0, _EndFadeWidth, t)
                              * smoothstep(1.0, 1.0 - _EndFadeWidth, t);
                core *= endFade;
                glow *= endFade;
                specular *= endFade;

                return float3(core, glow, specular);
            }

            float2 evaluateBranch(
                float2 uv,
                float2 brOrigin,
                float2 brDirN,
                float brLen,
                float brThickness,
                float brSeed,
                float branchMaxOffset)
            {
                float2 brPerpN = float2(-brDirN.y, brDirN.x);
                float2 toPixel = uv - brOrigin;
                float along = dot(toPixel, brDirN);
                float perp = dot(toPixel, brPerpN);

                float margin = max(brLen * 0.05, 0.003);
                if (along < -margin || along > brLen + margin)
                {
                    return float2(0.0, 0.0);
                }

                float t = saturate(along / max(brLen, 0.0001));
                float boltOffset = EvaluateSegmentedBranchOffset(t, brSeed, branchMaxOffset);
                float dist = abs(perp - boltOffset);

                float core = saturate(1.0 - dist / max(brThickness, 0.0001)) * _CoreIntensity * 0.5;
                float corePresence = saturate(1.0 - dist / max(brThickness * 3.0, 0.0001));
                float glow = exp(-dist * dist / (_BranchGlowRadius * _BranchGlowRadius)) * 0.42;
                glow *= smoothstep(0.0, 0.12, corePresence);

                float taper = smoothstep(0.0, 0.06, t) * (1.0 - t);
                taper *= taper;
                core *= taper;
                glow *= taper;

                return float2(core, glow);
            }

            void BuildBranchState(
                int branchIndex,
                float time,
                out bool isVisible,
                out float2 origin,
                out float2 dirN,
                out float lengthUV,
                out float thicknessUV,
                out float branchSeed,
                out float maxOffset)
            {
                isVisible = false;
                origin = float2(0.0, 0.0);
                dirN = float2(1.0, 0.0);
                lengthUV = 0.0;
                thicknessUV = 0.0;
                branchSeed = 0.0;
                maxOffset = 0.0;

                int branchCount = clamp((int)round(_MaxBranches), 0, MAX_BRANCHES);
                if (branchIndex >= branchCount)
                {
                    return;
                }

                int kinkCount = clamp((int)round(_MainKinkCount), 1, MAX_MAIN_KINKS);
                float state = floor(time * _MainReseedRate);
                float blend = ComputeReseedBlend(time);
                float phaseSeedA = state * 17.31 + (float)branchIndex * 41.0;
                float phaseSeedB = (state + 1.0) * 17.31 + (float)branchIndex * 41.0;

                float visA = hash11(phaseSeedA + 9.0);
                float visB = hash11(phaseSeedB + 9.0);
                bool showA = visA <= _BranchSpawnChance;
                bool showB = visB <= _BranchSpawnChance;
                if (!showA && !showB)
                {
                    return;
                }

                float activeSeed = (blend < 0.5 || !showB) ? phaseSeedA : phaseSeedB;
                if (blend >= 0.5 && showB)
                {
                    activeSeed = phaseSeedB;
                }

                int jointIndex = clamp(1 + (int)floor(hash11(activeSeed + 13.0) * (float)kinkCount), 1, kinkCount);

                float pointX;
                float pointY;
                GetMainControlPoint(jointIndex, kinkCount, activeSeed, pointX, pointY);
                origin = float2(pointX + lerp(0.0, 0.04, hash11(activeSeed + 15.0)), pointY);

                float prevX;
                float prevY;
                float nextX;
                float nextY;
                GetMainControlPoint(jointIndex - 1, kinkCount, activeSeed, prevX, prevY);
                GetMainControlPoint(jointIndex + 1, kinkCount, activeSeed, nextX, nextY);

                float2 tangent = normalizeOrFallback(float2(nextX - prevX, nextY - prevY), float2(1.0, 0.0));
                float2 outward = float2(-tangent.y, tangent.x);
                float side = pointY >= 0.0 ? 1.0 : -1.0;
                if (side * outward.y < 0.0)
                {
                    outward *= -1.0;
                }

                float2 baseDir = normalizeOrFallback(lerp(tangent, outward, _BranchOutwardBias), outward);
                float angleDeg = lerp(_BranchAngleMin, _BranchAngleMax, hash11(activeSeed + 17.0));
                float angleRad = radians(angleDeg) * side;
                dirN = normalizeOrFallback(rotate2(baseDir, angleRad), outward);

                lengthUV = lerp(_BranchLengthMin, _BranchLengthMax, hash11(activeSeed + 19.0));
                thicknessUV = lerp(_BranchThicknessMin, _BranchThicknessMax, hash11(activeSeed + 21.0));
                maxOffset = min(lengthUV * 0.18, _MainOffsetMax * 0.7);
                branchSeed = activeSeed + 23.0;
                isVisible = true;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float time = _Time.y;

                float3 boltResult = evaluateBolt(uv, time);
                float totalCore = boltResult.x;
                float totalGlow = boltResult.y;
                float totalSpec = boltResult.z;

                float flicker = 1.0 - _Flicker * (0.5 + 0.5 * sin(time * 16.0));
                flicker *= 0.88 + 0.12 * smoothHash11(floor(time * 7.0) + 3.0);
                totalCore *= flicker;
                totalGlow *= flicker;
                totalSpec *= flicker;

                for (int br = 0; br < MAX_BRANCHES; br++)
                {
                    bool visible;
                    float2 origin;
                    float2 dirN;
                    float lengthUV;
                    float thicknessUV;
                    float branchSeed;
                    float branchMaxOffset;

                    BuildBranchState(br, time, visible, origin, dirN, lengthUV, thicknessUV, branchSeed, branchMaxOffset);
                    if (!visible)
                    {
                        continue;
                    }

                    float2 branchResult = evaluateBranch(
                        uv,
                        origin,
                        dirN,
                        lengthUV,
                        thicknessUV,
                        branchSeed,
                        branchMaxOffset);

                    totalCore = max(totalCore, branchResult.x * flicker);
                    totalGlow = max(totalGlow, branchResult.y * flicker);
                }

                float3 finalColor = _BoltColor.rgb * totalCore
                                  + _BoltGlowColor.rgb * totalGlow * _GlowIntensity * 0.35
                                  + float3(1.0, 1.0, 1.0) * totalSpec;

                float alpha = saturate(totalCore * 0.5 + totalGlow * 0.3 + totalSpec * 0.25);
                finalColor *= _ExternalIntensity;
                alpha = saturate(alpha * _ExternalIntensity);

                clip(alpha - 0.001);
                return fixed4(finalColor, alpha);
            }
            ENDCG
        }
    }
}
