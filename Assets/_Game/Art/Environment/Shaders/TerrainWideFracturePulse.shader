Shader "ActionPlatformer/Environment/Terrain Digital Glitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Period ("Glitch interval (seconds)", Range(0.5,6)) = 3.0
        _Displacement ("Horizontal tear (world units)", Range(0,2)) = 0.26
        _BandHeight ("Horizontal block height (world units)", Range(0.04,2)) = 0.16
        _BlockWidth ("Wide corruption blocks (world units)", Range(1,16)) = 4.8
        _ColorSplit ("RGB offset (world units)", Range(0,0.3)) = 0.04
        _Corruption ("Digital noise strength", Range(0,1)) = 0.4
        _GlitchTint ("Muted signal color", Color) = (0.18,0.38,0.36,1)
        _ErrorTint ("Muted error color", Color) = (0.38,0.11,0.15,1)
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _PreviewTime ("Preview time (-1 = game clock)", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma target 3.0
            #pragma vertex GlitchVertex
            #pragma fragment GlitchFragment
            #pragma multi_compile_instancing
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float2 world : TEXCOORD1;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _GlitchTint, _ErrorTint;
                float _Period, _PreviewTime, _Displacement, _BandHeight, _BlockWidth, _ColorSplit, _Corruption;
            CBUFFER_END
            float Hash21(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }
            Varyings GlitchVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.world = TransformObjectToWorld(input.positionOS).xy;
                return o;
            }
            half4 ReadSprite(float2 uv)
            {
                // Current environment assets use single, full-texture sprites. Clamp shifted reads at their edges.
                float2 inset = _MainTex_TexelSize.xy * 0.5;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, clamp(uv, inset, 1.0-inset));
            }
            half4 GlitchFragment(Varyings input) : SV_Target
            {
                float t = max(0.0, _PreviewTime >= 0.0 ? _PreviewTime : _Time.y);
                float period = max(_Period, 0.5);
                float cycle = floor(t / period);
                float phase = t - cycle * period;
                // Keep a short, abrupt 0.48-second burst; repeat interval defaults to 3 seconds.
                float burst = (step(0.04,phase)-step(0.15,phase))
                            + (step(0.20,phase)-step(0.33,phase))
                            + (step(0.38,phase)-step(0.48,phase));
                float tick = floor(t * 24.0);
                float2 world = input.world;
                float row = floor((world.y + Hash21(float2(cycle,3.0))*2.0) / max(_BandHeight,0.04));
                float column = floor((world.x + Hash21(float2(cycle,7.0))*_BlockWidth) / max(_BlockWidth,1.0));
                float rowRandom = Hash21(float2(row, tick));
                float blockRandom = Hash21(float2(column + tick*3.0, row + cycle*17.0));
                float mask = burst * step(0.24, rowRandom) * step(0.18, blockRandom);
                float shift = (Hash21(float2(row + 11.0,tick)) * 2.0 - 1.0) * _Displacement * mask;
                // Convert a world-space horizontal displacement into texture UV space without changing geometry.
                float2 dx = ddx(world), dy = ddy(world);
                float determinant = dx.x*dy.y - dx.y*dy.x;
                float invDet = abs(determinant)>0.00000001 ? rcp(determinant) : 0.0;
                float2 uvPerWorldX = (ddx(input.uv)*dy.y - ddy(input.uv)*dx.y)*invDet;
                float2 offset = uvPerWorldX * shift;
                float2 split = uvPerWorldX * _ColorSplit * mask;
                half4 original = ReadSprite(input.uv) * input.color;
                half4 displaced = ReadSprite(input.uv + offset);
                half3 separated = half3(ReadSprite(input.uv + offset + split).r,
                                       displaced.g,
                                       ReadSprite(input.uv + offset - split).b) * input.color.rgb;
                half3 color = lerp(original.rgb, separated, mask);
                // Sparse horizontal data fragments cover multi-tile regions, with 24 Hz sample-and-hold noise.
                float2 bitGrid = floor(world * float2(18.0, 32.0));
                float bits = step(0.52, Hash21(bitGrid + tick*7.1));
                float strip = step(0.72, Hash21(float2(row + 43.0, tick)));
                float blockLoss = step(0.82, blockRandom);
                color *= 1.0 - mask * blockLoss * 0.55;
                half3 corruptColor = lerp(_GlitchTint.rgb, _ErrorTint.rgb, step(0.78,blockRandom));
                float fragmentMask = mask * strip * (0.35 + 0.65*bits) * _Corruption;
                color = lerp(color, corruptColor * input.color.rgb, fragmentMask);
                // Very fine dark static on disrupted areas, not a fullscreen flash.
                color *= 1.0 - mask * (1.0-bits) * 0.13;
                return half4(color, original.a);
            }
            ENDHLSL
        }
    }
}
