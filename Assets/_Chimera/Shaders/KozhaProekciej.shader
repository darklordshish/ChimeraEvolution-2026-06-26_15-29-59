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
        _PatternHalfW ("Diamond half-width (fraction of body height)", Float) = 0.05
        _PatternPeriod ("Diamond period along spine", Float) = 0.11
        _PatternPhase ("Pattern phase along spine", Float) = 0
        _PatternRim ("Light rim width (in d)", Float) = 0.08
        _SpineAxis ("Spine axis (body space)", Vector) = (0,0,1,0)
        _PatternContrast ("Pattern contrast", Range(0,1)) = 0
        _SkinScale ("Tiles per body height", Float) = 28
        _ScaleContrast ("Fine scale contrast", Range(0,1)) = 0.3
        _DorsalAxis ("Dorsal axis (body space)", Vector) = (0,1,0,0)
        _DorsalEdge ("Dorsal falloff half-width (dot)", Float) = 0.35
        _PatternLimb ("Pattern factor on limbs", Float) = 1
        _HeadEdge ("Head-bone mask transition half-width (weight)", Float) = 0.07
        _HeadCut ("Head-bone mask threshold (weight)", Float) = 0.62
        _PatternWidth ("Diamond width factor", Float) = 1.25
        _PatternTop ("Chain top (fraction of body height)", Float) = 0.87
        _PatternBottom ("Chain bottom", Float) = 0.54
        _MaskEdge ("Mask edge half-width (fraction of height)", Float) = 0.007
        _TriPower ("Triplanar sharpness", Float) = 10
        _BodyHeight ("Body height, m", Float) = 1.85
        _SkinWeight ("Skin weight", Range(0,1)) = 1
        _SkinRelief ("Relief strength", Range(0,2)) = 0.6
        _MaskMin ("Mask min (fraction of body height)", Float) = -0.5
        _MaskMax ("Mask max (fraction of body height)", Float) = 10
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SkinHeight_TexelSize;
                float4 _SkinTint;
                float4 _SkinMean;
                float _SkinScale;
                float _PatternHalfW;
                float _PatternPeriod;
                float _PatternPhase;
                float _PatternRim;
                float4 _SpineAxis;
                float4 _DorsalAxis;
                float _DorsalEdge;
                float _PatternLimb;
                float _ScaleContrast;
                float _MaskEdge;
                float _HeadEdge;
                float _HeadCut;
                float _PatternWidth;
                float _PatternTop;
                float _PatternBottom;
                float _TriPower;
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
                float2 headW : TEXCOORD4;     // вес по кости головы
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 normalOS : TEXCOORD2;
                float3 restPos : TEXCOORD3;
                float3 restN : TEXCOORD4;
                float headW : TEXCOORD5;
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
                OUT.headW = IN.headW.x;
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

                // триплан: веса по |n|^_TriPower (резко, без двоящихся ромбов на боку), нормированные; координаты плитки — позиция покоя в долях тела
                float3 w = pow(abs(nRest), _TriPower);
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

                // МАСКА КОЖИ ПО КОСТИ ГОЛОВЫ (критик 10.11): вес вершины по поддереву кости головы, граница идёт под подбородком и по
                // основанию черепа, а не горизонтальной «шапочкой» на высоте носа. Маска по высоте осталась опцией (_MaskMin/_MaskMax)
                float hFrac = IN.restPos.y / max(_BodyHeight, 1e-3);
                float mask = (1.0 - smoothstep(_HeadCut - _HeadEdge, _HeadCut + _HeadEdge, IN.headW))
                           * smoothstep(_MaskMin - _MaskEdge, _MaskMin + _MaskEdge, hFrac)
                           * (1.0 - smoothstep(_MaskMax - _MaskEdge, _MaskMax + _MaskEdge, hFrac));
                float weight = saturate(_SkinWeight) * mask;

                // КРУПНЫЙ УЗОР вида (сёдла змеи) — АНАЛИТИЧЕСКИ в координатах позы покоя, а не плиткой: одна цепь ромбов вдоль хребта.
                // along — вдоль оси хребта шасси, lateral — поперёк (тело симметрично по X); всё в долях роста тела. Ромб: d < 1, край
                // ступенчатый (координаты квантуются), вокруг тонкая светлая кайма. Вес — дорсальный: спина полный, бока ~половина,
                // грудь и живот 0; на конечностях узора нет (_PatternLimb 0). Нет узора у вида — _PatternContrast 0
                float3 pr = IN.restPos / max(_BodyHeight, 1e-3);
                float along = dot(pr, normalize(_SpineAxis.xyz));
                float lateral = abs(pr.x);
                const float Q = 11.0;   // ступеней на полуширину ромба и на период
                float hw = max(_PatternHalfW * _PatternWidth, 1e-4);   // поперечная полуось шире продольной (ромбы шире высоты)
                float fr = frac(along / max(_PatternPeriod, 1e-4) + _PatternPhase);
                float frQ = floor(fr * Q + 0.5) / Q;
                float latQ = floor(lateral / hw * Q + 0.5) / Q;
                float d = latQ + abs(frQ - 0.5) * 2.0;                        // квантованный: ступени только на внешнем крае тёмного поля
                float dRaw = lateral / hw + abs(fr - 0.5) * 2.0;             // нерезаный: по нему кайма — сплошная, ровными гранями
                // цепь — от загривка до крестца, края гаснут остриём (фаза подогнана под низ, верх — целое число периодов)
                float inChain = step(_PatternBottom, along) * step(along, _PatternTop);
                // вес по нормали плавный (как в r4): спина полный, бока ~половина, грудь и живот 0; цепь обрезана по along
                float dorsal = smoothstep(-_DorsalEdge, _DorsalEdge, dot(nRest, normalize(_DorsalAxis.xyz)));
                float o = _PatternContrast * dorsal * _PatternLimb * inChain;
                // тёмный ромб чуть теплее и темнее основы, тонкая светлая кайма только по его контуру; поле вне ромбов — основа
                float3 warm = float3(1.0, 0.94, 0.84);
                float3 patLayer = d < 1.0 ? lerp(float3(1, 1, 1), warm, o) * (1.0 - 0.6 * o)
                               : dRaw < 1.0 + _PatternRim ? float3(1, 1, 1) * (1.0 + 1.4 * o)
                               : float3(1, 1, 1);
                float3 scaleLayer = lerp(float3(1, 1, 1), tile / max(_SkinMean.rgb, 1e-3), _ScaleContrast);

                float3 albedo = _BaseColor.rgb * lerp(float3(1, 1, 1), _SkinTint.rgb * scaleLayer * patLayer, weight);   // плитка нормирована на среднее: даёт контраст швов, не яркость; тинт — цвет вида-донора
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
