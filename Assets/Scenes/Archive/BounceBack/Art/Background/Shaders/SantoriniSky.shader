Shader "BounceBack/Santorini Sky"
{
    Properties
    {
        _Mist ("Mist Blue", Color) = (0.63, 0.79, 0.90, 1)
        _Air ("Air", Color) = (0.78, 0.87, 0.93, 1)
        _Ivory ("Warm Ivory", Color) = (0.96, 0.92, 0.84, 1)
        _Sea ("Lower Mist", Color) = (0.84, 0.88, 0.90, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Background"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Santorini Sky"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Mist;
                half4 _Air;
                half4 _Ivory;
                half4 _Sea;
            CBUFFER_END

            struct Attributes
            {
                float4 position : POSITION;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 world : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.position.xyz);
                output.position = TransformWorldToHClip(output.world);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = saturate(input.position.y / _ScreenParams.y);
                half3 color = lerp(_Sea.rgb, _Ivory.rgb, smoothstep(0.0, 0.34, height));
                color = lerp(color, _Air.rgb, smoothstep(0.24, 0.68, height));
                color = lerp(color, _Mist.rgb, smoothstep(0.58, 1.0, height));

                float breath = sin(input.world.x * 0.008 + input.world.z * 0.011);
                color = lerp(color, color * half3(1.015, 1.0, 0.985), breath * 0.018);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
