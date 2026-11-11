// Experiment: stylised toon for the witch game (URP, Unity 6).
// Fixes the two things that felt off in Magic/ToonCharacter:
//  * Colours/shadows: shadows are not a flat blue multiply. They are a hue-shifted, slightly more saturated version of the albedo
//    (cool-shifted), with a warm light side, a soft painted core-shadow band, and a bounce-light tint in the darkest part.
//  * Outline: constant width in PIXELS (not world units), coloured from the albedo instead of one dark blue for everything, and it uses
//    smoothed normals (blend of mesh normal and direction from the pivot) so hard-edged low-poly models do not split open.
Shader "Magic/ToonStylized"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Toggle] _UseVertexColor ("Use Vertex Colour", Float) = 0

        [Header(Light)]
        _LightWarmth ("Lit Side Warm Tint", Color) = (1.0, 0.96, 0.88, 1)
        _ShadowHueShift ("Shadow Hue Shift (cool)", Range(0, 1)) = 0.55
        _ShadowTint ("Shadow Colour (multiplied)", Color) = (0.62, 0.62, 0.9, 1)
        _ShadowSaturation ("Shadow Saturation Boost", Range(0.5, 2)) = 1.25
        _ShadowDepth ("Shadow Depth", Range(0, 1)) = 0.5
        _TerminatorPos ("Shadow Edge Position", Range(0.2, 0.8)) = 0.5
        _TerminatorSoftness ("Shadow Edge Softness", Range(0.005, 0.6)) = 0.3
        _CastSoftness ("Cast Shadow Softness", Range(0, 1)) = 0.8
        _ReceiveShadow ("Receive Cast Shadows (0 = none)", Range(0, 1)) = 1
        _ShadeAmount ("Form Shading Amount (0 = flat, no shadow side)", Range(0, 1)) = 1
        _CoreShadowPos ("Core Shadow Position", Range(0.0, 0.5)) = 0.22
        _CoreShadowStrength ("Core Shadow Strength", Range(0, 1)) = 0.35
        _BounceColor ("Bounce Light Colour", Color) = (0.95, 0.7, 0.8, 1)
        _BounceStrength ("Bounce Strength", Range(0, 1)) = 0.1
        _AmbientLift ("Ambient Lift", Range(0, 1)) = 0.3

        [Header(Shadow Style)]
        [Enum(Smooth, 0, Hard Cel, 1, Painterly Edge, 2)] _ShadowStyle ("Shadow Style", Float) = 0
        _PaintAmount ("Painterly: Edge Breakup", Range(0, 1)) = 0.35
        _PaintScale ("Painterly: Brush Size (higher = smaller)", Range(0.5, 20)) = 4

        [Header(Kake ami Hatching)]
        _HatchStrength ("Hatching Strength (0 = off)", Range(0, 1)) = 0
        _HatchColor ("Hatch Line Colour", Color) = (0.16, 0.1, 0.3, 1)
        _HatchSpacing ("Hatch Spacing (pixels)", Range(3, 40)) = 9
        _HatchAngle ("Hatch Angle (degrees)", Range(0, 180)) = 45
        _HatchThickness ("Hatch Line Thickness", Range(0.05, 1)) = 0.5
        _HatchStart ("Hatch Starts Where Light Below", Range(0, 1)) = 0.5
        _HatchGap ("Light Drop Between Layers", Range(0.05, 0.5)) = 0.18

        [Header(Specular and Rim)]
        _SpecColor2 ("Highlight Colour", Color) = (1, 0.97, 0.9, 1)
        _SpecSize ("Highlight Size", Range(0, 1)) = 0.12
        _SpecSoftness ("Highlight Softness", Range(0.001, 0.3)) = 0.03
        _SpecStrength ("Highlight Strength", Range(0, 1)) = 0.5
        _RimColor ("Rim Colour", Color) = (0.8, 0.9, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.25

        [Header(Outline)]
        _OutlineColor ("Outline Colour", Color) = (0.1, 0.08, 0.2, 1)
        _OutlineFromAlbedo ("Outline Takes Albedo Colour", Range(0, 1)) = 0.6
        _OutlineWidth ("Outline Width (pixels)", Range(0, 20)) = 5
        _OutlineSmooth ("Outline Normal Smoothing", Range(0, 1)) = 0.6
        _OutlineVariation ("Stroke Variation (thick/thin like ink)", Range(0, 1)) = 0.8
        _OutlineThinMin ("Thinnest Stroke (x width)", Range(0, 1)) = 0.25
        _OutlineThickMax ("Thickest Stroke (x width)", Range(1, 4)) = 2.2
        _OutlineVarFreq ("Stroke Variation Frequency", Range(0.2, 12)) = 2.5
        [Toggle] _OutlineShiftFromLight ("Shift Line Away From Light (ignores X/Y below)", Float) = 0
        _OutlineShiftAmount ("Shift Amount When Following Light", Range(0, 1)) = 1
        _OutlineShiftX ("Stroke Shift X on screen (- left, + right, max +-1)", Range(-1, 1)) = 0
        _OutlineShiftY ("Stroke Shift Y on screen (- down, + up, max +-1)", Range(-1, 1)) = 0
        _OutlineOffset ("Stroke Offset (slides the thick/thin pattern, same frequency, max +-1)", Range(-1, 1)) = 0
        _OutlineFadeStart ("Far Shrink Starts At Distance", Range(1, 100)) = 8
        _OutlineFarMin ("Far Minimum Width (x)", Range(0, 1)) = 0.15
        _OutlineDepthPush ("Outline Push Back (keeps it out of neighbours)", Range(0, 20)) = 6
        [IntRange] _OutlineGroup ("Outline Group (same number = no lines between them)", Range(1, 255)) = 1

        [Header(Skinned Characters)]
        [Toggle] _UseRestPosition ("Use Rest Position From UV0 (witch mesh: keeps patterns on the cloth)", Float) = 0
        _SparkleColor ("Sparkle Colour", Color) = (1, 0.93, 0.6, 1)
        _SparkleScale ("Sparkle Density", Range(2, 60)) = 22
        _SparkleSpeed ("Sparkle Speed", Range(0, 8)) = 2.5
        _SparkleStrength ("Sparkle Strength (vertex alpha = how sparkly)", Range(0, 2)) = 0

        [Header(Glow)]
        [HDR] _EmissionColor ("Glow Colour", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Glow Strength", Range(0, 6)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _LightWarmth, _ShadowTint, _BounceColor, _SpecColor2, _RimColor, _OutlineColor, _EmissionColor;
            half _CastSoftness, _ReceiveShadow, _ShadeAmount, _UseVertexColor, _ShadowHueShift, _ShadowSaturation, _ShadowDepth, _TerminatorPos, _TerminatorSoftness;
            half _CoreShadowPos, _CoreShadowStrength, _BounceStrength, _AmbientLift;
            half _SpecSize, _SpecSoftness, _SpecStrength, _RimPower, _RimStrength;
            half _OutlineFromAlbedo, _OutlineWidth, _OutlineSmooth, _OutlineDepthPush;
            half _OutlineVariation, _OutlineThinMin, _OutlineThickMax, _OutlineVarFreq, _OutlineShiftFromLight, _OutlineShiftAmount;
            half _OutlineShiftX, _OutlineShiftY, _OutlineFadeStart, _OutlineFarMin, _OutlineOffset;
            half4 _HatchColor, _SparkleColor;
            half _UseRestPosition, _SparkleScale, _SparkleSpeed, _SparkleStrength, _EmissionStrength;
            half _ShadowStyle, _PaintAmount, _PaintScale;
            half _HatchStrength, _HatchSpacing, _HatchAngle, _HatchThickness, _HatchStart, _HatchGap;
        CBUFFER_END
        ENDHLSL

        // ---------------- toon colour ----------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            // marks every pixel of this object's body with its outline group; the outline pass refuses to draw on those pixels,
            // so parts of one object (and any objects sharing the group) never get outline lines running over each other
            Stencil { Ref [_OutlineGroup] Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "ToonOutline.hlsl"      // only for the noise helpers (Noise3)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 uv : TEXCOORD0;      // xy = texture uv; z only used by the witch mesh (rest-pose position)
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 posOS : TEXCOORD3;
                half4 color : COLOR;
            };

            // one set of parallel lines in screen pixels; depth (0..1) says how deep into the shadow we are and thickens the line
            half HatchLine(float2 pix, float ang, float spacing, half depth)
            {
                if (depth <= 0.001) return 0;
                float v = dot(pix, float2(cos(ang), sin(ang))) / spacing;
                float f = abs(frac(v) - 0.5) * 2.0;                       // 1 on the line centre, 0 half way between lines
                float th = lerp(0.12, 1.0, depth) * _HatchThickness;
                float aa = max(fwidth(v) * 2.0, 0.02);
                return smoothstep(1.0 - th - aa, 1.0 - th + aa, f);
            }

            // a twinkling four-point star in a random spot of each cell of the rest-pose grid (same sparkles as the old witch shader)
            half Sparkle(float3 rest)
            {
                float3 q = rest * _SparkleScale;
                float3 cell = floor(q);
                float3 local = frac(q) - 0.5;
                float h = Hash31(cell);
                if (h < 0.55) return 0;                                            // most cells stay empty

                float3 off = (float3(Hash31(cell + 3.1), Hash31(cell + 7.7), Hash31(cell + 1.9)) - 0.5) * 0.5;
                float dist = length(local - off);
                float twinkle = 0.5 + 0.5 * sin(_Time.y * _SparkleSpeed + h * 40.0);
                float size = 0.16 * twinkle;
                float core = saturate(1.0 - dist / max(size, 0.001));
                return core * core;
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.posOS = _UseRestPosition > 0.5 ? i.uv : i.positionOS.xyz;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv.xy, _BaseMap);
                o.color = i.color;
                return o;
            }

            half Luma(half3 c) { return dot(c, half3(0.299, 0.587, 0.114)); }

            // shadow colour: rotate the albedo towards cool, saturate it, then multiply by the tint. Never a grey/black stain.
            half3 ShadowOf(half3 albedo)
            {
                half l = Luma(albedo);
                half3 sat = lerp(half3(l, l, l), albedo, _ShadowSaturation);
                // a cool shift: pull red down, push blue up in proportion to how much we shift
                half3 cool = half3(sat.r * 0.82, sat.g * 0.92, sat.b * 1.08);
                half3 s = lerp(sat, cool, _ShadowHueShift) * _ShadowTint.rgb;
                return lerp(albedo, s, _ShadowDepth * 2.0 > 1.0 ? 1.0 : _ShadowDepth * 2.0) * lerp(1.0, 0.82, saturate(_ShadowDepth * 2.0 - 1.0));
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb * lerp(half3(1, 1, 1), i.color.rgb, _UseVertexColor);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS + n * 0.03);
                Light l = GetMainLight(shadowCoord);

                half ndl = dot(n, l.direction) * 0.5 + 0.5;                          // half-lambert
                half cast = smoothstep(0.0, 1.0, l.shadowAttenuation);               // soft-shadow filter already blurs the edge; this widens it
                cast = lerp(step(0.5, l.shadowAttenuation), cast, _CastSoftness);
                cast = lerp(1.0, cast, _ReceiveShadow);                              // 0 = ignore all cast shadows
                half lit = lerp(1.0, ndl, _ShadeAmount) * lerp(0.45, 1.0, cast);                             // cast shadows ease a surface into the shadow band

                // two-step painted look: lit / shadow with a soft edge, plus a darker core shadow band inside the shadow
                // shadow style: Smooth keeps the soft gradient; Hard Cel makes the edges crisp (like anime key art);
                // Painterly Edge breaks the edge up with brush-like noise that sticks to the object
                half soft = _TerminatorSoftness;
                half litS = lit;
                if (_ShadowStyle > 1.5)
                {
                    float nz = Noise3(i.posOS * _PaintScale) * 0.65 + Noise3(i.posOS * _PaintScale * 2.7 + 7.3) * 0.35;
                    litS += (nz - 0.5) * _PaintAmount;
                    soft = min(soft, 0.07);
                }
                else if (_ShadowStyle > 0.5) soft = 0.012;

                half toLit = smoothstep(_TerminatorPos - soft, _TerminatorPos + soft, litS);
                half core = smoothstep(_CoreShadowPos - soft, _CoreShadowPos + soft, litS);

                half3 shadowCol = ShadowOf(albedo);
                half3 coreCol = shadowCol * lerp(1.0, 0.7, _CoreShadowStrength);
                half3 darkSide = lerp(coreCol, shadowCol, core);

                // reflected bounce light tints the darkest part so shadows feel lit from the ground
                half bounceMask = saturate(-n.y * 0.5 + 0.5) * (1.0 - core * 0.6) * (1.0 - toLit);
                darkSide += _BounceColor.rgb * albedo * bounceMask * _BounceStrength;

                half3 litCol = albedo * lerp(half3(1, 1, 1), _LightWarmth.rgb, 0.8) * (l.color * 0.9 + 0.1);
                half3 col = lerp(darkSide, litCol, toLit);
                col += albedo * SampleSH(n) * _AmbientLift * 0.5;

                // stylised highlight: a crisp blob that only appears on the lit side
                half3 hv = normalize(l.direction + v);
                half spec = pow(saturate(dot(n, hv)), lerp(300.0, 8.0, _SpecSize));
                spec = smoothstep(0.5 - _SpecSoftness, 0.5 + _SpecSoftness, spec) * toLit * cast;
                col = lerp(col, _SpecColor2.rgb, spec * _SpecStrength);

                // rim light that respects the light direction
                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                rim *= _RimStrength * saturate(dot(n, l.direction) * 0.5 + 0.6);
                col += _RimColor.rgb * rim * 0.6;

                // kake-ami: crossing ink hatch lines in the shadow. The deeper the shadow, the more layers cross and the thicker they get:
                // layer 1 (angle), layer 2 (crossing it at 90 degrees) one step darker, layer 3 (45 degrees between) in the deepest part
                if (_HatchStrength > 0.001)
                {
                    float2 pix = i.positionCS.xy;
                    float a0 = radians(_HatchAngle);
                    half d1 = saturate((_HatchStart - litS) / _HatchGap);
                    half d2 = saturate((_HatchStart - _HatchGap - litS) / _HatchGap);
                    half d3 = saturate((_HatchStart - _HatchGap * 2.0 - litS) / _HatchGap);
                    half hatch = HatchLine(pix, a0, _HatchSpacing, d1);
                    hatch = max(hatch, HatchLine(pix, a0 + 1.5708, _HatchSpacing, d2));
                    hatch = max(hatch, HatchLine(pix, a0 + 0.7854, _HatchSpacing * 0.8, d3));
                    col = lerp(col, _HatchColor.rgb, hatch * _HatchStrength);
                }

                // little stars on the cloth (vertex alpha = how sparkly)
                col += _SparkleColor.rgb * Sparkle(i.posOS) * i.color.a * _SparkleStrength;

                col += _EmissionColor.rgb * _EmissionStrength;
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ---------------- outline: drawn by the "ToonOutline" Render Objects renderer feature AFTER all opaque bodies ----------------
        // so every body has already stamped the stencil and the outline never depends on which object Unity happened to draw first.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "ToonOutline" }
            Cull Front
            Stencil { Ref [_OutlineGroup] Comp NotEqual Pass Keep }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "ToonOutline.hlsl"
            OutlineVaryings vert(OutlineAttributes i) { return OutlineVert(i); }
            half4 frag(OutlineVaryings i) : SV_Target { return OutlineFrag(i); }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    Fallback "Universal Render Pipeline/Lit"
}
