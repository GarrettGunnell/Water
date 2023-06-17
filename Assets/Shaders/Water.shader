Shader "Custom/Water" {
	SubShader {
		Pass {
			Tags {
				"RenderType" = "Opaque"
                "LightMode" = "ForwardBase"
			}

			CGPROGRAM

			#pragma vertex vp
			#pragma fragment fp

			#pragma shader_feature USE_VERTEX_DISPLACEMENT

			#include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

			float2 _Direction;
			float _Frequency, _Amplitude, _Phase;

			struct VertexData {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float3 normal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
			};

			float Sine(float3 v, float2 d) {
			float xz = d.x * v.x + d.y * v.z;
				return sin(_Frequency * xz + _Time.y * _Phase) * _Amplitude;
			}

			float3 SineNormal(float3 v, float2 d) {
				float xz = d.x * v.x + d.y * v.z;
				float2 n = _Frequency * _Amplitude * d * cos(xz *_Frequency + _Time.y * _Phase);

				return float3(n.x, n.y, 0.0f);
			}

			v2f vp(VertexData v) {
				v2f i;

				#ifdef USE_VERTEX_DISPLACEMENT
					i.worldPos = mul(unity_ObjectToWorld, v.vertex);

					float h = Sine(i.worldPos, _Direction);
					i.pos = UnityObjectToClipPos(v.vertex + float4(0.0f, h, 0.0f, 0.0f));

					float3 n = SineNormal(i.worldPos, _Direction);
					i.normal = normalize(UnityObjectToWorldNormal(float3(-n.x, 1.0f, -n.y)));
				#else
					i.worldPos = mul(unity_ObjectToWorld, v.vertex);
					i.normal = normalize(UnityObjectToWorldNormal(v.normal));
					i.pos = UnityObjectToClipPos(v.vertex);
				#endif

				return i;
			}

			float4 fp(v2f i) : SV_TARGET {
                float3 lightDir = _WorldSpaceLightPos0;
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 halfwayDir = normalize(lightDir + viewDir);

                float3 ambient = float3(0.0f, 0.0f, 0.1f);
                float3 diffuse = _LightColor0.rgb * DotClamped(lightDir, normalize(i.normal)) * 0.5f + 0.5f;
				diffuse *= diffuse;
                float specular = _LightColor0.rgb * pow(DotClamped(i.normal, halfwayDir), 50.0f);


                return float4(saturate(ambient + diffuse + specular), 1.0f);
			}

			ENDCG
		}
	}
}