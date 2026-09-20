// Плоский вертекс-колор Unlit для звёзд-геометрии купола (s10c).
// Яркость — uniform _Level (0 днём, 1 ночью ставит риг). Без bloom осознанно.
Shader "Chimera/DomeFlat"
{
    Properties
    {
        _Level ("Level", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Off
        ZWrite On
        Fog { Mode Off }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Level;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                return float4(IN.color.rgb * _Level, 1.0);
            }
            ENDHLSL
        }
    }
}
