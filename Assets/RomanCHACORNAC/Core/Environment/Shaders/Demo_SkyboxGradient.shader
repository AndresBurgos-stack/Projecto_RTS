Shader "RomanCHACORNAC/Demo/Skybox Gradient"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.02, 0.04, 0.1, 1)
        _HorizonColor ("Horizon Color", Color) = (0.08, 0.14, 0.28, 1)
        _BottomColor ("Bottom Color", Color) = (0.03, 0.05, 0.12, 1)
        _TopExponent ("Top Exponent", Range(0.1, 8)) = 1.5
        _BottomExponent ("Bottom Exponent", Range(0.1, 8)) = 2
        _Intensity ("Intensity", Range(0, 4)) = 1
    }

    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half  _TopExponent;
                half  _BottomExponent;
                half  _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction  = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half y      = normalize(input.direction).y;
                half top    = pow(saturate(y), _TopExponent);
                half bottom = pow(saturate(-y), _BottomExponent);
                half3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, top);
                color       = lerp(color, _BottomColor.rgb, bottom);
                return half4(color * _Intensity, 1.0h);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half  _TopExponent;
                half  _BottomExponent;
                half  _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = UnityObjectToClipPos(v.vertex);
                o.direction  = v.vertex.xyz;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half y      = normalize(i.direction).y;
                half top    = pow(saturate(y), _TopExponent);
                half bottom = pow(saturate(-y), _BottomExponent);
                half3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, top);
                color       = lerp(color, _BottomColor.rgb, bottom);
                return half4(color * _Intensity, 1.0h);
            }
            ENDCG
        }
    }

    FallBack Off
}
