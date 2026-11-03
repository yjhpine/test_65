Shader "ActionPlatformer/Player Damage Screen"
{
    Properties
    {
        _Flash ("Flash", Range(0,1)) = 0
        _Vignette ("Vignette", Range(0,1)) = 0
        _Streak ("Impact streaks", Range(0,1)) = 0
        _Glitch ("Strong strike glitch", Range(0,1)) = 0
        _GlitchSeed ("Glitch pattern", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Overlay" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Flash, _Vignette, _Streak, _Glitch, _GlitchSeed;
            CBUFFER_END
            static const half3 Teal = half3(.475, .851, .875);
            static const half3 Red = half3(.847, .259, .333);
            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            // An overlay, not a resample of the scene: torn teal/red bands, flickering blocks, a split-channel
            // fringe at the sides and scan lines. A new seed every 1/30 s makes the bands jump.
            half4 Glitch(float2 uv, float pixelY, float strength, float seed)
            {
                half4 color = 0;
                // Thin, sparse rows: the tear reads at a glance without hiding the fight.
                const float rows = 48;
                float row = floor(uv.y * rows);
                if (Hash(float2(row, seed)) > 1 - .16 * strength)
                {
                    float start = Hash(float2(row, seed + 7.1)), span = .1 + .35 * Hash(float2(row, seed + 3.7));
                    float shift = (Hash(float2(row, seed + 1.3)) - .5) * .2;
                    float gap = abs(frac(uv.x + shift - start + .5) - .5);
                    if (gap < span * .5)
                    {
                        float inRow = frac(uv.y * rows);
                        float rim = inRow < .2 || inRow > .8 ? .3 : 0;
                        color = Hash(float2(row, seed + 5.9)) > .5 ? half4(Red, .12 + rim) : half4(Teal, .1 + rim);
                    }
                }
                float2 cell = floor(uv * float2(120, 68));
                if (Hash(cell + seed * 1.7) > 1 - .008 * strength)
                    color = half4(Hash(cell + seed * 2.3) > .5 ? Teal : Red, .4);
                if (color.a <= 0)
                {
                    float side = smoothstep(.85, 1, abs(uv.x * 2 - 1));
                    color = half4(uv.x < .5 ? Red : Teal, .16 * side);
                }
                if (color.a <= .001 && fmod(pixelY, 3) < 1) color = half4(0, 0, 0, .08);
                color.a *= strength;
                return color;
            }
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Clip-space coverage stays full-screen when the viewport or target texture changes.
                // The camera-local transform exists only for renderer visibility/culling.
                output.positionCS = float4(input.positionOS.xy, 0, 1);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float edge = smoothstep(.72, 1, max(abs(p.x), abs(p.y)));
                half4 vignette = half4(1, .35, .55, edge * min(_Vignette, .12));
                if (_Glitch <= 0) return vignette;
                half4 glitch = Glitch(input.uv, input.positionCS.y, saturate(_Glitch), _GlitchSeed);
                // The tear composites over the damage vignette.
                half alpha = glitch.a + vignette.a * (1 - glitch.a);
                half3 rgb = (glitch.rgb * glitch.a + vignette.rgb * vignette.a * (1 - glitch.a)) / max(alpha, .0001);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
