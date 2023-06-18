using System;
using System.Collections;
using System.Collections.Generic;
using static System.Runtime.InteropServices.Marshal;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Water : MonoBehaviour {
    public Shader waterShader;

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

        public Wave(float wavelength, float amplitude, float speed, float direction, float steepness, WaveType waveType, Vector2 origin, WaveFunction waveFunction) {
            this.frequency = 2.0f / wavelength;
            this.amplitude = amplitude;
            this.phase = speed * 2.0f / wavelength;

            if (waveFunction == WaveFunction.Gerstner)
                this.steepness = (steepness - 1) / this.frequency * this.amplitude * 4.0f;
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

                Vector2 heading = p - this.origin;
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

    private Wave[] waves = new Wave[4];
    private ComputeBuffer waveBuffer;

    private Material waterMaterial;
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] displacedVertices;
    private Vector3[] normals;
    private Vector3[] displacedNormals;

    private bool usingVertexDisplacement = false;

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
            Debug.Log("Toggled GPU Vertex Displacement");
        } else {
            waterMaterial.DisableKeyword("USE_VERTEX_DISPLACEMENT");
            mesh.vertices = displacedVertices;
            mesh.normals = displacedNormals;
            Debug.Log("Toggled CPU Vertex Displacement");
        }
    }

    private void CreateWaterPlane() {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Water";

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
        MeshRenderer renderer = GetComponent<MeshRenderer>();

        renderer.material = waterMaterial;
    }

    void CreateWaveBuffer() {
        if (waveBuffer != null) return;

        waveBuffer = new ComputeBuffer(4, SizeOf(typeof(Wave)));

        waterMaterial.SetBuffer("_Waves", waveBuffer);
    }

    void OnEnable() {
        CreateWaterPlane();
        CreateMaterial();
        CreateWaveBuffer();
    }

    void Update() {
        if (usingVertexDisplacement) {
            if (updateStatics) {
                waves[0] = new Wave(wavelength1, amplitude1, speed1, direction1, steepness1, waveType, origin1, waveFunction);
                waves[1] = new Wave(wavelength2, amplitude2, speed2, direction2, steepness2, waveType, origin2, waveFunction);
                waves[2] = new Wave(wavelength3, amplitude3, speed3, direction3, steepness3, waveType, origin3, waveFunction);
                waves[3] = new Wave(wavelength4, amplitude4, speed4, direction4, steepness4, waveType, origin4, waveFunction);

                waveBuffer.SetData(waves);
                waterMaterial.SetBuffer("_Waves", waveBuffer);
            }
        } else {
            waves[0] = new Wave(wavelength1, amplitude1, speed1, direction1, steepness1, waveType, origin1, waveFunction);
            waves[1] = new Wave(wavelength2, amplitude2, speed2, direction2, steepness2, waveType, origin2, waveFunction);
            waves[2] = new Wave(wavelength3, amplitude3, speed3, direction3, steepness3, waveType, origin3, waveFunction);
            waves[3] = new Wave(wavelength4, amplitude4, speed4, direction4, steepness4, waveType, origin4, waveFunction);

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
        if (vertices == null) return;

        for (int i = 0; i < vertices.Length; ++i) {
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(transform.TransformPoint(displacedVertices[i]), 0.1f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.TransformPoint(displacedVertices[i]), normals[i]);
        }
    }
}
