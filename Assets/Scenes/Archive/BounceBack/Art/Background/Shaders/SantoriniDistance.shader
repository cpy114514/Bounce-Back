Shader "BounceBack/Santorini Distance"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (0.95, 0.93, 0.89, 0.4)
        _MainTex ("Soft Mask", 2D) = "white" {}
        _Opacity ("Opacity", Range(0, 1)) = 0.45
        _Rise ("Band Width", Range(0.05, 0.8)) = 0.32
        _MaskAmount ("Mask Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Santorini Distance"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _MainTex_ST;
                half _Opacity;
                half _Rise;
                half _MaskAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // pow() is undefined for a negative base in HLSL, so the band is a squared falloff.
                float2 centered = input.uv - 0.5;
                float rise = max(_Rise, 0.05);
                float band = exp(-(centered.y * centered.y) / (rise * rise));
                float sides = 1.0 - smoothstep(0.70, 1.08, abs(centered.x) * 2.0);
                float swell = 0.94 + 0.06 * sin(centered.x * 6.0);
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                float cloud = band * sides * swell * lerp(0.78, 1.0, mask);
                float shape = lerp(cloud, mask, _MaskAmount);
                half alpha = saturate(shape * _BaseColor.a * _Opacity);
                if (alpha < 0.004)
                    discard;
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
