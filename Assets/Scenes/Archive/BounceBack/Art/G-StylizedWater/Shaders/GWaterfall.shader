Shader "BounceBack/G Stylized Waterfall"
{
    Properties
    {
        [MainColor] _BaseColor ("MV_Water · cyan (sRGB)", Color) = (0.45,0.80,0.84,1)
        _FlowSpeed ("Downward flow speed", Range(0,2)) = 0.72
        _Opacity ("Upper water opacity", Range(0,1)) = 0.98
        _FadeStart ("Bottom fade start (UV)", Range(0.5,0.98)) = 0.78
        _Sway ("Geometric sway (m)", Range(0,0.02)) = 0.012
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Flowing Ribbon Forward"
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
                half _FlowSpeed;
                half _Opacity;
                half _FadeStart;
                half _Sway;
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
                float3 p = input.positionOS.xyz;
                // The outlet itself stays fixed; sway is sub-centimetre on the fall.
                float3 world = TransformObjectToWorld(p);
                world.x += sin(input.uv.y * 12.0 - (_GPreviewTime > 0.0 ? _GPreviewTime : _Time.y) * 1.15) * _Sway * input.uv.y;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float t = (_GPreviewTime > 0.0 ? _GPreviewTime : _Time.y) * _FlowSpeed;
                float2 uv = input.uv;
                float flow = uv.y * 8.0 - t * 1.9;
                float stream = sin(uv.x * 37.7 + sin(flow * 0.72) * 0.65) * 0.5 + 0.5;
                float broad = sin(uv.x * 12.57 + 0.8) * 0.5 + 0.5;
                float moving = sin(flow * 6.28318 + uv.x * 3.0) * 0.5 + 0.5;
                float ribbon = smoothstep(0.79, 0.975, stream);
                float pulse = smoothstep(0.68, 0.98, moving);
                float streak = ribbon * (0.18 + 0.82 * pulse);
                // A few broken, pale foam glides, not photo-real refraction.
                float broken = smoothstep(0.88, 0.99, sin(uv.y * 27.0 - t * 9.0 + uv.x * 6.0) * 0.5 + 0.5);
                streak = saturate(streak + ribbon * broken * 0.40);
                half3 color = _BaseColor.rgb * lerp(0.88h, 1.10h, broad);
                color = lerp(color, half3(0.88h,0.98h,0.96h), streak * 0.84h);
                float lip = 1.0 - smoothstep(0.008, 0.035, uv.y);
                color = lerp(color, half3(0.96h,0.99h,0.95h), lip * 0.75h);
                float sides = smoothstep(0.0, 0.035, uv.x) * smoothstep(0.0, 0.035, 1.0-uv.x);
                float fade = 1.0 - smoothstep(_FadeStart, 1.0, uv.y);
                half alpha = _Opacity * sides * fade;
                if (alpha < 0.008) discard;
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
