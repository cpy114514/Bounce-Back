Shader "BounceBack/G Water Mist"
{
    Properties
    {
        [MainColor] _BaseColor ("MV_Water · mist tint (sRGB)", Color) = (0.80,0.91,0.91,1)
        _Opacity ("Mist opacity", Range(0,1)) = 0.48
        _Drift ("Slow drift (m)", Range(0,0.15)) = 0.065
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" "IgnoreProjector"="True" }
        Pass
        {
            Name "Soft Water Mist"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Opacity;
                half _Drift;
            CBUFFER_END
            float _GPreviewTime;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float t = _GPreviewTime > 0.0 ? _GPreviewTime : _Time.y;
                world.x += sin(t * 0.44 + world.z * 0.65) * _Drift;
                world.y += sin(t * 0.32 + world.z * 0.5) * _Drift * 0.28;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 q = (input.uv - 0.5) * 2.0;
                float shape = saturate(1.0 - dot(q,q));
                float alpha = shape * shape * _Opacity;
                if (alpha < 0.006) discard;
                half3 color = lerp(_BaseColor.rgb, half3(0.95h,0.985h,0.965h), shape * 0.65h);
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
