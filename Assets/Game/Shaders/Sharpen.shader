// Contrast-adaptive sharpening as the last step of the PC renderer's frame (the UI is drawn after it, so text is untouched).
// Sharpens more where the local contrast is low, so edges that are already crisp do not ring.
Shader "NovaBlade/Sharpen"
{
    Properties { _Amount ("Amount", Range(0, 1)) = .6 }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "Sharpen"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float _Amount;
            half3 Tap(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord, t = 1 / _ScreenParams.xy;
                half3 c = Tap(uv), n = Tap(uv + float2(0, -t.y)), s = Tap(uv + float2(0, t.y)), w = Tap(uv + float2(-t.x, 0)), e = Tap(uv + float2(t.x, 0));
                half3 lo = min(c, min(n, min(s, min(w, e)))), hi = max(c, max(n, max(s, max(w, e))));
                half3 amp = sqrt(saturate(min(lo, 1 - hi) / max(hi, .0001)));
                half3 k = amp * lerp(-.125, -.2, _Amount);
                // keep the alpha: the studio camera renders weapon and hero pictures through this renderer onto a transparent background
                half alpha = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).a;
                return half4(saturate((c + (n + s + w + e) * k) / (1 + 4 * k)), alpha);
            }
            ENDHLSL
        }
    }
}
