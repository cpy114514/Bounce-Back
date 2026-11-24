Shader "BounceBack/Geometry Matte"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.32
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "Matte Forward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.position.xyz);
                output.position = TransformWorldToHClip(output.world);
                output.normal = TransformObjectToWorldNormal(input.normal);
                output.fog = ComputeFogFactor(output.position.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normal);
                half3 key = normalize(half3(-0.56, 0.83, -0.41));
                half diffuse = saturate(dot(normal, key));
                half3 fill = lerp(half3(0.62, 0.66, 0.77), half3(1.02, 0.99, 0.94), diffuse);
                half3 view = GetWorldSpaceNormalizeViewDir(input.world);
                half rim = 1.0h - saturate(dot(normal, view));
                half sheen = pow(saturate(dot(normal, normalize(view + key))), lerp(8.0h, 32.0h, _Smoothness));
                half3 color = _BaseColor.rgb * fill + (0.037h * rim * rim + sheen * (0.025h + 0.10h * _Metallic));
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
