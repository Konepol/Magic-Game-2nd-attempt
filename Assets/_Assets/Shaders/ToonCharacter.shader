// Soft pastel toon shading for the witch and her wand: a few soft light bands with a cool shadow tint, a glassy rim glow,
// tiny twinkling stars on the cloth, a thin dark-blue outline and an optional glow (used by the wand tip).
// The colour comes from the mesh's vertex colours (rgb) multiplied by Tint; the vertex alpha says how much star sparkle a spot gets.
// The mesh's first UV set holds the rest-pose position so the stars stay on the cloth while the character animates.
Shader "Magic/ToonCharacter"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shadow Tint", Color) = (0.55, 0.62, 0.92, 1)
        _Softness ("Band Softness", Range(0.01, 0.5)) = 0.12
        _Bands ("Light Bands", Range(2, 4)) = 3
        _AmbientLift ("Ambient Lift", Range(0, 1)) = 0.35
        _ShadowFloor ("Cast Shadow Brightness", Range(0, 1)) = 0.45
        _ShadowNormalOffset ("Shadow Normal Offset", Range(0, 0.2)) = 0.06

        _RimColor ("Rim Colour", Color) = (0.75, 0.9, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.6
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.8

        _SparkleColor ("Sparkle Colour", Color) = (1, 0.93, 0.6, 1)
        _SparkleScale ("Sparkle Density", Range(2, 60)) = 22
        _SparkleSpeed ("Sparkle Speed", Range(0, 8)) = 2.5
        _SparkleStrength ("Sparkle Strength", Range(0, 2)) = 1

        _EmissionColor ("Glow Colour", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Glow Strength", Range(0, 6)) = 0

        _OutlineColor ("Outline Colour", Color) = (0.12, 0.17, 0.4, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.04)) = 0.008
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        // ---------------- the toon colour ----------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadeColor, _RimColor, _SparkleColor, _EmissionColor, _OutlineColor;
                half _Softness, _Bands, _AmbientLift, _RimPower, _RimStrength;
                half _SparkleScale, _SparkleSpeed, _SparkleStrength, _EmissionStrength, _OutlineWidth, _ShadowFloor, _ShadowNormalOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float3 rest : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float3 rest : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.rest = i.rest;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // a twinkling four-point star in a random spot of each cell of the rest-pose grid
            half Sparkle(float3 rest)
            {
                float3 q = rest * _SparkleScale;
                float3 cell = floor(q);
                float3 local = frac(q) - 0.5;
                float h = Hash(cell);
                if (h < 0.55) return 0;                                            // most cells stay empty

                float3 off = (float3(Hash(cell + 3.1), Hash(cell + 7.7), Hash(cell + 1.9)) - 0.5) * 0.5;
                float3 d = local - off;
                float dist = length(d);
                float twinkle = 0.5 + 0.5 * sin(_Time.y * _SparkleSpeed + h * 40.0);
                float size = 0.16 * twinkle;
                float core = saturate(1.0 - dist / max(size, 0.001));
                return core * core;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                // the shadow map is read a little off the surface (no blotchy self-shadowing on round shapes) and its edge is softened;
                // a shadow only lowers the light to the middle band, so it reads as a soft toon shadow rather than a dark stain
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS + n * _ShadowNormalOffset);
                Light l = GetMainLight(shadowCoord);
                half shadow = smoothstep(0.2, 0.8, l.shadowAttenuation);

                half ndl = dot(n, l.direction) * 0.5 + 0.5;                          // wrap lighting keeps the shadow side soft
                half lit = min(ndl, lerp(_ShadowFloor, 1.0, shadow));

                // a few soft bands from shadow to light
                half steps = max(_Bands - 1, 1);
                half stepped = floor(lit * steps) / steps;
                half edge = frac(lit * steps);
                half soft = smoothstep(0.5 - _Softness, 0.5 + _Softness, edge);
                half band = saturate(stepped + soft / steps);

                half3 albedo = i.color.rgb * _BaseColor.rgb;
                half3 shade = albedo * _ShadeColor.rgb;
                half3 ambient = SampleSH(n) * _AmbientLift;
                half3 col = lerp(shade, albedo, band) * (l.color * 0.85 + 0.15) + albedo * ambient * 0.5;

                // glassy rim light, stronger on the lit side
                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimStrength * (0.35 + 0.65 * band);
                col += _RimColor.rgb * rim;

                // little stars on the cloth (vertex alpha = how sparkly)
                col += _SparkleColor.rgb * Sparkle(i.rest) * i.color.a * _SparkleStrength;

                col += _EmissionColor.rgb * _EmissionStrength;
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ---------------- the thin outline (the back faces, pushed outward) ----------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadeColor, _RimColor, _SparkleColor, _EmissionColor, _OutlineColor;
                half _Softness, _Bands, _AmbientLift, _RimPower, _RimStrength;
                half _SparkleScale, _SparkleSpeed, _SparkleStrength, _EmissionStrength, _OutlineWidth, _ShadowFloor, _ShadowNormalOffset;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 pos = i.positionOS.xyz + normalize(i.normalOS) * _OutlineWidth;
                o.positionCS = TransformObjectToHClip(pos);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return half4(_OutlineColor.rgb, 1); }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    Fallback "Universal Render Pipeline/Lit"
}
