using MachineSimulator.ImageProcessing;
using UnityEngine;
using UnityEngine.Rendering;
using c = MachineSimulator.Constants;

namespace MachineSimulator.UVCCamera
{
    // NOTE: Debug visualization for the ball tracking. Shows the (post-image-processing) camera stream on a
    //       quad that is the camera's image plane, _distanceFromCamera in front of the camera transform the
    //       Controller injects (the same transform the ball-direction gizmos start from).
    //       The quad is the pinhole image plane: 2 * distance * tan(fov / 2) wide/high (fov constants in
    //       Constants.cs), and the texture is mapped onto it with the exact pixel <-> direction conventions of
    //       BallDetection and Converter. So if detection and ray math agree, the Controller's yellow gizmo ray
    //       pierces the quad right at the ball image. With the legacy linear-angle model (Converter.ConvertToAngle)
    //       this holds exactly only at the image centre and borders; with Controller's pinhole projection toggle
    //       it holds everywhere.
    //       Optionally also draws the camera's frustum out to the image plane (no near/far plane): its four walls
    //       as a transparent mesh and/or its edges as gizmo lines, which can be grown from 0 FOV to the real FOV
    //       for recordings.
    public sealed class CameraImagePlaneView : MonoBehaviour
    {
        private const string ShaderName = "MachineSimulator/CameraImagePlane";
        private const string FrustumShaderName = "MachineSimulator/CameraFrustum";

        // NOTE: Optional; falls back to the UVCCameraPlugin on this GameObject or one of its parents.
        [SerializeField] private UVCCameraPlugin _camera;
        [SerializeField] private bool _showImagePlane = true;
        // NOTE: Distance (m) of the image plane in front of the camera origin. The frustum ends there as well.
        [SerializeField, Min(0.001f)] private float _distanceFromCamera = 0.1f;

        // NOTE: The frustum is independent of the quad: it only needs the camera pose, not a camera stream, so it
        //       also renders while the image plane is off or the camera is not running. Walls (transparent mesh)
        //       and edges (gizmo lines: Scene view, or Game view with gizmos on) are toggled separately.
        [SerializeField] private bool _showFrustumWalls;
        [SerializeField] private Color _frustumWallColor = new Color(1f, 1f, 1f, 0.3f);
        [SerializeField] private bool _showFrustumEdges;
        [SerializeField] private Color _frustumEdgeColor = Color.white;
        // NOTE: Multiply the FOV constants for the frustum only. At 1 the walls end exactly at the quad's edges;
        //       the grow animation below drives both from 0 to 1.
        [SerializeField, Min(0f)] private float _frustumHorizontalFovMultiplier = 1f;
        [SerializeField, Min(0f)] private float _frustumVerticalFovMultiplier = 1f;
        // NOTE: Tick during play mode (like the SingleArmMover animation toggles): the frustum grows from 0 FOV to
        //       the real FOV in _frustumGrowAnimationTime seconds, following _frustumGrowCurve (x: normalised time,
        //       y: multiplier). Walls and edges both follow; if neither is shown, the walls get switched on. The
        //       toggle switches itself off once the animation is done; unticking it earlier stops the animation at
        //       the multipliers it has reached.
        [SerializeField] private bool _playFrustumGrowAnimation;
        [SerializeField, Min(0.01f)] private float _frustumGrowAnimationTime = 2f;
        [SerializeField] private AnimationCurve _frustumGrowCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Transform _cameraTransform;
        private Transform _quad;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Material _material;
        private float _appliedDistance = -1f;

        private Transform _frustum;
        private MeshRenderer _frustumRenderer;
        private Mesh _frustumMesh;
        private Material _frustumMaterial;
        private float _appliedFrustumDistance = -1f;
        private float _appliedFrustumHorizontalFovMultiplier = -1f;
        private float _appliedFrustumVerticalFovMultiplier = -1f;

        private bool _isGrowAnimationRunning;
        private float _growAnimationTime;

        public float DistanceFromCamera => _distanceFromCamera;
        public bool IsVisible => _renderer != null && _renderer.enabled;

        // NOTE: The camera transform lives inside the Hexaplate prefab instance, which only exists at runtime,
        //       so it gets injected (Controller.InjectRefs) instead of being wired in the inspector.
        public void InjectRefs(Transform cameraTransform)
        {
            _cameraTransform = cameraTransform;
        }

        private void Awake()
        {
            if (_camera == null) _camera = GetComponentInParent<UVCCameraPlugin>();
            if (_camera == null) Debug.LogError(name + ": no UVCCameraPlugin assigned or found in the parents.", this);

            // NOTE: Shader.Find only works in the editor and for shaders included in a build; this is editor tooling.
            var shader = Shader.Find(ShaderName);
            if (shader == null) Debug.LogError(name + ": shader '" + ShaderName + "' not found.", this);

            _material = new Material(shader != null ? shader : Shader.Find("Unlit/Texture"));
            _mesh = new Mesh { name = "CameraImagePlane" };
            _renderer = CreateRootRenderer(name + " ImagePlane", _mesh, _material);
            _quad = _renderer.transform;

            var frustumShader = Shader.Find(FrustumShaderName);
            if (frustumShader == null) Debug.LogError(name + ": shader '" + FrustumShaderName + "' not found.", this);

            // NOTE: Sprites/Default as the fallback because it is alpha blended and two-sided as well.
            _frustumMaterial = new Material(frustumShader != null ? frustumShader : Shader.Find("Sprites/Default"));
            _frustumMesh = new Mesh { name = "CameraFrustum" };
            _frustumRenderer = CreateRootRenderer(name + " Frustum", _frustumMesh, _frustumMaterial);
            _frustum = _frustumRenderer.transform;
        }

        // NOTE: The quad and the frustum live at the scene root and follow the camera in world space instead of
        //       being parented to it: the camera dummies in the Hexaplate prefab are scaled (0.01) and would
        //       scale them as well.
        private static MeshRenderer CreateRootRenderer(string objectName, Mesh mesh, Material material)
        {
            var rendererObject = new GameObject(objectName);
            rendererObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            var meshRenderer = rendererObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;
            return meshRenderer;
        }

        private void LateUpdate()
        {
            UpdateImagePlane();
            UpdateFrustum();
        }

        private void UpdateImagePlane()
        {
            var texture = _camera != null && _camera.CameraIsInitialized ? _camera.Texture : null;
            var canShow = _showImagePlane && _cameraTransform != null && texture != null;

            _renderer.enabled = canShow;
            if (!canShow) return;

            if (_material.mainTexture != texture) _material.mainTexture = texture;
            if (!Mathf.Approximately(_appliedDistance, _distanceFromCamera)) RebuildMesh();

            // NOTE: The mesh already contains the forward offset (vertices sit at z = distance in camera space),
            //       so the quad transform just takes over the camera pose.
            _quad.SetPositionAndRotation(_cameraTransform.position, _cameraTransform.rotation);
        }

        private void UpdateFrustum()
        {
            HandleFrustumGrowAnimation();

            var canShow = _showFrustumWalls && _cameraTransform != null;

            _frustumRenderer.enabled = canShow;
            if (!canShow) return;

            if (_frustumMaterial.color != _frustumWallColor) _frustumMaterial.color = _frustumWallColor;
            if (!Mathf.Approximately(_appliedFrustumDistance, _distanceFromCamera)
                || !Mathf.Approximately(_appliedFrustumHorizontalFovMultiplier, _frustumHorizontalFovMultiplier)
                || !Mathf.Approximately(_appliedFrustumVerticalFovMultiplier, _frustumVerticalFovMultiplier))
            {
                RebuildFrustumMesh();
            }

            // NOTE: Same as the quad: the apex is the mesh origin, so the transform just takes over the camera pose.
            _frustum.SetPositionAndRotation(_cameraTransform.position, _cameraTransform.rotation);
        }

        // NOTE: Drives both FOV multipliers along the curve; the first frame sits at curve(0) (0 FOV), the last
        //       frame exactly at curve(1) (the full FOV with the default ease-in/out curve).
        private void HandleFrustumGrowAnimation()
        {
            if (_playFrustumGrowAnimation != _isGrowAnimationRunning)
            {
                _isGrowAnimationRunning = _playFrustumGrowAnimation;

                if (_isGrowAnimationRunning)
                {
                    _growAnimationTime = 0f;
                    if (!_showFrustumWalls && !_showFrustumEdges) _showFrustumWalls = true;
                }
            }

            if (!_isGrowAnimationRunning) return;

            var progress = Mathf.Clamp01(_growAnimationTime / _frustumGrowAnimationTime);
            var multiplier = Mathf.Max(0f, _frustumGrowCurve.Evaluate(progress));
            _frustumHorizontalFovMultiplier = multiplier;
            _frustumVerticalFovMultiplier = multiplier;

            if (progress >= 1f)
            {
                _isGrowAnimationRunning = false;
                _playFrustumGrowAnimation = false;
                return;
            }

            _growAnimationTime += Time.deltaTime;
        }

        private void OnDisable()
        {
            if (_renderer != null) _renderer.enabled = false;
            if (_frustumRenderer != null) _frustumRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_quad != null) Destroy(_quad.gameObject);
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);

            if (_frustum != null) Destroy(_frustum.gameObject);
            if (_frustumMesh != null) Destroy(_frustumMesh);
            if (_frustumMaterial != null) Destroy(_frustumMaterial);
        }

        // NOTE: Each corner of the texture is placed where the viewing ray of that image corner pierces the
        //       image plane (Converter's pinhole mapping), so the whole quad is consistent with the ray math.
        //       Vertex order by (u, v): (0,0) (1,0) (0,1) (1,1).
        private void RebuildMesh()
        {
            var vertices = new Vector3[4];
            var uvs = new Vector2[4];
            var normals = new Vector3[4];

            for (var i = 0; i < 4; i++)
            {
                var uv = new Vector2(i & 1, i >> 1);
                uvs[i] = uv;
                vertices[i] = Converter.ConvertToImagePlanePoint(ImagePositionFromUv(uv), _distanceFromCamera);
                normals[i] = Vector3.back;
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.uv = uvs;
            _mesh.normals = normals;
            // NOTE: Clockwise when looking along the camera's forward axis (the shader is Cull Off anyway).
            _mesh.triangles = new[] { 0, 1, 3, 0, 3, 2 };
            _mesh.RecalculateBounds();

            _appliedDistance = _distanceFromCamera;
        }

        // NOTE: Vertex order: apex at the camera origin, then the far corners in CalculateFrustumFarCorners' order.
        private void RebuildFrustumMesh()
        {
            var corners = CalculateFrustumFarCorners();

            _frustumMesh.Clear();
            _frustumMesh.vertices = new[] { Vector3.zero, corners[0], corners[1], corners[2], corners[3] };
            // NOTE: One triangle per wall (bottom, right, top, left), no near/far plane. All wound the same way
            //       around the forward axis; the shader is Cull Off anyway.
            _frustumMesh.triangles = new[] { 0, 1, 2, 0, 2, 4, 0, 4, 3, 0, 3, 1 };
            _frustumMesh.RecalculateBounds();

            _appliedFrustumDistance = _distanceFromCamera;
            _appliedFrustumHorizontalFovMultiplier = _frustumHorizontalFovMultiplier;
            _appliedFrustumVerticalFovMultiplier = _frustumVerticalFovMultiplier;
        }

        // NOTE: The edges are gizmos like the Controller's camera rays. They use the same corners as the walls, so
        //       they follow the multipliers and the grow animation too: the four lateral edges from the camera
        //       origin to the corners plus the four edges of the far rectangle (the image plane's border).
        //       position + rotation * corner instead of TransformPoint on purpose: the camera dummies are scaled
        //       (0.01), see CreateRootRenderer.
        private void OnDrawGizmos()
        {
            if (!_showFrustumEdges || !enabled || _cameraTransform == null) return;

            var apex = _cameraTransform.position;
            var rotation = _cameraTransform.rotation;
            var corners = CalculateFrustumFarCorners();
            for (var i = 0; i < corners.Length; i++) corners[i] = apex + rotation * corners[i];

            Gizmos.color = _frustumEdgeColor;
            foreach (var corner in corners) Gizmos.DrawLine(apex, corner);
            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[3]);
            Gizmos.DrawLine(corners[3], corners[2]);
            Gizmos.DrawLine(corners[2], corners[0]);
        }

        // NOTE: Camera-space corners of the frustum's far end, where the image plane's corners sit for the
        //       (multiplied) fields of view: the same distance * tan(fov / 2) pinhole mapping as
        //       Converter.ConvertToImagePlanePoint, so with both multipliers at 1 the frustum ends exactly at the
        //       quad's edges. x (right) spans the horizontal FOV, y (up) the vertical one. Order by (right, up)
        //       sign: (-,-) (+,-) (-,+) (+,+).
        private Vector3[] CalculateFrustumFarCorners()
        {
            var distance = _distanceFromCamera;
            var halfWidth = distance * TanHalfFov(c.CameraHorizontalFov * _frustumHorizontalFovMultiplier);
            var halfHeight = distance * TanHalfFov(c.CameraVerticalFov * _frustumVerticalFovMultiplier);

            return new[]
            {
                new Vector3(-halfWidth, -halfHeight, distance),
                new Vector3(halfWidth, -halfHeight, distance),
                new Vector3(-halfWidth, halfHeight, distance),
                new Vector3(halfWidth, halfHeight, distance),
            };
        }

        // NOTE: Clamped just below 180deg; a pinhole image plane only exists for fields of view below that
        //       (multipliers above ~3.4 horizontally / ~1.9 vertically would otherwise blow the mesh up).
        private static float TanHalfFov(float fovDegrees)
        {
            return Mathf.Tan(Mathf.Clamp(fovDegrees, 0f, 179f) / 2f * Mathf.Deg2Rad);
        }

        // NOTE: Inverse of how BallDetection reports positions (PositionX = width / 2 - column,
        //       PositionY = height / 2 - row). Texture u runs along the columns and v along the rows: Unity
        //       texture rows start at the bottom, matching the raw buffer order UVCCameraPlugin uploads.
        private static Vector2 ImagePositionFromUv(Vector2 uv)
        {
            return new Vector2(
                c.CameraResolutionWidth / 2f - uv.x * c.CameraResolutionWidth,
                c.CameraResolutionHeight / 2f - uv.y * c.CameraResolutionHeight);
        }
    }
}
