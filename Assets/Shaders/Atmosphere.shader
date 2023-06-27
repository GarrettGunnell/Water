Shader "Hidden/Atmosphere" {
    
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    SubShader {
        CGINCLUDE
        #include "UnityCG.cginc"
        #include "UnityStandardBRDF.cginc"

        sampler2D _MainTex, _DepthTexture;

        struct VertexData {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f {
            float2 uv : TEXCOORD0;
            float4 vertex : SV_POSITION;
        };

        v2f vp(VertexData v) {
            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            return o;
        }
        ENDCG

        Pass {
            CGPROGRAM
            #pragma vertex vp
            #pragma fragment fp

            float _FogDensity, _FogOffset;
            float3 _FogColor;

            float4x4 _CameraInvViewProjection;

            float3 ComputeWorldSpacePosition(float2 positionNDC, float deviceDepth) {
				float4 positionCS = float4(positionNDC * 2.0 - 1.0, deviceDepth, 1.0);
				float4 hpositionWS = mul(_CameraInvViewProjection, positionCS);
				return hpositionWS.xyz / hpositionWS.w;
			}

            float4 fp(v2f i) : SV_Target {
                float4 col = tex2D(_MainTex, i.uv);

                float depth = SAMPLE_DEPTH_TEXTURE(_DepthTexture, i.uv);
				float3 worldPos = ComputeWorldSpacePosition(i.uv, depth);

                float height = min(500.0f, worldPos.y) / 500.0f;
                height = pow(saturate(height), 1.0f / 1.2f);

                depth = Linear01Depth(depth);
                float viewDistance = depth * _ProjectionParams.z;
                
                float fogFactor = (_FogDensity / sqrt(log(2))) * max(0.0f, viewDistance - _FogOffset);
                fogFactor = exp2(-fogFactor * fogFactor);

                float3 sunDir = -normalize(float3(1.15f, 0.5f, 1.0f));
                float sun = pow(max(0.0f, dot(normalize(_WorldSpaceCameraPos - worldPos), sunDir)), 1000.0f) * 1.5f;

                float3 output = lerp(_FogColor, col.rgb, saturate(height + fogFactor));

                return float4(output + sun, 1.0f);
            }

            ENDCG
        }
    }
}