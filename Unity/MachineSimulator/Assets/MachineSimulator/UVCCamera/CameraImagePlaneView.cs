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
    public sealed class CameraImagePlaneView : MonoBehaviour
    {
        private const string ShaderName = "MachineSimulator/CameraImagePlane";

        // NOTE: Optional; falls back to the UVCCameraPlugin on this GameObject or one of its parents.
        [SerializeField] private UVCCameraPlugin _camera;
        [SerializeField] private bool _showImagePlane = true;
        // NOTE: Distance (m) of the image plane in front of the camera origin.
        [SerializeField, Min(0.001f)] private float _distanceFromCamera = 0.1f;

        private Transform _cameraTransform;
        private Transform _quad;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Material _material;
        private float _appliedDistance = -1f;

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

            // NOTE: The quad lives at the scene root and follows the camera in world space instead of being
            //       parented to it: the camera dummies in the Hexaplate prefab are scaled (0.01) and would
            //       scale the quad as well.
            var quadGameObject = new GameObject(name + " ImagePlane");
            _quad = quadGameObject.transform;
            quadGameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _renderer = quadGameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.enabled = false;
        }

        private void LateUpdate()
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

        private void OnDisable()
        {
            if (_renderer != null) _renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_quad != null) Destroy(_quad.gameObject);
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
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
