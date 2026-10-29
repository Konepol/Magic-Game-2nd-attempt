// Ground and walls with stylised shadows: the shadow is a tint (cool lavender by default) instead of near-black, and its edge can be anything
// from crisp cel (Softness 0) to very soft (Softness 1). With "Receive Shadows" off the surface ignores real shadows (for blob shadows).
Shader "Magic/ToonGround"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.55, 0.58, 0.86, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.85
        _ShadowSoftness ("Shadow Edge Softness (0 = cel)", Range(0, 1)) = 0.6
        _ReceiveShadow ("Receive Shadows", Float) = 1
        _AmbientLift ("Ambient", Range(0, 1)) = 0.35
        _SlopeShade ("Shading On Slopes", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _ShadowTint;
                half _ShadowStrength, _ShadowSoftness, _ReceiveShadow, _AmbientLift, _SlopeShade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;

                float3 p = i.positionWS + n * 0.02;
                Light l = GetMainLight(TransformWorldToShadowCoord(p));

                // soft shadows: the shadow map is read at a ring of points around this spot and averaged (a wide, smooth edge);
                // cel shadows: one read and a hard step
                half shadow = 1.0;
                if (_ReceiveShadow > 0.5)
                {
                    if (_ShadowSoftness > 0.01)
                    {
                        float3 t = normalize(abs(n.y) < 0.9 ? cross(n, float3(0, 1, 0)) : cross(n, float3(1, 0, 0)));
                        float3 b = cross(n, t);
                        float r = _ShadowSoftness * 0.45;
                        half sum = l.shadowAttenuation;
                        [unroll] for (int k = 0; k < 12; k++)
                        {
                            float a = k * 2.39996;                         // golden angle spiral
                            float d = r * sqrt((k + 0.5) / 12.0);
                            float3 q = p + (t * cos(a) + b * sin(a)) * d;
                            sum += MainLightRealtimeShadow(TransformWorldToShadowCoord(q));
                        }
                        shadow = smoothstep(0.0, 1.0, sum / 13.0);
                    }
                    else shadow = step(0.5, l.shadowAttenuation);
                }

                // faces turned away from the light get a little of the same tint, so walls and slopes still read
                half ndl = saturate(dot(n, l.direction));
                half facing = lerp(1.0, ndl, _SlopeShade);
                half light = min(shadow, facing);

                half3 tint = lerp(half3(1, 1, 1), _ShadowTint.rgb, _ShadowStrength);
                half3 col = albedo * lerp(tint, half3(1, 1, 1), light) * (l.color * 0.85 + 0.15) + albedo * SampleSH(n) * _AmbientLift;
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    Fallback "Universal Render Pipeline/Lit"
}
