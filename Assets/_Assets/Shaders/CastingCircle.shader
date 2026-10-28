// Unlit, glowing (additive) circle texture with a clockwise "draw-on" reveal, used by the casting circles.
Shader "Magic/CastingCircle"
{
    Properties
    {
        _BaseMap ("Circle Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Reveal ("Reveal (0 = hidden, 1 = fully drawn)", Range(0, 1)) = 1
        _Soft ("Reveal Edge Softness", Range(0.001, 0.3)) = 0.04
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Unlit"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                half _Reveal;
                half _Soft;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // angle around the centre, 0 at the top going clockwise to 1
                float2 p = input.uv - 0.5;
                float t = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);

                half mask = _Reveal >= 0.999 ? 1.0 : saturate((_Reveal - t) / _Soft);

                half3 rgb = tex.rgb * _Color.rgb;
                half a = tex.a * _Color.a * mask;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
