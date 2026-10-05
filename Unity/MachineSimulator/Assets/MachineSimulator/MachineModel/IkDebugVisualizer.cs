using MachineSimulator.Ik;
using UnityEngine;
using UnityEngine.Rendering;

namespace MachineSimulator.MachineModel
{
    // NOTE: Renders the SphereCircleIntersectIK geometry (link1 circle, link2 sphere,
    //       plane-sphere intersection circle, solution highlights) as runtime meshes
    //       with custom SDF shaders, so it is visible in the Game view for recordings.
    public sealed class IkDebugVisualizer : MonoBehaviour
    {
        [SerializeField] private Material _link1CircleMaterial;
        [SerializeField] private Material _intersectionCircleMaterial;
        [SerializeField] private Material _sphereMaterial;

        // NOTE: Padding around each ring so line width + edge fade + highlight blobs
        //       never clip at the quad border (must exceed LineWidth/2 + EdgeFade + HighlightRadius).
        [SerializeField] private float _quadMargin = 0.05f;

        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int P1Id = Shader.PropertyToID("_P1");
        private static readonly int P2Id = Shader.PropertyToID("_P2");
        private static readonly int HighlightsOnId = Shader.PropertyToID("_HighlightsOn");

        private MeshRenderer _link1CircleRenderer;
        private MeshRenderer _intersectionCircleRenderer;
        private MeshRenderer _sphereRenderer;
        private MaterialPropertyBlock _propertyBlock;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _link1CircleRenderer = CreateChild(PrimitiveType.Quad, "IkViz_Link1Circle", _link1CircleMaterial);
            _intersectionCircleRenderer = CreateChild(PrimitiveType.Quad, "IkViz_IntersectionCircle", _intersectionCircleMaterial);
            _sphereRenderer = CreateChild(PrimitiveType.Sphere, "IkViz_Link2Sphere", _sphereMaterial);
        }

        // NOTE: All positions/radii are in the arm-root local space the IK solves in
        //       (the link1 circle lies in the local z = 0 plane, centered at the origin).
        public void UpdateVisualization(
            bool showLink1Circle,
            bool showLink2Sphere,
            bool showIntersectionCircle,
            bool showIntersectionPoints,
            Vector3 localTarget,
            float circleRadius,
            float sphereRadius,
            IkResult ikResult)
        {
            _link1CircleRenderer.enabled = showLink1Circle;
            _link1CircleRenderer.transform.localScale = Vector3.one * (2f * (circleRadius + _quadMargin));

            _sphereRenderer.enabled = showLink2Sphere;
            _sphereRenderer.transform.localPosition = localTarget;
            _sphereRenderer.transform.localScale = Vector3.one * (2f * sphereRadius);

            // The sphere cut by the circle plane (local z = 0) forms the intersection circle.
            var projectedRadiusSquared = sphereRadius * sphereRadius - localTarget.z * localTarget.z;
            var planeCutsSphere = projectedRadiusSquared > 1e-10f;
            var projectedRadius = planeCutsSphere ? Mathf.Sqrt(projectedRadiusSquared) : 0f;
            var intersectionCircleLocalCenter = new Vector3(localTarget.x, localTarget.y, 0f);

            _intersectionCircleRenderer.enabled = showIntersectionCircle && planeCutsSphere;
            if (planeCutsSphere)
            {
                _intersectionCircleRenderer.transform.localPosition = intersectionCircleLocalCenter;
                _intersectionCircleRenderer.transform.localScale = Vector3.one * (2f * (projectedRadius + _quadMargin));
            }

            // NOTE: The solver can report Success with NaN points (division by zero when
            //       sphereCenter.y == circleCenter.y), so finiteness is checked explicitly.
            var highlightsOn = showIntersectionPoints && ikResult.Success && IsFinite(ikResult.P1) && IsFinite(ikResult.P2);
            var worldP1 = transform.TransformPoint(ikResult.P1);
            var worldP2 = transform.TransformPoint(ikResult.P2);

            ApplyCircleProperties(_link1CircleRenderer, transform.position, circleRadius, worldP1, worldP2, highlightsOn);
            ApplyCircleProperties(_intersectionCircleRenderer, transform.TransformPoint(intersectionCircleLocalCenter),
                projectedRadius, worldP1, worldP2, highlightsOn);
        }

        private MeshRenderer CreateChild(PrimitiveType type, string childName, Material material)
        {
            var child = GameObject.CreatePrimitive(type);
            child.name = childName;
            Destroy(child.GetComponent<Collider>());

            // NOTE: Identity local pose puts the quads exactly in the arm-local z = 0 circle
            //       plane; the circle shader is Cull Off, so the quad facing is irrelevant.
            child.transform.SetParent(transform, false);

            var meshRenderer = child.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.enabled = false;
            return meshRenderer;
        }

        private void ApplyCircleProperties(
            MeshRenderer circleRenderer,
            Vector3 worldCenter,
            float radius,
            Vector3 worldP1,
            Vector3 worldP2,
            bool highlightsOn)
        {
            _propertyBlock.Clear();
            _propertyBlock.SetVector(CenterId, worldCenter);
            _propertyBlock.SetFloat(RadiusId, radius);
            _propertyBlock.SetVector(P1Id, worldP1);
            _propertyBlock.SetVector(P2Id, worldP2);
            _propertyBlock.SetFloat(HighlightsOnId, highlightsOn ? 1f : 0f);
            circleRenderer.SetPropertyBlock(_propertyBlock);
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
                   && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
                   && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }
    }
}
