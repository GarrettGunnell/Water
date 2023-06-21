Shader "Custom/Water" {
		
		Properties {
			[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
		}

	SubShader {
		Tags {
			"RenderType" = "Opaque"
			"Queue" = "Transparent"
		}

		GrabPass { "_WaterBackground" }

		Pass {

			ZWrite On

			CGPROGRAM

			#pragma vertex vp
			#pragma fragment fp

			#pragma shader_feature USE_VERTEX_DISPLACEMENT
			#pragma shader_feature SINE_WAVE
			#pragma shader_feature STEEP_SINE_WAVE
			#pragma shader_feature GERSTNER_WAVE
			#pragma shader_feature NORMALS_IN_PIXEL_SHADER
			#pragma shader_feature CIRCULAR_WAVES

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

			#define PI 3.14159265358979323846

			float fastSine(float x) {
				return 1.0f;
			}

			float2 GetDirection(float3 v, Wave w) {
				#ifdef CIRCULAR_WAVES
                float2 p = float2(v.x, v.z);

                return normalize(p - w.origin);
				#else
				return w.direction;
				#endif
			}

			float GetWaveCoord(float3 v, float2 d, Wave w) {
				#ifdef CIRCULAR_WAVES
					float2 p = float2(v.x, v.z);
					return length(p - w.origin);
				#endif

				return v.x * d.x + v.z * d.y;
			}

			float GetTime(Wave w) {
				#ifdef CIRCULAR_WAVES
					return -_Time.y * w.phase;
				#else
				return _Time.y * w.phase;
				#endif
			}

			float Sine(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				return w.amplitude * sin(xz * w.frequency + t);
			}

			float3 SineNormal(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				float2 n = w.frequency * w.amplitude * d * cos(xz * w.frequency + t);

				return float3(n.x, n.y, 0.0f);
			}

			float SteepSine(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				return 2.0f * w.amplitude * pow((sin(xz * w.frequency + t) + 1.0f) / 2.0f, w.steepness);
			}

			float3 SteepSineNormal(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				float h = pow((sin(xz * w.frequency + t) + 1) / 2.0f, max(1.0f, w.steepness - 1));
				float2 n = d * w.steepness * w.frequency * w.amplitude * h * cos(xz * w.frequency + t);

				return float3(n.x, n.y, 0.0f);
			}

			float3 Gerstner(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				float3 g = float3(0.0f, 0.0f, 0.0f);
				g.x = w.steepness * w.amplitude * d.x * cos(w.frequency * xz + t);
				g.z = w.steepness * w.amplitude * d.y * cos(w.frequency * xz + t);
				g.y = w.amplitude * sin(w.frequency * xz + t);
				
				return g;
			}

			float3 GerstnerNormal(float3 v, Wave w) {
				float2 d = GetDirection(v, w);
				float xz = GetWaveCoord(v, d, w);
				float t = GetTime(w);

				float3 n = float3(0.0f, 0.0f, 0.0f);
				
				float wa = w.frequency * w.amplitude;
				float s = sin(w.frequency * xz + _Time.y * w.phase);
				float c = cos(w.frequency * xz + _Time.y * w.phase);

				n.x = d.x * wa * c;
				n.z = d.y * wa * c;
				n.y = w.steepness * wa * s;

				return n;
			}

			float3 CalculateOffset(float3 v, Wave w) {
				#ifdef SINE_WAVE
					return float3(0.0f, Sine(v, w), 0.0f);
				#endif

				#ifdef STEEP_SINE_WAVE
					return float3(0.0f, SteepSine(v, w), 0.0f);
				#endif

				#ifdef GERSTNER_WAVE
					return Gerstner(v, w);
				#endif

				return 0.0f;
			}

			float3 CalculateNormal(float3 v, Wave w) {
				#ifdef SINE_WAVE
					return SineNormal(v, w);
				#endif

				#ifdef STEEP_SINE_WAVE
					return SteepSineNormal(v, w);
				#endif

				#ifdef GERSTNER_WAVE
					return GerstnerNormal(v, w);
				#endif

				return 0.0f;
			}

			const int _WaveCount;

			float3 _Ambient, _DiffuseReflectance, _SpecularReflectance, _FresnelColor;
			float _Shininess, _FresnelBias, _FresnelStrength, _FresnelShininess;
			float _AbsorptionCoefficient;

			float4x4 _CameraInvViewProjection;
			sampler2D _CameraDepthTexture, _WaterBackground;
			float4 _WaterBackground_TexelSize;

			float hash(uint n) {
				// integer hash copied from Hugo Elias
				n = (n << 13U) ^ n;
				n = n * (n * n * 15731U + 0x789221U) + 0x1376312589U;
				return float(n & uint(0x7fffffffU)) / float(0x7fffffff);
			}

			
			#define CHOPPINESS 1232.34999345f;

			v2f vp(VertexData v) {
				v2f i;

				#ifdef USE_VERTEX_DISPLACEMENT
					i.worldPos = mul(unity_ObjectToWorld, v.vertex);

					float3 h = 0.0f;
					float3 n = 0.0f;

					for (int wi = 0; wi < _WaveCount; ++wi) {
						h += CalculateOffset(i.worldPos, _Waves[wi]);

						#ifndef GERSTNER_WAVE
							#ifndef NORMALS_IN_PIXEL_SHADER
								n += CalculateNormal(i.worldPos, _Waves[wi]);
							#endif
						#endif
					}
					/*
					float G = exp2(-1);
					float f = 0.5f;
					float a = 0.5f;
					float t = 0.0f;
					float timeMult = 1.0f;
					float iter = 0.0f;
					float3 p = i.worldPos;

					for (int wi = 0; wi < 8; ++wi) {
						float2 d = normalize(float2(cos(iter), sin(iter)));
						float x = dot(d, p.xz) * f + _Time.y * timeMult;
						t += a * exp(sin(x) - 1);
						f *= 2.0f;
						a *= G;
						timeMult *= 1.07;
						iter += CHOPPINESS;
					}

					h = float3(0.0f, t, 0.0f);
					*/
					float4 newPos = v.vertex + float4(h, 0.0f);
					i.worldPos = mul(unity_ObjectToWorld, newPos);
					i.pos = UnityObjectToClipPos(newPos);
					
					#ifndef NORMALS_IN_PIXEL_SHADER
					#ifdef GERSTNER_WAVE
						for (int wi = 0; wi < _WaveCount; ++wi) {
							n += CalculateNormal(i.worldPos, _Waves[wi]);
						}

                        i.normal = normalize(UnityObjectToWorldNormal(normalize(float3(-n.x, 1.0f - n.y, -n.z))));
					#else
						i.normal = normalize(UnityObjectToWorldNormal(normalize(float3(-n.x, 1.0f, -n.y))));
					#endif
					#else
						i.normal = 0.0;
					#endif
				#else
					i.worldPos = mul(unity_ObjectToWorld, v.vertex);
					i.normal = normalize(UnityObjectToWorldNormal(v.normal));
					i.pos = UnityObjectToClipPos(v.vertex);
				#endif

				return i;
			}

			float3 ComputeWorldSpacePosition(float2 positionNDC, float deviceDepth) {
				float4 positionCS = float4(positionNDC * 2.0 - 1.0, deviceDepth, 1.0);
				float4 hpositionWS = mul(_CameraInvViewProjection, positionCS);
				return hpositionWS.xyz / hpositionWS.w;
			}

			float4 fp(v2f i) : SV_TARGET {
                float3 lightDir = _WorldSpaceLightPos0;
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 halfwayDir = normalize(lightDir + viewDir);

				float3 normal = 0.0f;

				#ifdef NORMALS_IN_PIXEL_SHADER

				for (int wi = 0; wi < _WaveCount; ++wi) {
					normal += CalculateNormal(i.worldPos, _Waves[wi]);
				}
				/*
				float G = exp2(-1);
				float f = 1.0f;
				float a = 0.5f;
				float2 t = 0.0f;
				float timeMult = 1.0f;
				float iter = 0.0f;

				float3 p = i.worldPos;

				[unroll]
				for (int wi = 0; wi < 32; ++wi) {
					float2 d = normalize(float2(cos(iter), sin(iter)));
					float x = dot(d, p.xz) * f + _Time.y * timeMult;
					float wave = a * exp(sin(x) - 1);
					t += f * d * wave * cos(x);
					f *= 1.5f;
					timeMult *= 1.27;
					a *= G;
					iter += CHOPPINESS;
				}

				normal = float3(t.x, t.y, 0.0f);
				*/
				#ifdef GERSTNER_WAVE
					normal = normalize(UnityObjectToWorldNormal(normalize(float3(-normal.x, 1.0f - normal.y, -normal.z))));
				#else
					normal = normalize(UnityObjectToWorldNormal(normalize(float3(-normal.x, 1.0f, -normal.y))));
				#endif

				#else
					normal = normalize(i.normal);
				#endif

				float ndotl = DotClamped(lightDir, normal);

				float3 diffuseReflectance = _DiffuseReflectance / PI;
                float3 diffuse = _LightColor0.rgb * ndotl * diffuseReflectance;

				float3 specularReflectance = _SpecularReflectance;
				float spec = pow(DotClamped(normal, halfwayDir), _Shininess) * ndotl;
                float3 specular = _LightColor0.rgb * specularReflectance * spec;

				float3 I = normalize(i.worldPos - _WorldSpaceCameraPos);
				float R = _FresnelBias + _FresnelStrength * pow(1.0f + dot(I, normal), _FresnelShininess);

				float3 fresnel = _FresnelColor * R;

				float2 uv = i.pos.xy / _ScreenParams.xy;

				float4 backgroundColor = tex2D(_WaterBackground, uv);

				float3 depthPos = ComputeWorldSpacePosition(uv, (SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv)));

				float waterDepth = length(depthPos - i.worldPos);
				
				float3 beersLaw = exp(-waterDepth * _AbsorptionCoefficient);

				float4 albedo = float4(saturate(_Ambient + diffuse + specular + fresnel), saturate(R + spec));
				
                return float4(lerp(albedo.rgb, backgroundColor * (1 - albedo.a) + albedo.rgb, saturate(beersLaw - R - spec)), 1.0f);
			}

			ENDCG
		}
	}
}