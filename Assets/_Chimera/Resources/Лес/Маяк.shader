// Фонарь маяка над лабораторией (17.11): ровный цвет без света и БЕЗ ТУМАНА — фонарь читается из дальнего угла
// купола и в густой туман. Туман URP подмешивается в самом шейдере, здесь его нет намеренно: сторож
// `LabBeaconTests` проверяет по тексту шейдера, что туман в нём не подмешивается.
// Лежит в Resources — `Shader.Find` в сборке без ссылки на шейдер его бы не нашёл.
Shader "Chimera/Beacon"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.7, 0.35, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Back
        ZWrite On

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                return float4(_Color.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
