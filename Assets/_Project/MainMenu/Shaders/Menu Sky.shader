Shader "BounceBack/Menu Sky"
{
    Properties
    {
        _Zenith("Upper sky", Color) = (0.69, 0.80, 0.82, 1)
        _Horizon("Horizon", Color) = (0.84, 0.88, 0.82, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon;
            CBUFFER_END
            struct Varyings { float4 position : SV_POSITION; float4 screen : TEXCOORD0; };

            Varyings Vert(float3 vertex : POSITION)
            {
                Varyings output;
                output.position = TransformObjectToHClip(vertex);
                output.screen = ComputeScreenPos(output.position);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half depth = smoothstep(0, 1, input.screen.y / input.screen.w);
                return half4(lerp(_Horizon.rgb, _Zenith.rgb, depth), 1);
            }
            ENDHLSL
        }
    }
}
