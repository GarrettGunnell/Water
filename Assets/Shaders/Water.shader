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
			#pragma shader_feature SINE_WAVE
			#pragma shader_feature STEEP_SINE_WAVE
			#pragma shader_feature GERSTNER_WAVE

			#include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

			struct VertexData {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float3 normal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
			};

			struct Wave {
				float2 direction;
				float2 origin;
				float frequency;
				float amplitude;
				float phase;
				float steepness;
				int waveType;
			};
			
			StructuredBuffer<Wave> _Waves;

			//#define sin fastSine

			float fastSine(float x) {
				return 1.0f;
			}

			float Sine(float3 v, Wave w) {
				float2 d = w.direction;
				float xz = d.x * v.x + d.y * v.z;

				return w.amplitude * sin(xz * w.frequency + _Time.y * w.phase);
			}

			float3 SineNormal(float3 v, Wave w) {
				float2 d = w.direction;
				float xz = d.x * v.x + d.y * v.z;

				float2 n = w.frequency * w.amplitude * d * cos(xz * w.frequency + _Time.y * w.phase);

				return float3(n.x, n.y, 0.0f);
			}

			float SteepSine(float3 v, Wave w) {
				float2 d = w.direction;
				float xz = d.x * v.x + d.y * v.z;

				return 2.0f * w.amplitude * pow((sin(xz * w.frequency + _Time.y * w.phase) + 1.0f) / 2.0f, w.steepness);
			}

			float3 SteepSineNormal(float3 v, Wave w) {
				float2 d = w.direction;
				float xz = d.x * v.x + d.y * v.z;

				float h = pow((sin(xz * w.frequency + _Time.y * w.phase) + 1) / 2.0f, max(1.0f, w.steepness - 1));
				float2 n = d * w.steepness * w.frequency * w.amplitude * h * cos(xz * w.frequency + _Time.y * w.phase);

				return float3(n.x, n.y, 0.0f);
			}

			float3 Gerstner(float3 v, Wave w) {
				float2 d = w.direction;
				float xz = d.x * v.x + d.y * v.z;

				float3 g = float3(0.0f, 0.0f, 0.0f);
				g.x = w.steepness * w.amplitude * d.x * cos(w.frequency * xz + _Time.y * w.phase);
				g.z = w.steepness * w.amplitude * d.y * cos(w.frequency * xz + _Time.y * w.phase);
				g.y = w.amplitude * sin(w.frequency * xz + _Time.y * w.phase);
				
				return g;
			}

			float3 GerstnerNormal(float3 v, Wave w) {
				float2 d = w.direction;
				float xz = d.x * v.x + d.y * v.z;

				float3 n = float3(0.0f, 0.0f, 0.0f);
				
				float wa = w.frequency * w.amplitude;
				float s = sin(w.frequency * xz + _Time.y * w.phase);
				float c = cos(w.frequency * xz + _Time.y * w.phase);

				n.x = d.x * wa * c;
				n.z = d.y * wa * c;
				n.y = w.steepness * wa * s;

				return n;
			}

			v2f vp(VertexData v) {
				v2f i;

				#ifdef USE_VERTEX_DISPLACEMENT
					i.worldPos = mul(unity_ObjectToWorld, v.vertex);

					float3 h = 0.0f;
					float3 n = 0.0f;

					[unroll]
					for (int wi = 0; wi < 4; ++wi) {
						#ifdef SINE_WAVE
							h.y += Sine(i.worldPos, _Waves[wi]);
							n += SineNormal(i.worldPos, _Waves[wi]);
						#endif

						#ifdef STEEP_SINE_WAVE
							h.y += SteepSine(i.worldPos, _Waves[wi]);
							n += SteepSineNormal(i.worldPos, _Waves[wi]);
						#endif

						#ifdef GERSTNER_WAVE
							h += Gerstner(i.worldPos, _Waves[wi]);
						#endif
					}

					float4 newPos = v.vertex + float4(h, 0.0f);
					i.worldPos = mul(unity_ObjectToWorld, newPos);
					i.pos = UnityObjectToClipPos(newPos);
					
					#ifdef GERSTNER_WAVE
						[unroll]
						for (int wi = 0; wi < 4; ++wi) {
							n += GerstnerNormal(newPos, _Waves[wi]);
						}

                        i.normal = normalize(UnityObjectToWorldNormal(normalize(float3(-n.x, 1.0f - n.y, -n.z))));
					#else
						i.normal = normalize(UnityObjectToWorldNormal(normalize(float3(-n.x, 1.0f, -n.y))));
					#endif
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