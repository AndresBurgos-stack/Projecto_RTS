Shader "RomanCHACORNAC/Demo/Environment"
{
    Properties
    {
        [Header(Surface)]
        _MainTex ("Texture", 2D) = "white" {}
        _TextureBlend ("Texture Blend", Range(0, 1)) = 1
        _MultiplyColor ("Multiply Color (A = Strength)", Color) = (1, 1, 1, 0)
    }

    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _MultiplyColor;
            half  _TextureBlend;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor  = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half3 Albedo(float3 positionWS, half3 normalWS)
            {
                half3  axis  = abs(normalWS);
                float2 plane = axis.y >= axis.x && axis.y >= axis.z ? positionWS.xz : (axis.z >= axis.x ? positionWS.xy : positionWS.zy);
                half3  color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, plane * _MainTex_ST.xy + _MainTex_ST.zw).rgb;
                return lerp(1.0h, color, _TextureBlend) * lerp(1.0h, _MultiplyColor.rgb, _MultiplyColor.a);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 albedo   = Albedo(input.positionWS, normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS              = input.positionWS;
                inputData.normalWS                = normalWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowCoord             = TransformWorldToShadowCoord(input.positionWS);

                half4 shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
                Light mainLight  = GetMainLight(inputData.shadowCoord);
                half  ndl        = saturate(dot(normalWS, mainLight.direction)) * 0.5h + 0.5h;
                half3 lighting   = mainLight.color * (ndl * mainLight.shadowAttenuation) + SampleSH(normalWS);

            #if defined(_ADDITIONAL_LIGHTS)
            #if USE_CLUSTER_LIGHT_LOOP || USE_FORWARD_PLUS
                [loop] for (uint directionalIndex = 0; directionalIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); directionalIndex++)
                {
                    Light light = GetAdditionalLight(directionalIndex, inputData.positionWS, shadowMask);
                    lighting += light.color * (saturate(dot(normalWS, light.direction)) * light.distanceAttenuation * light.shadowAttenuation);
                }
            #endif
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    lighting += light.color * (saturate(dot(normalWS, light.direction)) * light.distanceAttenuation * light.shadowAttenuation);
                LIGHT_LOOP_END
            #endif

                return half4(MixFog(albedo * lighting, input.fogFactor), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "UnityLightingCommon.cginc"
        #include "AutoLight.cginc"

        sampler2D _MainTex;
        float4 _MainTex_ST;
        half4 _MultiplyColor;
        half  _TextureBlend;

        struct Attributes
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 pos      : SV_POSITION;
            float3 worldPos : TEXCOORD0;
            half3  normalWS : TEXCOORD1;
            UNITY_LIGHTING_COORDS(2, 3)
            UNITY_FOG_COORDS(4)
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            UNITY_INITIALIZE_OUTPUT(Varyings, o);
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            o.pos      = UnityObjectToClipPos(v.vertex);
            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.normalWS = UnityObjectToWorldNormal(v.normal);
            UNITY_TRANSFER_LIGHTING(o, float2(0, 0));
            UNITY_TRANSFER_FOG(o, o.pos);
            return o;
        }

        half3 Albedo(float3 positionWS, half3 normalWS)
        {
            half3  axis  = abs(normalWS);
            float2 plane = axis.y >= axis.x && axis.y >= axis.z ? positionWS.xz : (axis.z >= axis.x ? positionWS.xy : positionWS.zy);
            half3  color = tex2D(_MainTex, plane * _MainTex_ST.xy + _MainTex_ST.zw).rgb;
            return lerp(1.0h, color, _TextureBlend) * lerp(1.0h, _MultiplyColor.rgb, _MultiplyColor.a);
        }
        ENDCG

        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragBase
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            half4 FragBase(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half3 normalWS = normalize(i.normalWS);
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
                half  ndl      = saturate(dot(normalWS, _WorldSpaceLightPos0.xyz)) * 0.5h + 0.5h;
                half3 lighting = _LightColor0.rgb * (ndl * attenuation) + ShadeSH9(half4(normalWS, 1.0h));
                half4 color    = half4(Albedo(i.worldPos, normalWS) * lighting, 1.0h);
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }

        Pass
        {
            Name "ForwardAdd"
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragAdd
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            half4 FragAdd(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half3 normalWS = normalize(i.normalWS);
                half3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
                half4 color = half4(Albedo(i.worldPos, normalWS) * _LightColor0.rgb * (saturate(dot(normalWS, lightDir)) * attenuation), 1.0h);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, color, half4(0, 0, 0, 0));
                return color;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
