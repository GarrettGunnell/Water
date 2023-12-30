using UnityEngine;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;

public class Buoyancy : MonoBehaviour {
    public FFTWater waterSource;

    public enum SimulationType { Legitimate, PlaneFitApproximation };
    public SimulationType simulationType = SimulationType.Legitimate;

    [Header("Buoyancy Sim Settings")]
    [Space(5)]
    [Range(0.1f, 1.0f)]
    public float normalizedVoxelSize = 0.1f;

    public bool onlyReadbackBottomVoxels = false;

    [Range(0.0f, 1.0f)]
    public float minimumDrag = 0.0f;
    
    [Range(0.0f, 1.0f)]
    public float minimumAngularDrag = 0.05f;

    [Header("Fake Sim Settings")]
    [Space(5)]
    [Range(0.01f, 10.0f)]
    public float stiffness = 1.0f;

    [Range(0.01f, 10.0f)]
    public float angularStiffness = 1.0f;

    [Range(0.01f, 10.0f)]
    public float directionalStrength = 1.0f;

    [Range(0.01f, 5.0f)]
    public float angleThreshold = 1.5f;

    private Rigidbody rigidBody;
    private Collider cachedCollider;

    private Voxel[,,] voxels;
    private List<Vector3> receiverVoxels;
    private Vector3 voxelSize;
    private int voxelsPerAxis = 0;

    private Vector3 origin, direction;
    private Quaternion targetRotation;
    
    List<Vector3> points;
    Queue<Vector3> cachedDirections;
    
    private float velocity = 0.0f;

    private struct Voxel {
        public Vector3 position { get; }
        private float cachedBuoyancyData;
        private FFTWater waterSource;
        public bool isReceiver;

        private AsyncGPUReadbackRequest voxelWaterRequest;

        public Voxel(Vector3 position, FFTWater waterSource, bool isReceiver) {
            this.position = position;
            this.waterSource = waterSource;
            this.cachedBuoyancyData = 0.0f;
            this.isReceiver = isReceiver;

            if (isReceiver)
                voxelWaterRequest = AsyncGPUReadback.Request(waterSource.GetBuoyancyData(), 0, 0, 1, 0, 1, 0, 1, null);
            else voxelWaterRequest = new AsyncGPUReadbackRequest();
        }

        public float GetWaterHeight() {
            return cachedBuoyancyData;
        }

        public void Update(Transform parentTransform) {
            if (isReceiver && voxelWaterRequest.done) {
                if (voxelWaterRequest.hasError) {
                    Debug.Log("Height Error detected!");
                    return;
                }

                NativeArray<ushort> buoyancyDataQuery = voxelWaterRequest.GetData<ushort>();

                cachedBuoyancyData = Mathf.HalfToFloat(buoyancyDataQuery[0]);

                Vector3 worldPos = parentTransform.TransformPoint(this.position);
                Vector2 pos = new Vector2(worldPos.x, worldPos.z);
                Vector2 uv = worldPos * waterSource.tile1;

                int x = Mathf.FloorToInt(frac(uv.x) * 1023);
                int y = Mathf.FloorToInt(frac(uv.y) * 1023);

                voxelWaterRequest = AsyncGPUReadback.Request(waterSource.GetBuoyancyData(), 0, x, 1, y, 1, 0, 1, null);
            }
        }
    };

    // Largely referenced from https://github.com/dbrizov/NaughtyWaterBuoyancy/blob/master/Assets/NaughtyWaterBuoyancy/Scripts/Core/FloatingObject.cs
    private void CreateVoxels() {
        receiverVoxels = new List<Vector3>();

        Quaternion initialRotation = this.transform.rotation;
        this.transform.rotation = Quaternion.identity;

        Bounds bounds = this.cachedCollider.bounds;
        voxelSize = bounds.size * normalizedVoxelSize;
        voxelsPerAxis = Mathf.RoundToInt(1.0f / normalizedVoxelSize);
        voxels = new Voxel[voxelsPerAxis, voxelsPerAxis, voxelsPerAxis];

        for (int x = 0; x < voxelsPerAxis; ++x) {
            for (int y = 0; y < voxelsPerAxis; ++y) {
                for (int z = 0; z < voxelsPerAxis; ++z) {
                    Vector3 point = voxelSize;
                    point.Scale(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
                    point += bounds.min;

                    voxels[x,y,z] = new Voxel(this.transform.InverseTransformPoint(point), waterSource, onlyReadbackBottomVoxels ? y == 0 : true);

                    if (voxels[x,y,z].isReceiver) receiverVoxels.Add(new Vector3(x, y, z));
                }
            }
        }
    }


    void OnEnable() {
        if (waterSource == null) return;

        rigidBody = GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();
        targetRotation = Quaternion.identity;
        cachedDirections = new Queue<Vector3>();

        //CreateVoxels();
    }

    // Taken from https://github.com/zalo/MathUtilities/blob/master/Assets/LeastSquares/LeastSquaresFitting.cs
    public static class Fit {
        public static void Line(List<Vector3> points, out Vector3 origin, ref Vector3 direction, int iters = 100, bool drawGizmos = false) {
            if (direction == Vector3.zero || float.IsNaN(direction.x) || float.IsInfinity(direction.x)) direction = Vector3.up;

            //Calculate Average
            origin = Vector3.zero;
            for (int i = 0; i < points.Count; i++) origin += points[i];
            origin /= points.Count;

            // Step the optimal fitting line approximation:
            for (int iter = 0; iter < iters; iter++) {
                Vector3 newDirection = Vector3.zero;
                foreach (Vector3 worldSpacePoint in points) {
                    Vector3 point = worldSpacePoint - origin;
                    newDirection += Vector3.Dot(direction, point) * point;
                }
                direction = newDirection.normalized;
            }
        }

        public static void Plane(List<Vector3> points, out Vector3 position, out Vector3 normal, int iters = 200, bool drawGizmos = false) {
            //Find the primary principal axis
            Vector3 primaryDirection = Vector3.right;
            Line(points, out position, ref primaryDirection, iters / 2, false);

            //Flatten the points along that axis
            List<Vector3> flattenedPoints = new List<Vector3>(points);
            for (int i = 0; i < flattenedPoints.Count; i++)
                flattenedPoints[i] = Vector3.ProjectOnPlane(points[i] - position, primaryDirection) + position;

            //Find the secondary principal axis
            Vector3 secondaryDirection = Vector3.right;
            Line(flattenedPoints, out position, ref secondaryDirection, iters / 2, false);

            normal = Vector3.Cross(primaryDirection, secondaryDirection).normalized;

            if (drawGizmos) {
                Gizmos.color = Color.red;
                foreach (Vector3 point in points) Gizmos.DrawLine(point, Vector3.ProjectOnPlane(point - position, normal) + position);
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(position, normal * 0.5f); Gizmos.DrawRay(position, -normal * 0.5f);
                Gizmos.matrix = Matrix4x4.TRS(position, Quaternion.LookRotation(normal, primaryDirection), new Vector3(1f, 1f, 0.001f));
                Gizmos.DrawSphere(Vector3.zero, 1f);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }

    private Vector3 GetAverageDirection() {
        if (cachedDirections.Count == 0) return Vector3.up;

        Vector3 directionSum = Vector3.zero;
        Vector3[] directionArray = cachedDirections.ToArray();
        for (int i = 0; i < cachedDirections.Count; ++i) {
            directionSum += directionArray[i];
        }

        directionSum /= cachedDirections.Count;
        directionSum.Normalize();

        return directionSum;
    }

    void Update() {
        if (waterSource == null) return;
        if (voxels == null && waterSource.GetBuoyancyData() == null) return;
        if (voxels == null && waterSource.GetBuoyancyData() != null) CreateVoxels(); // Unity is being weird and sometimes randomly the FFT water returns a null rendertex when trying to create the voxels in OnEnable


        if (simulationType == SimulationType.PlaneFitApproximation && !rigidBody.isKinematic) rigidBody.isKinematic = true;
        if (simulationType == SimulationType.Legitimate && rigidBody.isKinematic) rigidBody.isKinematic = false;

        for (int i = 0; i < receiverVoxels.Count; ++i) {
            // This is probably the most ridiculous line of code I have ever written
            // but as far as I know C# doesn't let you store real references so instead
            // I store the indices of the voxels that read from the GPU that way we don't
            // waste time iterating through voxels that don't need to check their requests
            // it'd be smarter to just do a 1D array for all the voxels so that I store an int
            // instead of a vec3 but I love multidimensional arrays ok
            voxels[(int)receiverVoxels[i].x, (int)receiverVoxels[i].y, (int)receiverVoxels[i].z].Update(this.transform);
        }

        if (simulationType == SimulationType.PlaneFitApproximation) {
            points = new List<Vector3>();

            for (int i = 0; i < receiverVoxels.Count; ++i) {
                Vector3 pos = this.transform.TransformPoint(voxels[(int)receiverVoxels[i].x, (int)receiverVoxels[i].y, (int)receiverVoxels[i].z].position);
                pos.y = voxels[(int)receiverVoxels[i].x, (int)receiverVoxels[i].y, (int)receiverVoxels[i].z].GetWaterHeight();
                points.Add(pos);
            }
            
            Fit.Plane(points, out origin, out direction, 100, false);
            direction.y = 1;
            direction.x *= directionalStrength;
            direction.z *= directionalStrength;
            direction.Normalize();

            cachedDirections.Enqueue(direction);
            if (cachedDirections.Count > 10) cachedDirections.Dequeue();

            Vector3 avgDirection = GetAverageDirection();
            Quaternion avgRotation = Quaternion.FromToRotation(Vector3.up, avgDirection);

            float Fspring = -stiffness * origin.y;
            float a = Fspring / rigidBody.mass;

            velocity += a * Time.deltaTime;
            
            Vector3 position = this.transform.position;
            position.y = origin.y - velocity * Time.deltaTime;
            this.transform.position = position;

            float angularDistance = Quaternion.Angle(Quaternion.identity, avgRotation);
            float FxAngle = -angularStiffness * angularDistance;
            float aa = FxAngle / rigidBody.mass;
           
            float localAngularDistance = Quaternion.Angle(this.transform.rotation, avgRotation);
            if (localAngularDistance > angleThreshold)
                this.transform.rotation = Quaternion.RotateTowards(this.transform.rotation, avgRotation, Mathf.Max(0.0f, localAngularDistance - aa * Time.deltaTime) * 0.01f);
           
            
            // Vector3 fakePos = this.transform.position;
            // fakePos.y = voxels[0,0,0].GetWaterHeight();
            // this.transform.position = fakePos;
        }
    }

    void FixedUpdate() {
        if (voxels == null) return;

        if (simulationType == SimulationType.Legitimate) {
            float volume = cachedCollider.bounds.size.x * cachedCollider.bounds.size.y * cachedCollider.bounds.size.z;
            float density = rigidBody.mass / volume;

            float submergedVolume = 0.0f;
            float voxelHeight = cachedCollider.bounds.size.y * normalizedVoxelSize;

            float UnitForce = (1.0f - density) / voxels.Length;

            for (int x = 0; x < voxelsPerAxis; ++x) {
                for (int y = 0; y < voxelsPerAxis; ++y) {
                    for (int z = 0; z < voxelsPerAxis; ++z) {
                        Vector3 worldPos = this.transform.TransformPoint(voxels[x,y,z].position);

                        float waterLevel = voxels[x,y,z].isReceiver ? voxels[x,y,z].GetWaterHeight() : voxels[x,0,z].GetWaterHeight();
                        float depth = waterLevel - worldPos.y + voxelHeight;
                        float submergedFactor = Mathf.Clamp(depth / voxelHeight, 0.0f, 1.0f);
                        submergedVolume += submergedFactor;

                        float Displacement = Mathf.Max(0.0f, depth);

                        Vector3 F = -Physics.gravity * Displacement * UnitForce;
                        rigidBody.AddForceAtPosition(F, worldPos);
                    }
                }
            }

            submergedVolume /= voxels.Length;

            this.rigidBody.drag = Mathf.Lerp(minimumDrag, 1.0f, submergedVolume);
            this.rigidBody.angularDrag = Mathf.Lerp(minimumAngularDrag, 1.0f, submergedVolume);

            // Vector3 fakePos = this.transform.position;
            // fakePos.y = voxels[0,0,0].GetWaterHeight();
            // this.transform.position = fakePos;
        }
    }

    void OnDisable() {
        voxels = null;
    }

    private void OnDrawGizmos() {
        if (this.voxels != null) {
//            Fit.Plane(points, out origin, out direction, 50, true);

            for (int x = 0; x < voxelsPerAxis; ++x) {
                for (int y = 0; y < voxelsPerAxis; ++y) {
                    for (int z = 0; z < voxelsPerAxis; ++z) {
                        Gizmos.color = this.voxels[x,y,z].isReceiver ? Color.green : Color.red;
                        Gizmos.DrawCube(this.transform.TransformPoint(this.voxels[x,y,z].position), this.voxelSize * 0.8f);
                    }
                }
            }
        }
    }
}
