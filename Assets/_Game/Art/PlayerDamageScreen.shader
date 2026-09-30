Shader "ActionPlatformer/Player Damage Screen"
{
    Properties
    {
        _Flash ("Flash", Range(0,1)) = 0
        _Vignette ("Vignette", Range(0,1)) = 0
        _Streak ("Impact streaks", Range(0,1)) = 0
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
                float _Flash, _Vignette, _Streak;
            CBUFFER_END
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
                float radius = length(p);
                float angle = atan2(p.y, p.x + .00001);
                float edge = smoothstep(.35,1.3,radius);
                float rays = pow(saturate(sin(angle*19 + cos(angle*7)*1.4)),38);
                float rayAlpha = rays * smoothstep(.25,.85,radius) * _Streak * .8;
                float redAlpha = edge * _Vignette;
                float alpha = redAlpha + rayAlpha * (1-redAlpha);
                float3 rgb = float3(.42,.012,.03)*redAlpha*(1-rayAlpha) + float3(1,.84,.76)*rayAlpha;
                rgb = rgb*(1-_Flash) + float3(1,.95,.88)*_Flash;
                alpha = alpha*(1-_Flash) + _Flash;
                return half4(rgb / max(alpha,.0001), alpha);
            }
            ENDHLSL
        }
    }
}
