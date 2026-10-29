// A soft glowing ball in 3D: bright in the middle and fading toward the edge (no hard outline), additive. Used for the core of spell effects.
Shader "Magic/GlowOrb"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 0.8, 0.35, 1)
        _Falloff ("Edge Falloff", Range(0.5, 6)) = 2.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Glow"
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Falloff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half facing = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half a = pow(facing, _Falloff) * _BaseColor.a;
                return half4(_BaseColor.rgb, a);
            }
            ENDHLSL
        }
    }
}
