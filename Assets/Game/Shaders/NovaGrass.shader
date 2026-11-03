// Grass blades drawn with GPU instancing (GrassField). Vertex colour rgb = blade colour, a = height along the blade (0 root .. 1 tip).
// Wind bends the tips (global _CozyWind.x = time); lit by the main light (no shadows) + _NovaAmbient set per stage, light through the blades, fog.
Shader "NovaBlade/Grass"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Variation ("Per-clump Variation", Range(0,1)) = 0.3
        _Bend ("Wind Bend (m)", Float) = 0.14
        _Translucency ("Translucency", Range(0,2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor; float _Variation, _Bend, _Translucency;
        CBUFFER_END
        float4 _CozyWind; float4 _NovaAmbient;
        float3 BendWS(float3 wp, float h)
        {
            float t = _CozyWind.x;
            float gust = sin(wp.x * 0.35 + wp.z * 0.22 - t * 1.6) * 0.6 + 0.4 + sin(wp.x * 1.7 + t * 3.1) * 0.15;
            float k = h * h * _Bend;
            return wp + float3(gust * k, -abs(gust) * k * 0.35, gust * k * 0.45);
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 pos : POSITION; float3 n : NORMAL; float4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 n : TEXCOORD1; float4 col : TEXCOORD2; float fog : TEXCOORD3; };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v);
                o.wp = BendWS(TransformObjectToWorld(v.pos.xyz), v.col.a);
                o.pos = TransformWorldToHClip(o.wp);
                o.n = normalize(lerp(TransformObjectToWorldNormal(v.n), float3(0, 1, 0), 0.55));
                float hsh = frac(sin(dot(float2(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m23), float2(12.9898, 78.233))) * 43758.5453);
                float3 tint = lerp(float3(0.85, 0.95, 0.75), float3(1.12, 1.08, 0.88), hsh);
                o.col = float4(v.col.rgb * lerp(1, tint, _Variation * 2) * _BaseColor.rgb, v.col.a);
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.n); if (!front) n = normalize(float3(-n.x, n.y, -n.z));
                // no shadow lookup: for instanced draws the shadow coords come back fully shadowed
                Light L = GetMainLight();
                float atten = 1;
                float ndl = saturate(dot(n, L.direction) * 0.6 + 0.4);
                float occ = lerp(0.5, 1, i.col.a);
                float3 Vd = GetWorldSpaceNormalizeViewDir(i.wp);
                float3 c = i.col.rgb * (L.color * ndl * atten + _NovaAmbient.rgb) * occ;
                c += i.col.rgb * L.color * pow(saturate(dot(-Vd, L.direction)), 3) * _Translucency * i.col.a * atten;
                return half4(MixFog(c, i.fog), 1);
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
            #pragma multi_compile_instancing
            struct A { float4 pos : POSITION; float4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(A v) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(v); return TransformWorldToHClip(BendWS(TransformObjectToWorld(v.pos.xyz), v.col.a)); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
