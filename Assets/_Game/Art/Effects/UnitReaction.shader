Shader "ActionPlatformer/Unit Reaction"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _HitFlash ("Hit brightness", Range(0,1)) = 0
        _OutlineWidth ("Outline pixels", Range(0,2)) = 0
        _OutlineColor ("Outline color", Color) = (1,.12,.25,1)
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _OutlineColor;
                float _HitFlash, _OutlineWidth;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float2 stepUV = _MainTex_TexelSize.xy * _OutlineWidth;
                half edge = max(max(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(stepUV.x,0)).a,
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(stepUV.x,0)).a),
                    max(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0,stepUV.y)).a,
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0,stepUV.y)).a));
                half outline = saturate(edge - tex.a) * step(.01, _OutlineWidth) * _OutlineColor.a;
                half alpha = max(tex.a, outline) * input.color.a;
                half3 fill = lerp(tex.rgb * input.color.rgb, half3(1,1,1), saturate(_HitFlash) * .8);
                half3 rgb = (fill * tex.a + _OutlineColor.rgb * outline * (1-tex.a)) / max(max(tex.a,outline), .0001);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
