// This PlanarReflection is originally from Unity BoatAttack demo's water system. (com.verasl.water-system)
// https://github.com/Unity-Technologies/boat-attack-water/blob/main/Runtime/Rendering/PlanarReflections.cs

using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using SpatialSys.UnitySDK;

// [ExecuteAlways]
public class PlanarReflection : MonoBehaviour
{
    [Serializable]
    public enum ResolutionMultiplier
    {
        Full,
        Half,
        Third,
        Quarter
    }

    [Header("Settings")]
    [SerializeField] private ResolutionMultiplier _resolutionMultiplier = ResolutionMultiplier.Third;
    private ResolutionMultiplier _resolutionMultiplierCached = ResolutionMultiplier.Third;
    [SerializeField] private float _clipPlaneOffset = 0.0f;
    [SerializeField] private LayerMask _reflectLayers = -1;
    [SerializeField] private bool _renderShadows = false;

    [Header("Target Plane")]
    [SerializeField, Tooltip("If it's null, plane pos will be zero and normal will be up")]
    private GameObject _targetPlane;
    [FormerlySerializedAs("camOffset")] public float _planeOffset;

    [Header("Shader Property")]
    [SerializeField] private string _planarReflectionTexturePropertyName = "_PlanarReflectionTexture";
    [SerializeField] private string _planarReflectionRightEyeTexturePropertyName = "_PlanarReflectionTextureRight"; // for VR
    [SerializeField] private string _cameraRectPropertyName = "_PlanarReflectionTextureRect";

    private int2 _cameraResolutionCached = new int2(0, 0);
    private const float RESOLUTION_CHECK_INTERVAL = 2f;
    private float _timeLastResolutionCheck = -RESOLUTION_CHECK_INTERVAL;
    private static Camera _reflectionCamera;
    private RenderTexture _reflectionTexture;
    private RenderTexture _reflectionTextureClone; // Only for webgl, to avoid warning - "GL_INVALID_OPERATION: Feedback loop formed between Framebuffer and active Texture."
    private RenderTexture _reflectionTextureRight; // for VR

#if !UNITY_EDITOR && UNITY_WEBGL
        private bool _isWebPlatform => true;
#else
    private bool _isWebPlatform => false;
#endif

    private bool _isVR = false;

    private void OnEnable()
    {
#if UNITY_EDITOR
        _isVR = false;
#else
        _isVR = SpatialBridge.actorService.localActor.platform == SpatialPlatform.MetaQuest;
#endif

        // Clear reflection textures to initialize.
        ClearRenderTextures();

        if (SpatialBridge.graphicsService != null)
        {
            SpatialBridge.graphicsService.beginMainCameraRendering += ExecutePlanarReflections;
        }
    }

    private void OnValidate()
    {
        // If resolution is changed, re-create and assign RenderTexture
        if (_resolutionMultiplier != _resolutionMultiplierCached)
        {
            _resolutionMultiplierCached = _resolutionMultiplier;
            ClearRenderTextures();
            CreateRenderTexturesIfNecessary();
        }
    }

    private void ClearRenderTextures()
    {
        if (_reflectionTexture)
        {
            RenderTexture.ReleaseTemporary(_reflectionTexture);
            _reflectionTexture = null;
        }
        if (_reflectionTextureClone)
        {
            RenderTexture.ReleaseTemporary(_reflectionTextureClone);
            _reflectionTextureClone = null;
        }
        if (_reflectionTextureRight)
        {
            RenderTexture.ReleaseTemporary(_reflectionTextureRight);
            _reflectionTextureRight = null;
        }
    }

    private void CreateRenderTexturesIfNecessary()
    {
        bool resolutionChanged = false;
        if (Time.time - _timeLastResolutionCheck > RESOLUTION_CHECK_INTERVAL)
        {
            _timeLastResolutionCheck = Time.time;
            int2 cameraResolution = GetCameraResolution();
            resolutionChanged = !Int2Compare(_cameraResolutionCached, cameraResolution);
            _cameraResolutionCached = cameraResolution;
        }
        if (resolutionChanged)
        {
            ClearRenderTextures();
        }
        if (resolutionChanged || _reflectionTexture == null)
        {
            _reflectionTexture = CreatePlanarReflectionTexture(_cameraResolutionCached);
        }
        if (_isWebPlatform && (resolutionChanged || _reflectionTextureClone == null))
        {
            _reflectionTextureClone = CreatePlanarReflectionTexture(_cameraResolutionCached);
        }
        if (_isVR && (resolutionChanged || _reflectionTextureRight == null))
        {
            _reflectionTextureRight = CreatePlanarReflectionTexture(_cameraResolutionCached);
        }
    }

    // Cleanup all the objects we possibly have created
    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        if (SpatialBridge.graphicsService != null)
        {
            SpatialBridge.graphicsService.beginMainCameraRendering -= ExecutePlanarReflections;
        }

        if (_reflectionCamera != null)
        {
            _reflectionCamera.targetTexture = null;
            SafeDestroy(_reflectionCamera.gameObject);
            _reflectionCamera = null;
        }
        ClearRenderTextures();
    }

    private static void SafeDestroy(Object obj)
    {
#if UNITY_EDITOR
        if (Application.isEditor)
        {
            DestroyImmediate(obj);
        }
        else
#endif
        {
            Destroy(obj);
        }
    }

    private void UpdateCamera(ICameraService cameraService, Camera dest)
    {
        if (dest == null)
            return;

        cameraService.CopyFromMainCamera(dest);
        dest.useOcclusionCulling = false;

        if (dest.gameObject.TryGetComponent(out UniversalAdditionalCameraData camData))
        {
            camData.renderShadows = _renderShadows; // turn off shadows for the reflection camera
        }
    }

    private void UpdateReflectionCamera(ICameraService cameraService, bool isStereo, Camera.StereoscopicEye eye)
    {
        if (_reflectionCamera == null)
            _reflectionCamera = CreateMirrorObjects();

        // find out the reflection plane: position and normal in world space
        Vector3 pos = this.transform.position;
        Vector3 normal = this.transform.up;
        if (_targetPlane != null)
        {
            pos = _targetPlane.transform.position + Vector3.up * _planeOffset;
            normal = _targetPlane.transform.up;
        }

        UpdateCamera(cameraService, _reflectionCamera);

        // Render reflection
        // Reflect camera around reflection plane
        var d = -Vector3.Dot(normal, pos) - _clipPlaneOffset;
        var reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

        // Get reflection Matrix.
        Matrix4x4 reflectionMatrix = Matrix4x4.identity;
        reflectionMatrix *= Matrix4x4.Scale(new Vector3(1, -1, 1));
        CalculateReflectionMatrix(ref reflectionMatrix, reflectionPlane);

        // Set reflection camera transform.
        Vector3 oldPosition = cameraService.position - new Vector3(0, pos.y * 2, 0);
        Vector3 newPosition = reflectionMatrix.MultiplyPoint(oldPosition);
        _reflectionCamera.transform.position = newPosition;

        Vector3 newForward = Vector3.Scale(cameraService.forward, new Vector3(1, -1, 1));
        _reflectionCamera.transform.forward = newForward;

        _reflectionCamera.cullingMask = _reflectLayers;

        // View Matrix (worldToCameraMatrix)
        Matrix4x4 oldViewMatrix = isStereo ? cameraService.GetStereoViewMatrix(eye) : cameraService.worldToCameraMatrix;
        Matrix4x4 newViewMatrix = oldViewMatrix * reflectionMatrix;
        _reflectionCamera.worldToCameraMatrix = newViewMatrix;

        // Setup oblique projection matrix so that near plane is our reflection
        // plane. This way we clip everything below/above it for free.
        var clipPlane = CameraSpacePlane(newViewMatrix, pos - Vector3.up * 0.1f, normal, 1.0f);

        // Projection Matrix
        Matrix4x4 oldProjectionMatrix = isStereo ? cameraService.GetStereoProjectionMatrix(eye) : cameraService.projectionMatrix;
        // oldProjectionMatrix = GL.GetGPUProjectionMatrix(oldProjectionMatrix, renderIntoTexture: true);
        Matrix4x4 newProjectionMatrix = CalculateObliqueMatrix(clipPlane, oldProjectionMatrix);
        _reflectionCamera.projectionMatrix = newProjectionMatrix;
    }

    private Matrix4x4 CalculateObliqueMatrix(Vector4 clipPlane, Matrix4x4 projection)
    {
        Vector4 oblique = clipPlane * (2.0F / (Vector4.Dot(clipPlane, projection.inverse * new Vector4(sgn(clipPlane.x), sgn(clipPlane.y), 1.0f, 1.0f))));
        projection[2] = oblique.x - projection[3];
        projection[6] = oblique.y - projection[7];
        projection[10] = oblique.z - projection[11];
        projection[14] = oblique.w - projection[15];
        return projection;
    }

    private static float sgn(float a) => a > 0.0f ? 1.0f : a < 0.0f ? -1.0f : 0.0f;

    // Calculates reflection matrix around the given plane
    private static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
    {
        reflectionMat.m00 = (1F - 2F * plane[0] * plane[0]);
        reflectionMat.m01 = (-2F * plane[0] * plane[1]);
        reflectionMat.m02 = (-2F * plane[0] * plane[2]);
        reflectionMat.m03 = (-2F * plane[3] * plane[0]);

        reflectionMat.m10 = (-2F * plane[1] * plane[0]);
        reflectionMat.m11 = (1F - 2F * plane[1] * plane[1]);
        reflectionMat.m12 = (-2F * plane[1] * plane[2]);
        reflectionMat.m13 = (-2F * plane[3] * plane[1]);

        reflectionMat.m20 = (-2F * plane[2] * plane[0]);
        reflectionMat.m21 = (-2F * plane[2] * plane[1]);
        reflectionMat.m22 = (1F - 2F * plane[2] * plane[2]);
        reflectionMat.m23 = (-2F * plane[3] * plane[2]);

        reflectionMat.m30 = 0F;
        reflectionMat.m31 = 0F;
        reflectionMat.m32 = 0F;
        reflectionMat.m33 = 1F;
    }

    private float GetScaleValue()
    {
        switch (_resolutionMultiplier)
        {
            case ResolutionMultiplier.Full:
                return 1f;
            case ResolutionMultiplier.Half:
                return 0.5f;
            case ResolutionMultiplier.Third:
                return 0.33f;
            case ResolutionMultiplier.Quarter:
                return 0.25f;
            default:
                return 0.5f; // default to half res
        }
    }

    // Compare two int2
    private static bool Int2Compare(int2 a, int2 b)
    {
        return a.x == b.x && a.y == b.y;
    }

    // Given position/normal of the plane, calculates plane in camera space.
    private Vector4 CameraSpacePlane(Matrix4x4 worldToCameraMatrix, Vector3 pos, Vector3 normal, float sideSign)
    {
        var offsetPos = pos + normal * _clipPlaneOffset;
        var cameraPosition = worldToCameraMatrix.MultiplyPoint(offsetPos);
        var cameraNormal = worldToCameraMatrix.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
    }

    private Camera CreateMirrorObjects()
    {
        // Clean Reflection Camera gameObjects.
        foreach (Transform tr in transform)
        {
            if (Application.isPlaying)
            {
                Destroy(tr.gameObject);
            }
#if UNITY_EDITOR
            else
            {
                DestroyImmediate(tr.gameObject);
            }
#endif
        }

        var go = new GameObject("Planar Reflection Camera", typeof(Camera));
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.parent = transform;

        var cameraData = go.AddComponent<UniversalAdditionalCameraData>();
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.SetRenderer(1);

        var reflectionCamera = go.GetComponent<Camera>();
        reflectionCamera.transform.SetPositionAndRotation(transform.position, transform.rotation);
        reflectionCamera.depth = -10;
        reflectionCamera.enabled = false;

        return reflectionCamera;
    }

    private RenderTexture CreatePlanarReflectionTexture(int2 cameraResolution)
    {
        float scale = GetScaleValue() * UniversalRenderPipeline.asset.renderScale;
        cameraResolution = new int2(Mathf.RoundToInt(cameraResolution.x * scale), Mathf.RoundToInt(cameraResolution.y * scale));
        bool useHdr10 = RenderingUtils.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float);
        RenderTextureFormat hdrFormat = useHdr10 ? RenderTextureFormat.RGB111110Float : RenderTextureFormat.DefaultHDR;
        return RenderTexture.GetTemporary(cameraResolution.x, cameraResolution.y, 16,
            GraphicsFormatUtility.GetGraphicsFormat(hdrFormat, true));
    }

    private int2 GetCameraResolution()
    {
        int w;
        int h;
        if (Application.isPlaying)
        {
            w = SpatialBridge.cameraService.pixelWidth;
            h = SpatialBridge.cameraService.pixelHeight;
        }
        else
        {
            w = Screen.width;
            h = Screen.height;
        }
        return new int2(w, h);
    }

    // private void ExecutePlanarReflections(ScriptableRenderContext context, Camera camera)
    private void ExecutePlanarReflections(ScriptableRenderContext context, CameraType cameraType)
    {
        // we dont want to render planar reflections in reflections or previews
        if (cameraType == CameraType.Reflection || cameraType == CameraType.Preview)
            return;

        var data = new PlanarReflectionSettingData(); // save quality settings and lower them for the planar reflections
        data.Set(); // set quality settings

        if (!_isVR)
        {
            RenderReflection(context, SpatialBridge.cameraService);
        }
        else
        {
            // Left
            RenderReflection(context, SpatialBridge.cameraService, _isVR, Camera.StereoscopicEye.Left);
            // Right
            RenderReflection(context, SpatialBridge.cameraService, _isVR, Camera.StereoscopicEye.Right);
        }

        data.Restore(); // restore the quality settings
    }

    private void RenderReflection(ScriptableRenderContext context, ICameraService cameraService, bool isStereo = false, Camera.StereoscopicEye eye = Camera.StereoscopicEye.Left)
    {
        // Create and update reflected camera
        UpdateReflectionCamera(cameraService, isStereo, eye);

        // Create and assign RenderTexture
        CreateRenderTexturesIfNecessary();

        bool isLeft = eye == Camera.StereoscopicEye.Left;
        _reflectionCamera.targetTexture = isLeft ? _reflectionTexture : _reflectionTextureRight;

        // Render
        UniversalRenderPipeline.RenderSingleCamera(context, _reflectionCamera); // render planar reflections

        Rect rect;
        if (Application.isPlaying)
        {
            rect = SpatialBridge.cameraService.rect;
        }
        else
        {
            rect = new Rect(0, 0, 1, 1);
        }
        Vector4 cameraRect = new Vector4(rect.x, rect.y, rect.width, rect.height);
        if (isLeft)
        {
            if (_isWebPlatform)
            {
                // Only for webgl, to avoid warning - "GL_INVALID_OPERATION: Feedback loop formed between Framebuffer and active Texture."
                Graphics.Blit(_reflectionTexture, _reflectionTextureClone);
                Shader.SetGlobalTexture(_planarReflectionTexturePropertyName, _reflectionTextureClone);
            }
            else
            {
                Shader.SetGlobalTexture(_planarReflectionTexturePropertyName, _reflectionTexture);
            }
        }
        else // right eye
        {
            Shader.SetGlobalTexture(_planarReflectionRightEyeTexturePropertyName, _reflectionTextureRight);
        }
        Shader.SetGlobalVector(_cameraRectPropertyName, cameraRect);
    }

    class PlanarReflectionSettingData
    {
        private readonly bool _fog;
        private readonly int _maxLod;
        private readonly float _lodBias;

        public PlanarReflectionSettingData()
        {
            _fog = RenderSettings.fog;
            _maxLod = QualitySettings.maximumLODLevel;
            _lodBias = QualitySettings.lodBias;
        }

        public void Set()
        {
            GL.invertCulling = true;
            RenderSettings.fog = false; // disable fog for now as it's incorrect with projection
        }

        public void Restore()
        {
            GL.invertCulling = false;
            RenderSettings.fog = _fog;
        }
    }
}
