using System;
using System.Collections;
using System.Collections.Generic;
using static System.Runtime.InteropServices.Marshal;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Water : MonoBehaviour {
    public Shader waterShader;

    public Atmosphere atmosphere;

    public int planeLength = 10;
    public int quadRes = 10;

    public enum WaveFunction {
        Sine = 0,
        SteepSine,
        Gerstner
    }; 

    public enum WaveType {
        Directional = 0,
        Circular,
    };

    public WaveFunction waveFunction;
    public WaveType waveType;
    public bool updateStatics = true;

    public float direction1 = 0.0f;
    public Vector2 origin1 = new Vector2(0.0f, 0.0f);
    public float speed1 = 1.0f;
    public float amplitude1 = 1.0f;
    public float wavelength1 = 1.0f;
    public float steepness1 = 1.0f;

    public float direction2 = 0.0f;
    public Vector2 origin2 = new Vector2(0.0f, 0.0f);
    public float speed2 = 1.0f;
    public float amplitude2 = 1.0f;
    public float wavelength2 = 1.0f;
    public float steepness2 = 1.0f;

    public float direction3 = 0.0f;
    public Vector2 origin3 = new Vector2(0.0f, 0.0f);
    public float speed3 = 1.0f;
    public float amplitude3 = 1.0f;
    public float wavelength3 = 1.0f;
    public float steepness3 = 1.0f;

    public float direction4 = 0.0f;
    public Vector2 origin4 = new Vector2(0.0f, 0.0f);
    public float speed4 = 1.0f;
    public float amplitude4 = 1.0f;
    public float wavelength4 = 1.0f;
    public float steepness4 = 1.0f;

    public struct Wave {
        public Vector2 direction;
        public Vector2 origin;
        public float frequency;
        public float amplitude;
        public float phase;
        public float steepness;
        public WaveType waveType;

        public Wave(float wavelength, float amplitude, float speed, float direction, float steepness, WaveType waveType, Vector2 origin, WaveFunction waveFunction, int waveCount) {
            this.frequency = 2.0f / wavelength;
            this.amplitude = amplitude;
            this.phase = speed * Mathf.Sqrt(9.8f * 2.0f * Mathf.PI / wavelength);;

            if (waveFunction == WaveFunction.Gerstner)
                this.steepness = steepness / this.frequency * this.amplitude * (float)waveCount;
            else
                this.steepness = steepness;
            
            this.waveType = waveType;
            this.origin = origin;

            this.direction = new Vector2(Mathf.Cos(Mathf.Deg2Rad * direction), Mathf.Sin(Mathf.Deg2Rad * direction));
            this.direction.Normalize();
        }

        public Vector2 GetDirection(Vector3 v) {
            Vector2 d = this.direction;

            if (waveType == WaveType.Circular) {
                Vector2 p = new Vector2(v.x, v.z);

                d = p - this.origin;
                d.Normalize();
            }

            return d;
        }

        public float GetWaveCoord(Vector3 v, Vector2 d) {
            if (waveType == WaveType.Circular) {
                Vector2 p = new Vector2(v.x, v.z);
                Vector2 heading = p - this.origin;

                return heading.magnitude;
            }

            return v.x * d.x + v.z * d.y;
        }

        public float GetTime() {
            return waveType == WaveType.Circular ? -Time.time * this.phase : Time.time * this.phase;
        }

        public float Sine(Vector3 v) {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            return Mathf.Sin(this.frequency * xz + GetTime()) * this.amplitude;
        }

        public Vector3 SineNormal(Vector3 v) {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            float dx = this.frequency * this.amplitude * d.x * Mathf.Cos(xz * this.frequency + GetTime());
            float dy = this.frequency * this.amplitude * d.y * Mathf.Cos(xz * this.frequency + GetTime());

            return new Vector3(dx, dy, 0.0f);
        }

        public float SteepSine(Vector3 v) {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            return 2 * this.amplitude * Mathf.Pow((Mathf.Sin(xz * this.frequency + GetTime()) + 1) / 2.0f, this.steepness);
        }

        public Vector3 SteepSineNormal(Vector3 v) {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            float h = Mathf.Pow((Mathf.Sin(xz * this.frequency + GetTime()) + 1) / 2.0f, this.steepness - 1);
            float dx = this.steepness * d.x * this.frequency * this.amplitude * h * Mathf.Cos(xz * this.frequency + GetTime());
            float dy = this.steepness * d.y * this.frequency * this.amplitude * h * Mathf.Cos(xz * this.frequency + GetTime());

            return new Vector3(dx, dy, 0.0f);
        }

        public Vector3 Gerstner(Vector3 v) {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            Vector3 g = new Vector3(0.0f, 0.0f, 0.0f);
            g.x = this.steepness * this.amplitude * d.x * Mathf.Cos(this.frequency * xz + GetTime());
            g.z = this.steepness * this.amplitude * d.y * Mathf.Cos(this.frequency * xz + GetTime());
            g.y = this.amplitude * Mathf.Sin(this.frequency * xz + GetTime());
            
            return g;
        }

        public Vector3 GerstnerNormal(Vector3 v) {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            Vector3 n = new Vector3(0.0f, 0.0f, 0.0f);
            
            float wa = this.frequency * this.amplitude;
            float s = Mathf.Sin(this.frequency * xz + GetTime());
            float c = Mathf.Cos(this.frequency * xz + GetTime());

            n.x = d.x * wa * c;
            n.z = d.y * wa * c;
            n.y = this.steepness * wa * s;

            return n;
        }
    }

    private Camera cam;

    private Wave[] waves = new Wave[64];
    private ComputeBuffer waveBuffer;

    private Material waterMaterial;
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] displacedVertices;
    private Vector3[] normals;
    private Vector3[] displacedNormals;

    public bool usingVertexDisplacement = true;
    public bool usingPixelShaderNormals = true;
    public bool usingCircularWaves = false;
    public bool letJesusTakeTheWheel = true;
    public bool usingFBM = true;

    // Procedural Settings
    public int waveCount = 4;
    public float medianWavelength = 1.0f;
    public float wavelengthRange = 1.0f;
    public float medianDirection = 0.0f;
    public float directionalRange = 30.0f;
    public float medianAmplitude = 1.0f;
    public float medianSpeed = 1.0f;
    public float speedRange = 0.1f;
    public float steepness = 0.0f;

    // FBM Settings
    public int vertexWaveCount = 8;
    public int fragmentWaveCount = 40;
    
    public float vertexSeed = 0;
    public float vertexSeedIter = 1253.2131f;
    public float vertexFrequency = 1.0f;
    public float vertexFrequencyMult = 1.18f;
    public float vertexAmplitude = 1.0f;
    public float vertexAmplitudeMult = 0.82f;
    public float vertexInitialSpeed = 2.0f;
    public float vertexSpeedRamp = 1.07f;
    public float vertexDrag = 1.0f;
    public float vertexHeight = 1.0f;
    public float vertexMaxPeak = 1.0f;
    public float vertexPeakOffset = 1.0f;
    public float fragmentSeed = 0;
    public float fragmentSeedIter = 1253.2131f;
    public float fragmentFrequency = 1.0f;
    public float fragmentFrequencyMult = 1.18f;
    public float fragmentAmplitude = 1.0f;
    public float fragmentAmplitudeMult = 0.82f;
    public float fragmentInitialSpeed = 2.0f;
    public float fragmentSpeedRamp = 1.07f;
    public float fragmentDrag = 1.0f;
    public float fragmentHeight = 1.0f;
    public float fragmentMaxPeak = 1.0f;
    public float fragmentPeakOffset = 1.0f; 
    
    public float normalStrength = 1;

    // Shader Settings
    [ColorUsageAttribute(false, true)]
    public Color ambient;

    [ColorUsageAttribute(false, true)]
    public Color diffuseReflectance;

    [ColorUsageAttribute(false, true)]
    public Color specularReflectance;

    public float shininess;
    public float specularNormalStrength = 1;

    [ColorUsageAttribute(false, true)]
    public Color fresnelColor;

    public bool useTextureForFresnel = false;
    public Texture environmentTexture;

    public float fresnelBias, fresnelStrength, fresnelShininess;
    public float fresnelNormalStrength = 1;

    [ColorUsageAttribute(false, true)]
    public Color tipColor;
    public float tipAttenuation;

    public void ToggleJesus() {
        if (!Application.isPlaying) {
            Debug.Log("Not in play mode!");
            return;
        }

        letJesusTakeTheWheel = !letJesusTakeTheWheel;
        if (letJesusTakeTheWheel) GenerateNewWaves();
    }

    public void GenerateNewWaves() {
        float wavelengthMin = medianWavelength / (1.0f + wavelengthRange);
        float wavelengthMax = medianWavelength * (1.0f + wavelengthRange);
        float directionMin = medianDirection - directionalRange;
        float directionMax = medianDirection + directionalRange;
        float speedMin = Mathf.Max(0.01f, medianSpeed - speedRange);
        float speedMax = medianSpeed + speedRange;
        float ampOverLen = medianAmplitude / medianWavelength;

        float halfPlaneWidth = planeLength * 0.5f;
        Vector3 minPoint = transform.TransformPoint(new Vector3(-halfPlaneWidth, 0.0f, -halfPlaneWidth));
        Vector3 maxPoint = transform.TransformPoint(new Vector3(halfPlaneWidth, 0.0f, halfPlaneWidth));

        for (int wi = 0; wi < waveCount; ++wi) {
            float wavelength = UnityEngine.Random.Range(wavelengthMin, wavelengthMax);
            float direction = UnityEngine.Random.Range(directionMin, directionMax);
            float amplitude = wavelength * ampOverLen;
            float speed = UnityEngine.Random.Range(speedMin, speedMax);
            Vector2 origin = new Vector2(UnityEngine.Random.Range(minPoint.x * 2, maxPoint.x * 2), UnityEngine.Random.Range(minPoint.x * 2, maxPoint.x * 2));

            waves[wi] = new Wave(wavelength, amplitude, speed, direction, steepness, waveType, origin, waveFunction, waveCount);
        }

        waveBuffer.SetData(waves);
        waterMaterial.SetBuffer("_Waves", waveBuffer);
    }

    public void CycleWaveFunction() {
        if (!Application.isPlaying) {
            Debug.Log("Not in play mode!");
            return;
        }

        switch(waveFunction) {
            case WaveFunction.Sine:
                waterMaterial.DisableKeyword("SINE_WAVE");
            break;
            case WaveFunction.SteepSine:
                waterMaterial.DisableKeyword("STEEP_SINE_WAVE");
            break;
            case WaveFunction.Gerstner:
                waterMaterial.DisableKeyword("GERSTNER_WAVE");
            break;
        }

        waveFunction += 1;
        if ((int)waveFunction > 2) waveFunction = 0;

        switch(waveFunction) {
            case WaveFunction.Sine:
                waterMaterial.EnableKeyword("SINE_WAVE");
            break;
            case WaveFunction.SteepSine:
                waterMaterial.EnableKeyword("STEEP_SINE_WAVE");
            break;
            case WaveFunction.Gerstner:
                waterMaterial.EnableKeyword("GERSTNER_WAVE");
            break;
        }
    }

    public void ToggleVertexDisplacementMethod() {
        if (!Application.isPlaying) {
            Debug.Log("Not in play mode!");
            return;
        }

        usingVertexDisplacement = !usingVertexDisplacement;

        if (usingVertexDisplacement) {
            waterMaterial.EnableKeyword("USE_VERTEX_DISPLACEMENT");
            mesh.vertices = vertices;
            mesh.normals = normals;
        } else {
            waterMaterial.DisableKeyword("USE_VERTEX_DISPLACEMENT");
            mesh.vertices = displacedVertices;
            mesh.normals = displacedNormals;
        }
    }

    public void ToggleNormalGeneration() {
        if (!Application.isPlaying) {
            Debug.Log("Not in play mode!");
            return;
        }

        usingPixelShaderNormals = !usingPixelShaderNormals;

        if (usingPixelShaderNormals) {
            waterMaterial.EnableKeyword("NORMALS_IN_PIXEL_SHADER");
        } else {
            waterMaterial.DisableKeyword("NORMALS_IN_PIXEL_SHADER");
        }
    }

    public void ToggleCircularWaves() {
        if (!Application.isPlaying) {
            Debug.Log("Not in play mode!");
            return;
        }

        usingCircularWaves = !usingCircularWaves;

        if (usingCircularWaves) {
            waterMaterial.EnableKeyword("CIRCULAR_WAVES");
        } else {
            waterMaterial.DisableKeyword("CIRCULAR_WAVES");
        }
    }

    public void ToggleFBM() {
        if (!Application.isPlaying) {
            Debug.Log("Not in play mode!");
            return;
        }

        usingFBM = !usingFBM;

        if (usingFBM) {
            waterMaterial.EnableKeyword("USE_FBM");
        } else {
            waterMaterial.DisableKeyword("USE_FBM");
        }
    }

    private void CreateWaterPlane() {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Water";
        mesh.indexFormat = IndexFormat.UInt32;

        float halfLength = planeLength * 0.5f;
        int sideVertCount = planeLength * quadRes;

        vertices = new Vector3[(sideVertCount + 1) * (sideVertCount + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        Vector4[] tangents = new Vector4[vertices.Length];
        Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);

        for (int i = 0, x = 0; x <= sideVertCount; ++x) {
            for (int z = 0; z <= sideVertCount; ++z, ++i) {
                vertices[i] = new Vector3(((float)x / sideVertCount * planeLength) - halfLength, 0, ((float)z / sideVertCount * planeLength) - halfLength);
                uv[i] = new Vector2((float)x / sideVertCount, (float)z / sideVertCount);
                tangents[i] = tangent;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.tangents = tangents;

        int[] triangles = new int[sideVertCount * sideVertCount * 6];

        for (int ti = 0, vi = 0, x = 0; x < sideVertCount; ++vi, ++x) {
            for (int z = 0; z < sideVertCount; ti += 6, ++vi, ++z) {
                triangles[ti] = vi;
                triangles[ti + 1] = vi + 1;
                triangles[ti + 2] = vi + sideVertCount + 2;
                triangles[ti + 3] = vi;
                triangles[ti + 4] = vi + sideVertCount + 2;
                triangles[ti + 5] = vi + sideVertCount + 1;
            }
        }

        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        normals = mesh.normals;

        displacedVertices = new Vector3[vertices.Length];
        Array.Copy(vertices, 0, displacedVertices, 0, vertices.Length);
        displacedNormals = new Vector3[normals.Length];
        Array.Copy(normals, 0, displacedNormals, 0, normals.Length);
    }

    void CreateMaterial() {
        if (waterShader == null) return;
        if (waterMaterial != null) return;

        waterMaterial = new Material(waterShader);
        
        waterMaterial.DisableKeyword("USE_VERTEX_DISPLACEMENT");
        waterMaterial.DisableKeyword("SINE_WAVE");
        waterMaterial.DisableKeyword("STEEP_SINE_WAVE");
        waterMaterial.DisableKeyword("GERSTNER_WAVE");

        switch(waveFunction) {
            case WaveFunction.Sine:
                waterMaterial.EnableKeyword("SINE_WAVE");
            break;
            case WaveFunction.SteepSine:
                waterMaterial.EnableKeyword("STEEP_SINE_WAVE");
            break;
            case WaveFunction.Gerstner:
                waterMaterial.EnableKeyword("GERSTNER_WAVE");
            break;
        }

        if (usingVertexDisplacement) {
            waterMaterial.EnableKeyword("USE_VERTEX_DISPLACEMENT");
            waterMaterial.SetBuffer("_Waves", waveBuffer);
        } else
            waterMaterial.DisableKeyword("USE_VERTEX_DISPLACEMENT");
        

        if (usingPixelShaderNormals)
            waterMaterial.EnableKeyword("NORMALS_IN_PIXEL_SHADER");
        else
            waterMaterial.DisableKeyword("NORMALS_IN_PIXEL_SHADER");
        
        if (usingCircularWaves)
            waterMaterial.EnableKeyword("CIRCULAR_WAVES");
        else
            waterMaterial.DisableKeyword("CIRCULAR_WAVES");
        
        if (usingFBM)
            waterMaterial.EnableKeyword("USE_FBM");
        else
            waterMaterial.DisableKeyword("USE_FBM");
        

        MeshRenderer renderer = GetComponent<MeshRenderer>();

        renderer.material = waterMaterial;
    }

    void CreateWaveBuffer() {
        if (waveBuffer != null) return;

        waveBuffer = new ComputeBuffer(64, SizeOf(typeof(Wave)));

        waterMaterial.SetBuffer("_Waves", waveBuffer);
    }

    void OnEnable() {
        CreateWaterPlane();
        CreateMaterial();
        CreateWaveBuffer();
        cam = GameObject.Find("Main Camera").GetComponent<Camera>();
    }

    void Update() {
        waterMaterial.SetVector("_Ambient", ambient);
        waterMaterial.SetVector("_DiffuseReflectance", diffuseReflectance);
        waterMaterial.SetVector("_SpecularReflectance", specularReflectance);
        waterMaterial.SetVector("_TipColor", tipColor);
        waterMaterial.SetVector("_FresnelColor", fresnelColor);
        waterMaterial.SetFloat("_Shininess", shininess * 100);
        waterMaterial.SetFloat("_FresnelBias", fresnelBias);
        waterMaterial.SetFloat("_FresnelStrength", fresnelStrength);
        waterMaterial.SetFloat("_FresnelShininess", fresnelShininess);
        waterMaterial.SetFloat("_TipAttenuation", tipAttenuation);
        waterMaterial.SetFloat("_FresnelNormalStrength", fresnelNormalStrength);
        waterMaterial.SetFloat("_SpecularNormalStrength", specularNormalStrength);
        waterMaterial.SetInt("_WaveCount", waveCount);
        waterMaterial.SetInt("_UseEnvironmentMap", useTextureForFresnel ? 1 : 0);

        if (useTextureForFresnel) {
            waterMaterial.SetTexture("_EnvironmentMap", environmentTexture);
        }

        if (atmosphere != null) {
            waterMaterial.SetVector("_SunDirection", atmosphere.GetSunDirection());
            waterMaterial.SetVector("_SunColor", atmosphere.GetSunColor());
        }

        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 viewProjMatrix = projMatrix * cam.worldToCameraMatrix;
        waterMaterial.SetMatrix("_CameraInvViewProjection", viewProjMatrix.inverse);

        if (usingVertexDisplacement) {
            if (updateStatics) {
                if (letJesusTakeTheWheel) {
                    waterMaterial.SetInt("_VertexWaveCount", vertexWaveCount);
                    waterMaterial.SetFloat("_VertexSeed", vertexSeed);
                    waterMaterial.SetFloat("_VertexSeedIter", vertexSeedIter);
                    waterMaterial.SetFloat("_VertexFrequency", vertexFrequency);
                    waterMaterial.SetFloat("_VertexFrequencyMult", vertexFrequencyMult);
                    waterMaterial.SetFloat("_VertexAmplitude", vertexAmplitude);
                    waterMaterial.SetFloat("_VertexAmplitudeMult", vertexAmplitudeMult);
                    waterMaterial.SetFloat("_VertexInitialSpeed", vertexInitialSpeed);
                    waterMaterial.SetFloat("_VertexSpeedRamp", vertexSpeedRamp);
                    waterMaterial.SetFloat("_VertexDrag", vertexDrag);
                    waterMaterial.SetFloat("_VertexHeight", vertexHeight);
                    waterMaterial.SetFloat("_VertexMaxPeak", vertexMaxPeak);
                    waterMaterial.SetFloat("_VertexPeakOffset", vertexPeakOffset);
                    waterMaterial.SetInt("_FragmentWaveCount", fragmentWaveCount);
                    waterMaterial.SetFloat("_FragmentSeed", fragmentSeed);
                    waterMaterial.SetFloat("_FragmentSeedIter", fragmentSeedIter);
                    waterMaterial.SetFloat("_FragmentFrequency", fragmentFrequency);
                    waterMaterial.SetFloat("_FragmentFrequencyMult", fragmentFrequencyMult);
                    waterMaterial.SetFloat("_FragmentAmplitude", fragmentAmplitude);
                    waterMaterial.SetFloat("_FragmentAmplitudeMult", fragmentAmplitudeMult);
                    waterMaterial.SetFloat("_FragmentInitialSpeed", fragmentInitialSpeed);
                    waterMaterial.SetFloat("_FragmentSpeedRamp", fragmentSpeedRamp);
                    waterMaterial.SetFloat("_FragmentDrag", fragmentDrag);
                    waterMaterial.SetFloat("_FragmentHeight", fragmentHeight);
                    waterMaterial.SetFloat("_FragmentMaxPeak", fragmentMaxPeak);
                    waterMaterial.SetFloat("_FragmentPeakOffset", fragmentPeakOffset);
                    waterMaterial.SetFloat("_NormalStrength", normalStrength);

                    waterMaterial.SetBuffer("_Waves", waveBuffer);
                    return;
                }

                waterMaterial.SetInt("_WaveCount", 4);

                waves[0] = new Wave(wavelength1, amplitude1, speed1, direction1, steepness1, waveType, origin1, waveFunction, waveCount);
                waves[1] = new Wave(wavelength2, amplitude2, speed2, direction2, steepness2, waveType, origin2, waveFunction, waveCount);
                waves[2] = new Wave(wavelength3, amplitude3, speed3, direction3, steepness3, waveType, origin3, waveFunction, waveCount);
                waves[3] = new Wave(wavelength4, amplitude4, speed4, direction4, steepness4, waveType, origin4, waveFunction, waveCount);

                waveBuffer.SetData(waves);
                waterMaterial.SetBuffer("_Waves", waveBuffer);
            }
        } else {
            waterMaterial.SetInt("_WaveCount", 4);
            waves[0] = new Wave(wavelength1, amplitude1, speed1, direction1, steepness1, waveType, origin1, waveFunction, waveCount);
            waves[1] = new Wave(wavelength2, amplitude2, speed2, direction2, steepness2, waveType, origin2, waveFunction, waveCount);
            waves[2] = new Wave(wavelength3, amplitude3, speed3, direction3, steepness3, waveType, origin3, waveFunction, waveCount);
            waves[3] = new Wave(wavelength4, amplitude4, speed4, direction4, steepness4, waveType, origin4, waveFunction, waveCount);

            if (vertices != null) {
                for (int i = 0; i < vertices.Length; ++i) {
                    Vector3 v = transform.TransformPoint(vertices[i]);

                    Vector3 newPos = new Vector3(0.0f, 0.0f, 0.0f);
                    for (int wi = 0; wi < 4; ++wi) {
                        Wave w = waves[wi];

                        if (waveFunction == WaveFunction.Sine)
                            newPos.y += w.Sine(v);
                        else if (waveFunction == WaveFunction.SteepSine)
                            newPos.y += w.SteepSine(v);
                        else if (waveFunction == WaveFunction.Gerstner) {
                            Vector3 g = w.Gerstner(v);

                            newPos.x += g.x;
                            newPos.z += g.z;
                            newPos.y += g.y;
                        }
                    }

                    displacedVertices[i] = new Vector3(v.x + newPos.x, newPos.y, v.z + newPos.z);

                    // Gerstner waves require the new position to be calculated before normal calculation
                    // otherwise could do this in same loop above
                    Vector3 normal = new Vector3(0.0f, 0.0f, 0.0f);
                    for (int wi = 0; wi < 4; ++wi) {
                        Wave w = waves[wi];

                        if (waveFunction == WaveFunction.Sine) {
                            normal = normal + w.SineNormal(v);
                        } else if (waveFunction == WaveFunction.SteepSine) {
                            normal = normal + w.SteepSineNormal(v);
                        } else if (waveFunction == WaveFunction.Gerstner) {
                            normal = normal + w.GerstnerNormal(displacedVertices[i]);
                        }
                    }

                    if (waveFunction == WaveFunction.Gerstner) {
                        displacedNormals[i] = new Vector3(-normal.x, 1.0f - normal.y, -normal.z);
                    } else {
                        displacedNormals[i] = new Vector3(-normal.x, 1.0f, -normal.y);
                    }

                    displacedNormals[i].Normalize();
                }

                mesh.vertices = displacedVertices;
                mesh.normals = displacedNormals;
            }
        }
    }

    void OnDisable() {
        if (waterMaterial != null) {
            Destroy(waterMaterial);
            waterMaterial = null;
        }

        if (mesh != null) {
            Destroy(mesh);
            mesh = null;
            vertices = null;
            normals = null;
            displacedVertices = null;
            displacedNormals = null;
        }

        if (waveBuffer != null) {
            waveBuffer.Release();
            waveBuffer = null;
        }
    }

    private void OnDrawGizmos() {
        /*
        if (vertices == null) return;

        for (int i = 0; i < vertices.Length; ++i) {
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(transform.TransformPoint(displacedVertices[i]), 0.1f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.TransformPoint(displacedVertices[i]), displacedNormals[i]);
        }
        */
    }
}
