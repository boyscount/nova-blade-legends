// A 360° photo (equirectangular) projected back onto the world from the point it was taken: the ground disc shows the
// paving under the camera, the surrounding wall shows the buildings and the sky. So the photo keeps its depth as the game
// camera moves. Unlit (the photo carries its own light), darkened where the main light's shadow falls so characters sit
// on the ground. _Center = where the photo was taken (world), _Rotate turns the panorama (0..1 = 0..360°).
Shader "NovaBlade/PanoProject"
{
    Properties
    {
        _MainTex ("Equirectangular", 2D) = "white" {}
        _Center ("Projection Centre", Vector) = (0, 6, 0, 0)
        _Rotate ("Rotate (turns)", Float) = 0.5
        _Exposure ("Exposure", Float) = 1
        _ShadowDark ("Shadow Strength", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; float4 _Center; float _Rotate, _Exposure, _ShadowDark;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; };
            V vert(A v) { V o; o.wp = TransformObjectToWorld(v.pos.xyz); o.pos = TransformWorldToHClip(o.wp); return o; }
            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.wp - _Center.xyz);
                float u = atan2(d.x, d.z) * (0.5 / PI) + 0.5 + _Rotate, v = asin(clamp(d.y, -1, 1)) / PI + 0.5;
                // the longitude wraps once around: take derivatives from whichever of two offset copies is continuous here,
                // so the mip level does not jump to the smallest at the seam (a thin line otherwise)
                float2 uvA = float2(frac(u), v), uvB = float2(frac(u + 0.5) - 0.5, v);
                float2 dxA = ddx(uvA), dyA = ddy(uvA), dxB = ddx(uvB), dyB = ddy(uvB);
                bool useB = dot(dxB, dxB) + dot(dyB, dyB) < dot(dxA, dxA) + dot(dyA, dyA);
                half3 c = SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, uvA, useB ? dxB : dxA, useB ? dyB : dyA).rgb * _Exposure;
                float sh = MainLightRealtimeShadow(TransformWorldToShadowCoord(i.wp));
                c *= lerp(1 - _ShadowDark, 1, sh);
                return half4(c, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos : POSITION; };
            float4 vert(A v) : SV_POSITION { return TransformObjectToHClip(v.pos.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
