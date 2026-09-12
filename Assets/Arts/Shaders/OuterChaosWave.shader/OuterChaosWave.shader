Shader "Custom/OuterChaosWave"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.2, 0.2, 0.2, 1)
        _WaveSpeed("Wave Speed", Float) = 2.0
        _WaveFreq("Wave Frequency", Float) = 0.5
        _WaveAmp("Wave Amplitude", Float) = 1.0
        _BossPos("Boss Position", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _BaseColor;
            float _WaveSpeed;
            float _WaveFreq;
            float _WaveAmp;
            float4 _BossPos;

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 1. 获取世界空间坐标
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                // 2. 基于与 Boss 的距离计算波形
                float dist = distance(positionWS, _BossPos.xyz);
                float wave = sin(dist * _WaveFreq - _Time.y * _WaveSpeed) * _WaveAmp;
                
                // 3. 应用垂直偏移
                positionWS.y += wave;
                
                // 4. 转换回裁剪空间
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
