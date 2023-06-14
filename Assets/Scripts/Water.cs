using System;
using System.Collections;
using System.Collections.Generic;
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
    }; public WaveFunction waveFunction;
    
    public enum WaveType {
        Directional = 0,
        Circular,
    }; public WaveType waveType;

    [Range(0.0f, 360.0f)]
    public float direction = 0.0f;

    public Vector2 origin = new Vector2(0.0f, 0.0f);

    [Range(0.01f, 5.0f)]
    public float speed = 1.0f;

    [Range(0.01f, 5.0f)]
    public float amplitude = 1.0f;

    [Range(0.01f, 3.0f)]
    public float wavelength = 1.0f;

    [Range(1.0f, 5.0f)]
    public float steepness = 1.0f;

    private struct Wave {
        public float frequency;
        public float amplitude;
        public float phase;
        public float steepness;
        public Vector2 direction;
        public WaveType waveType;
        public Vector2 origin;

        public Wave(float wavelength, float amplitude, float speed, float direction, float steepness, WaveType waveType, Vector2 origin) {
            this.frequency = waveType == WaveType.Circular ? -2.0f / wavelength : 2.0f / wavelength;
            this.amplitude = amplitude;
            this.phase = speed * 2.0f / wavelength;
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

        public float Sine(Vector3 v) {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            return Mathf.Sin(this.frequency * xz + Time.time * this.phase) * this.amplitude;
        }

        public Vector2 SineNormal(Vector3 v) {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            float dx = this.frequency * this.amplitude * d.x * Mathf.Cos(xz * this.frequency + Time.time * this.phase);
            float dy = this.frequency * this.amplitude * d.y * Mathf.Cos(xz * this.frequency + Time.time * this.phase);

            return new Vector2(dx, dy);
        }

        public float SteepSine(Vector3 v) {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            return 2 * this.amplitude * Mathf.Pow((Mathf.Sin(xz * this.frequency + Time.time * this.phase) + 1) / 2.0f, this.steepness);
        }

        public Vector2 SteepSineNormal(Vector3 v) {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            float h = 2 * this.amplitude * Mathf.Pow((Mathf.Sin(xz * this.frequency + Time.time * this.phase) + 1) / 2.0f, this.steepness - 1);
            float dx = this.steepness * d.x * this.frequency * this.amplitude * h * Mathf.Cos(xz * this.frequency + Time.time * this.phase);
            float dy = this.steepness * d.y * this.frequency * this.amplitude * h * Mathf.Cos(xz * this.frequency + Time.time * this.phase);

            return new Vector2(dx, dy);
        }

        public Vector3 Gerstner(Vector3 v) {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            Vector3 g = new Vector3(0.0f, 0.0f, 0.0f);
            g.x = (this.steepness - 1) * this.amplitude * d.x * Mathf.Cos(this.frequency * xz + Time.time * this.phase);
            g.z = (this.steepness - 1) * this.amplitude * d.y * Mathf.Cos(this.frequency * xz + Time.time * this.phase);
            g.y = this.amplitude * Mathf.Sin(this.frequency * xz + Time.time * this.phase);

            if (this.waveType == WaveType.Circular) g.y = -g.y;
            
            return g;
        }
    }

    private Wave wave;

    private Material waterMaterial;
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] displacedVertices;
    private Vector3[] normals;

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
    }

    void CreateMaterial() {
        if (waterShader == null) return;
        if (waterMaterial != null) return;

        waterMaterial = new Material(waterShader);
        MeshRenderer renderer = GetComponent<MeshRenderer>();

        renderer.material = waterMaterial;
    }

    void OnEnable() {
        CreateWaterPlane();
        CreateMaterial();
    }

    void Update() {
        Wave w = new Wave(wavelength, amplitude, speed, direction, steepness, waveType, origin);

        if (vertices != null) {
            for (int i = 0; i < vertices.Length; ++i) {
                Vector3 v = transform.TransformPoint(vertices[i]);

                if (waveFunction == WaveFunction.Sine)
                    displacedVertices[i].y = w.Sine(v);
                else if (waveFunction == WaveFunction.SteepSine)
                    displacedVertices[i].y = w.SteepSine(v);
                else if (waveFunction == WaveFunction.Gerstner) {
                    Vector3 g = w.Gerstner(v);
                    //Debug.Log("Vertex: " + v.ToString() + "\nWave: " + g.ToString());
                    displacedVertices[i].x = v.x + g.x;
                    displacedVertices[i].z = v.z + g.z;
                    displacedVertices[i].y = g.y;
                }

                Vector3 normal = new Vector3(0.0f, 1.0f, 0.0f);
                if (waveFunction == WaveFunction.Sine)
                    normal = w.SineNormal(v);
                else if (waveFunction == WaveFunction.SteepSine)
                    normal = w.SteepSineNormal(v);
                
                normals[i] = new Vector3(-normal.x, 1.0f, -normal.y);
                normals[i].Normalize();
            }

            mesh.vertices = displacedVertices;
            mesh.normals = normals;
            mesh.RecalculateNormals();
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
        }
    }

    private void OnDrawGizmos() {
        if (vertices == null) return;

        for (int i = 0; i < vertices.Length; ++i) {
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(transform.TransformPoint(vertices[i]), 0.1f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.TransformPoint(vertices[i]), normals[i]);
        }
    }
}
