Shader "ActionPlatformer/Player Action Two Tone"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _PrimaryColor ("Teal", Color) = (0.47451,0.85098,0.87451,1)
        _AccentColor ("Red", Color) = (0.84706,0.25882,0.33333,1)
        _AccentBoundary ("Red boundary (local Y)", Range(-0.5,0.5)) = -0.12
        _AccentSlope ("Boundary diagonal", Range(-1,1)) = 0.3
        _Highlight ("White core strength", Range(0,1)) = 0.18
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex EffectVertex
            #pragma fragment EffectFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float2 effectPosition : TEXCOORD2;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _PrimaryColor, _AccentColor;
                float _AccentBoundary, _AccentSlope, _Highlight;
            CBUFFER_END

            Varyings EffectVertex(Attributes input)
            {
                // Local geometry keeps the palette fixed across sheet frames and mirrors with the sprite.
                float2 effectPosition = input.positionOS.xy;
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                output.effectPosition = effectPosition;
                return output;
            }

            half4 EffectFragment(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float diagonal = input.effectPosition.y + input.effectPosition.x * _AccentSlope;
                half accent = 1 - smoothstep(_AccentBoundary - 0.025, _AccentBoundary + 0.025, diagonal);
                half3 palette = lerp(_PrimaryColor.rgb, _AccentColor.rgb, accent);
                half brightness = max(source.r, max(source.g, source.b));
                palette = lerp(palette, half3(1,1,1), smoothstep(0.8,1,brightness) * _Highlight);
                // Retain the source shading, silhouette, and fade without adding opaque pixels.
                return half4(source.rgb * palette * input.color.rgb, source.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
