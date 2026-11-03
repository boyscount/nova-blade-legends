// Unlit vertex-coloured glow for slash arcs, rings, telegraph decals, sword trails and sparks.
// _SrcBlend/_DstBlend pick additive (One, One) or alpha blending (SrcAlpha, OneMinusSrcAlpha). _MainTex optional (white default).
Shader "NovaBlade/Glow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 1
        _ZTest ("ZTest", Float) = 4
        _UseVC ("Use Vertex Colour", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off Cull Off ZTest [_ZTest]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float4 _Color; float _UseVC;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
            V vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = TRANSFORM_TEX(i.uv, _MainTex); o.col = lerp(float4(1, 1, 1, 1), i.col, _UseVC) * _Color; return o; }
            half4 frag(V i) : SV_Target { half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.col; return half4(c.rgb * c.a, c.a); }
            ENDHLSL
        }
    }
}
