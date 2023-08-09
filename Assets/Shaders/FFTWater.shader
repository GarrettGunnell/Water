Shader "Custom/FFTWater" {
		
		Properties {
			[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
		}

	CGINCLUDE
        #define _TessellationEdgeLength 10

        struct TessellationFactors {
            float edge[3] : SV_TESSFACTOR;
            float inside : SV_INSIDETESSFACTOR;
        };

        float TessellationHeuristic(float3 cp0, float3 cp1) {
            float edgeLength = distance(cp0, cp1);
            float3 edgeCenter = (cp0 + cp1) * 0.5;
            float viewDistance = distance(edgeCenter, _WorldSpaceCameraPos);

            return edgeLength * _ScreenParams.y / (_TessellationEdgeLength * (pow(viewDistance * 0.5f, 1.2f)));
        }
		
        bool TriangleIsBelowClipPlane(float3 p0, float3 p1, float3 p2, int planeIndex, float bias) {
            float4 plane = unity_CameraWorldClipPlanes[planeIndex];

            return dot(float4(p0, 1), plane) < bias && dot(float4(p1, 1), plane) < bias && dot(float4(p2, 1), plane) < bias;
        }

        bool cullTriangle(float3 p0, float3 p1, float3 p2, float bias) {
            return TriangleIsBelowClipPlane(p0, p1, p2, 0, bias) ||
                   TriangleIsBelowClipPlane(p0, p1, p2, 1, bias) ||
                   TriangleIsBelowClipPlane(p0, p1, p2, 2, bias) ||
                   TriangleIsBelowClipPlane(p0, p1, p2, 3, bias);
        }
    ENDCG

	SubShader {
		Tags {
			"LightMode" = "ForwardBase"
		}

		Pass {

			CGPROGRAM



			#pragma vertex dummyvp
			#pragma hull hp
			#pragma domain dp 
			#pragma geometry gp
			#pragma fragment fp


			#include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

			struct TessellationControlPoint {
                float4 vertex : INTERNALTESSPOS;
                float2 uv : TEXCOORD0;
            };

			struct VertexData {
				float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
			};

			struct v2g {
				float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
			};


			#define PI 3.14159265358979323846

			float hash(uint n) {
				// integer hash copied from Hugo Elias
				n = (n << 13U) ^ n;
				n = n * (n * n * 15731U + 0x789221U) + 0x1376312589U;
				return float(n & uint(0x7fffffffU)) / float(0x7fffffff);
			}

			float3 _SunDirection, _SunColor;

			float _NormalStrength, _FresnelNormalStrength, _SpecularNormalStrength;

			samplerCUBE _EnvironmentMap;
			int _UseEnvironmentMap;

			float3 _Ambient, _DiffuseReflectance, _SpecularReflectance, _FresnelColor, _TipColor;
			float _Shininess, _FresnelBias, _FresnelStrength, _FresnelShininess, _TipAttenuation;

			float4x4 _CameraInvViewProjection;
			sampler2D _CameraDepthTexture;
            Texture2D _HeightTex, _SpectrumTex, _NormalTex, _FoamTex;
            SamplerState point_repeat_sampler, linear_repeat_sampler;

            float _Tile;

			TessellationControlPoint dummyvp(VertexData v) {
				TessellationControlPoint p;
				p.vertex = v.vertex;
				p.uv = v.uv;

				return p;
			}

			v2g vp(VertexData v) {
				v2g g;
                g.worldPos = mul(unity_ObjectToWorld, v.vertex);
				float3 displacement = _HeightTex.SampleLevel(linear_repeat_sampler, v.uv * _Tile, 0).rgb;

				v.vertex.xyz += mul(unity_WorldToObject, displacement.xyz);
                g.pos = UnityObjectToClipPos(v.vertex);
                g.uv = v.uv;
				
				return g;
			}

			struct g2f {
				v2g data;
				float2 barycentricCoordinates : TEXCOORD9;
			};

			TessellationFactors PatchFunction(InputPatch<TessellationControlPoint, 3> patch) {
                float3 p0 = mul(unity_ObjectToWorld, patch[0].vertex);
                float3 p1 = mul(unity_ObjectToWorld, patch[1].vertex);
                float3 p2 = mul(unity_ObjectToWorld, patch[2].vertex);

                TessellationFactors f;
                float bias = -0.5 * 20;
                if (cullTriangle(p0, p1, p2, bias)) {
                    f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 0;
                } else {
                    f.edge[0] = TessellationHeuristic(p1, p2);
                    f.edge[1] = TessellationHeuristic(p2, p0);
                    f.edge[2] = TessellationHeuristic(p0, p1);
                    f.inside = (TessellationHeuristic(p1, p2) +
                                TessellationHeuristic(p2, p0) +
                                TessellationHeuristic(p1, p2)) * (1 / 3.0);
                }
                return f;
            }

            [UNITY_domain("tri")]
            [UNITY_outputcontrolpoints(3)]
            [UNITY_outputtopology("triangle_cw")]
            [UNITY_partitioning("integer")]
            [UNITY_patchconstantfunc("PatchFunction")]
            TessellationControlPoint hp(InputPatch<TessellationControlPoint, 3> patch, uint id : SV_OUTPUTCONTROLPOINTID) {
                return patch[id];
            }

            [maxvertexcount(3)]
            void gp(triangle v2g g[3], inout TriangleStream<g2f> stream) {
                g2f g0, g1, g2;
                g0.data = g[0];
                g1.data = g[1];
                g2.data = g[2];

                g0.barycentricCoordinates = float2(1, 0);
                g1.barycentricCoordinates = float2(0, 1);
                g2.barycentricCoordinates = float2(0, 0);

                stream.Append(g0);
                stream.Append(g1);
                stream.Append(g2);
            }

            #define DP_INTERPOLATE(fieldName) data.fieldName = \
                data.fieldName = patch[0].fieldName * barycentricCoordinates.x + \
                                 patch[1].fieldName * barycentricCoordinates.y + \
                                 patch[2].fieldName * barycentricCoordinates.z;               

            [UNITY_domain("tri")]
            v2g dp(TessellationFactors factors, OutputPatch<TessellationControlPoint, 3> patch, float3 barycentricCoordinates : SV_DOMAINLOCATION) {
                VertexData data;
                DP_INTERPOLATE(vertex)
                DP_INTERPOLATE(uv)

                return vp(data);
            }

			float3 ComputeWorldSpacePosition(float2 positionNDC, float deviceDepth) {
				float4 positionCS = float4(positionNDC * 2.0 - 1.0, deviceDepth, 1.0);
				float4 hpositionWS = mul(_CameraInvViewProjection, positionCS);
				return hpositionWS.xyz / hpositionWS.w;
			}

			float4 fp(g2f f) : SV_TARGET {
                float3 lightDir = -normalize(_SunDirection);
                float3 viewDir = normalize(_WorldSpaceCameraPos - f.data.worldPos);
                float3 halfwayDir = normalize(lightDir + viewDir);

                float height = _HeightTex.Sample(linear_repeat_sampler, f.data.uv * _Tile).y;
				float jacobian = _FoamTex.Sample(linear_repeat_sampler, f.data.uv * _Tile).r;

				float4 derivatives = _NormalTex.Sample(linear_repeat_sampler, f.data.uv * _Tile);
				
				float2 slope = derivatives.xy / (1 + abs(derivatives.zw));
				slope *= _NormalStrength;

				float3 normal = normalize(float3(-slope.x, 1.0f, -slope.y));
                normal = normalize(UnityObjectToWorldNormal(normalize(normal)));

				// normal = centralDifferenceNormal(f.data.worldPos, 0.01f);

				float ndotl = DotClamped(lightDir, normal);

				float3 diffuseReflectance = _DiffuseReflectance / PI;
                float3 diffuse = _LightColor0.rgb * ndotl * diffuseReflectance;

				// Schlick Fresnel
				float3 fresnelNormal = normal;
				fresnelNormal.xz *= _FresnelNormalStrength;
				fresnelNormal = normalize(fresnelNormal);
				float base = 1 - dot(viewDir, fresnelNormal);
				float exponential = pow(base, _FresnelShininess);
				float R = exponential + _FresnelBias * (1.0f - exponential);
				R *= _FresnelStrength;
				
				float3 fresnel = _FresnelColor * R;
                
				if (_UseEnvironmentMap) {
					float3 reflectedDir = reflect(-viewDir, normal);
					float3 skyCol = texCUBE(_EnvironmentMap, reflectedDir).rgb;
					float3 sun = _SunColor * pow(max(0.0f, DotClamped(reflectedDir, lightDir)), 500.0f);

					fresnel = skyCol.rgb * R;
					fresnel += sun * R;
				}


				float3 specularReflectance = _SpecularReflectance;
				float3 specNormal = normal;
				specNormal.xz *= _SpecularNormalStrength;
				specNormal = normalize(specNormal);
				float spec = pow(DotClamped(specNormal, halfwayDir), _Shininess) * ndotl;
                float3 specular = _LightColor0.rgb * specularReflectance * spec;

				// Schlick Fresnel but again for specular
				base = 1 - DotClamped(viewDir, halfwayDir);
				exponential = pow(base, 5.0f);
				R = exponential + _FresnelBias * (1.0f - exponential);

				specular *= R;
				

				float3 output = _Ambient + diffuse + specular + fresnel;
				output = lerp(output, _TipColor, saturate(jacobian));
				
				return float4(output, 1.0f);
			}

			ENDCG
		}
	}
}