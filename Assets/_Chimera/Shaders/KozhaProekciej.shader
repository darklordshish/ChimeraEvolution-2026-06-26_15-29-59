// КОЖА ОРГАНА ПРОЕКЦИЕЙ (спека 2026-11-10-kozha-organa-proekciej.md): одна бесшовная плитка вида-донора
// (цвет + карта высоты) проецируется на тело шасси с трёх осей по ПОЗЕ ПОКОЯ, записанной в меш при посадке
// (uv2 = позиция покоя в системе тела, uv3 = нормаль покоя). Покой, а не мир: узор не плывёт при движении
// костей и одинаково ложится на любое шасси без развёртки под каждое. Масштаб узора — в долях ТЕЛА шасси
// (_SkinScale плиток на _BodyHeight), поэтому на крупном шасси чешуя крупнее.
// Окраска по составу и эмоциям (TintMixer) — множитель _BaseColor поверх плитки. _SkinWeight и _BodyHeight
// ставятся блоком свойств (SRP Batcher у кожи выключен осознанно — спека §4).
Shader "Chimera/KozhaProekciej"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _SkinTex ("Skin Color Tile (gray)", 2D) = "gray" {}
        _SkinHeight ("Skin Height Tile", 2D) = "gray" {}
        _SkinTint ("Donor tint", Color) = (1,1,1,1)
        _SkinMean ("Tile mean (linear)", Color) = (1,1,1,1)
        _PatternTex ("Large pattern tile (gray)", 2D) = "white" {}
        _PatternScale ("Pattern tiles per body height", Float) = 7
        _PatternContrast ("Pattern contrast", Range(0,1)) = 0
        _SkinScale ("Tiles per body height", Float) = 8
        _BodyHeight ("Body height, m", Float) = 1.85
        _SkinWeight ("Skin weight", Range(0,1)) = 1
        _SkinRelief ("Relief strength", Range(0,2)) = 0.6
        _MaskMin ("Mask min (fraction of body height)", Float) = 0.0
        _MaskMax ("Mask max (fraction of body height)", Float) = 0.88
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_SkinTex);    SAMPLER(sampler_SkinTex);
            TEXTURE2D(_SkinHeight); SAMPLER(sampler_SkinHeight);
            TEXTURE2D(_PatternTex); SAMPLER(sampler_PatternTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SkinHeight_TexelSize;
                float4 _SkinTint;
                float4 _SkinMean;
                float _SkinScale;
                float _PatternScale;
                float _PatternContrast;
                float _BodyHeight;
                float _SkinWeight;
                float _SkinRelief;
                float _MaskMin;
                float _MaskMax;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 restPos : TEXCOORD2;   // поза покоя, система тела
                float3 restN : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 normalOS : TEXCOORD2;
                float3 restPos : TEXCOORD3;
                float3 restN : TEXCOORD4;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.normalOS = IN.normalOS;
                OUT.restPos = IN.restPos;
                OUT.restN = IN.restN;
                return OUT;
            }

            // поворот вектора v тем же кратчайшим поворотом, что переводит единичную a в единичную b
            float3 RotateTo(float3 v, float3 a, float3 b)
            {
                float c = dot(a, b);
                float3 k = cross(a, b);
                return v * c + cross(k, v) + k * (dot(k, v) / max(1.0 + c, 1e-3));
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float3 nW = normalize(IN.normalWS);
                float3 nRest = normalize(IN.restN);

                // триплан: веса по |n|^4, нормированные; координаты плитки — позиция покоя в долях тела
                float3 w = pow(abs(nRest), 4.0);
                w /= max(w.x + w.y + w.z, 1e-5);
                float3 p = IN.restPos / max(_BodyHeight, 1e-3) * _SkinScale;
                float2 uvX = p.zy, uvY = p.xz, uvZ = p.xy;

                float3 tile = SAMPLE_TEXTURE2D(_SkinTex, sampler_SkinTex, uvX).rgb * w.x
                            + SAMPLE_TEXTURE2D(_SkinTex, sampler_SkinTex, uvY).rgb * w.y
                            + SAMPLE_TEXTURE2D(_SkinTex, sampler_SkinTex, uvZ).rgb * w.z;

                // рельеф: градиент высоты конечными разностями по двум соседним выборкам на каждую проекцию
                float2 e = _SkinHeight_TexelSize.xy;
                float2 gX, gY, gZ;
                {
                    float h0 = SAMPLE_TEXTURE2D(_SkinHeight, sampler_SkinHeight, uvX).r;
                    gX = float2(SAMPLE_TEXTURE2D(_SkinHeight, sampler_SkinHeight, uvX + float2(e.x, 0)).r - h0,
                                SAMPLE_TEXTURE2D(_SkinHeight, sampler_SkinHeight, uvX + float2(0, e.y)).r - h0) / e;
                    h0 = SAMPLE_TEXTURE2D(_SkinHeight, sampler_SkinHeight, uvY).r;
                    gY = float2(SAMPLE_TEXTURE2D(_SkinHeight, sampler_SkinHeight, uvY + float2(e.x, 0)).r - h0,
                                SAMPLE_TEXTURE2D(_SkinHeight, sampler_SkinHeight, uvY + float2(0, e.y)).r - h0) / e;
                    h0 = SAMPLE_TEXTURE2D(_SkinHeight, sampler_SkinHeight, uvZ).r;
                    gZ = float2(SAMPLE_TEXTURE2D(_SkinHeight, sampler_SkinHeight, uvZ + float2(e.x, 0)).r - h0,
                                SAMPLE_TEXTURE2D(_SkinHeight, sampler_SkinHeight, uvZ + float2(0, e.y)).r - h0) / e;
                }
                // градиент в осях покоя: проекция X лежит в (z,y), Y — в (x,z), Z — в (x,y)
                float3 gRest = float3(0, gX.y, gX.x) * w.x + float3(gY.x, 0, gY.y) * w.y + float3(gZ.x, gZ.y, 0) * w.z;
                gRest -= nRest * dot(gRest, nRest);                           // только касательная часть
                // из осей покоя в мир: поворот покоя к нынешней нормали, затем матрица объекта
                float3 gW = TransformObjectToWorldDir(RotateTo(gRest, nRest, normalize(IN.normalOS)), false);
                float3 nBump = normalize(nW - _SkinRelief * 0.25 * gW);

                // маска: доля высоты тела в позе покоя, на краях smoothstep 0.03
                float hFrac = IN.restPos.y / max(_BodyHeight, 1e-3);
                float mask = smoothstep(_MaskMin - 0.03, _MaskMin + 0.03, hFrac)
                           * (1.0 - smoothstep(_MaskMax - 0.03, _MaskMax + 0.03, hFrac));
                float weight = saturate(_SkinWeight) * mask;

                // КРУПНЫЙ УЗОР вида (сёдла змеи): тот же триплан по позе покоя, своя плотность в долях тела. Нормирован на среднее
                // (самый грубый мип = среднее плитки), поэтому яркость не падает; нет плитки — контраст 0, слоя нет
                float3 pp = IN.restPos / max(_BodyHeight, 1e-3) * _PatternScale;
                float pat = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, pp.zy).r * w.x
                          + SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, pp.xz).r * w.y
                          + SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, pp.xy).r * w.z;
                float patMean = SAMPLE_TEXTURE2D_LOD(_PatternTex, sampler_PatternTex, float2(0.5, 0.5), 11).r;
                float patLayer = lerp(1.0, pat / max(patMean, 1e-3), _PatternContrast);

                float3 albedo = _BaseColor.rgb * lerp(float3(1, 1, 1), _SkinTint.rgb * tile / max(_SkinMean.rgb, 1e-3) * patLayer, weight);   // плитка нормирована на среднее: даёт контраст швов, не яркость; тинт — цвет вида-донора
                float3 n = normalize(lerp(nW, nBump, weight));

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light l = GetMainLight(shadowCoord);
                float3 lit = l.color * (l.distanceAttenuation * l.shadowAttenuation * saturate(dot(n, l.direction)))
                           + SampleSH(n);
                return float4(albedo * lit, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
