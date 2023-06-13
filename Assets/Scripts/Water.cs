using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Water : MonoBehaviour {
    public Shader waterShader;

    public int planeLength = 10;
    public int quadRes = 10;

    public enum WaveType {
        Sine = 0,
        SteepSine,
        Gerstner
    }; public WaveType waveType;

    [Range(0.0f, 360.0f)]
    public float direction = 0.0f;

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

        public Wave(float wavelength, float amplitude, float speed, float direction, float steepness) {
            this.frequency = 2.0f / wavelength;
            this.amplitude = amplitude;
            this.phase = speed * 2.0f / wavelength;
            this.steepness = steepness;

            this.direction = new Vector2(Mathf.Cos(Mathf.Deg2Rad * direction), Mathf.Sin(Mathf.Deg2Rad * direction));
            this.direction.Normalize();
        }

        public float Sine(Vector3 v) {
            v.x *= this.direction.x;
            v.z *= this.direction.y;

            return Mathf.Sin(this.frequency * (v.x + v.z) + Time.time * this.phase) * this.amplitude;
        }

        public Vector2 SineNormal(Vector3 v) {
            v.x *= this.direction.x;
            v.z *= this.direction.y;

            float dx = this.frequency * this.amplitude * this.direction.x * Mathf.Cos((v.x + v.z) * this.frequency + Time.time * this.phase);
            float dy = this.frequency * this.amplitude * this.direction.y * Mathf.Cos((v.x + v.z) * this.frequency + Time.time * this.phase);

            return new Vector2(dx, dy);
        }

        public float SteepSine(Vector3 v) {
            v.x *= this.direction.x;
            v.z *= this.direction.y;

            return 2 * this.amplitude * Mathf.Pow((Mathf.Sin((v.x + v.z) * this.frequency + Time.time * this.phase) + 1) / 2.0f, this.steepness);
        }

        public Vector2 SteepSineNormal(Vector3 v) {
            v.x *= this.direction.x;
            v.z *= this.direction.y;

            float h = 2 * this.amplitude * Mathf.Pow((Mathf.Sin((v.x + v.z) * this.frequency + Time.time * this.phase) + 1) / 2.0f, this.steepness - 1);
            float dx = this.steepness * this.direction.x * this.frequency * this.amplitude * h * Mathf.Cos((v.x + v.z) * this.frequency + Time.time * this.phase);
            float dy = this.steepness * this.direction.y * this.frequency * this.amplitude * h * Mathf.Cos((v.x + v.z) * this.frequency + Time.time * this.phase);

            return new Vector2(dx, dy);
        }
    }

    Wave wave;

    private Material waterMaterial;
    private Mesh mesh;
    private Vector3[] vertices;
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
        Wave w = new Wave(wavelength, amplitude, speed, direction, steepness);

        if (vertices != null) {
            for (int i = 0; i < vertices.Length; ++i) {
                Vector3 v = transform.TransformPoint(vertices[i]);

                if (waveType == WaveType.Sine)
                    vertices[i].y = w.Sine(v);
                else if (waveType == WaveType.SteepSine)
                    vertices[i].y = w.SteepSine(v);

                Vector3 normal = new Vector3(0.0f, 1.0f, 0.0f);
                if (waveType == WaveType.Sine)
                    normal = w.SineNormal(v);
                else if (waveType == WaveType.SteepSine)
                    normal = w.SteepSineNormal(v);
                
                normals[i] = new Vector3(-normal.x, 1.0f, -normal.y);
                normals[i].Normalize();
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
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
