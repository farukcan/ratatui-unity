// Minimal UnityEngine stubs used ONLY by DocFX metadata extraction.
// Provides just enough type surface so that Runtime/*.cs compiles
// during `docfx metadata` (which has no access to UnityEngine.dll).
// These stubs are NOT shipped, never executed, and filtered out of
// the generated API reference via filterConfig.yml.

using System;

namespace UnityEngine
{
    public enum HideFlags
    {
        None = 0,
        HideInHierarchy = 1,
        HideInInspector = 2,
        DontSaveInEditor = 4,
        NotEditable = 8,
        DontSaveInBuild = 16,
        DontUnloadUnusedAsset = 32,
        DontSave = DontSaveInEditor | DontSaveInBuild | DontUnloadUnusedAsset,
        HideAndDontSave = HideInHierarchy | HideInInspector | DontSaveInEditor | DontSaveInBuild | DontUnloadUnusedAsset
    }

    public class Object
    {
        public HideFlags hideFlags { get; set; }
        public static void Destroy(Object obj) { }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; }
    }

    public class GameObject : Object
    {
        public Transform transform { get; }
        public string name { get; set; }
        public bool activeSelf { get; }
        public void SetActive(bool active) { }
        public T GetComponent<T>() => default;
    }

    public class Transform : Object
    {
        public Vector2 position { get; set; }
    }

    public class MonoBehaviour : Object
    {
        public GameObject gameObject { get; }
        public Transform transform { get; }
    }

    public struct Vector2
    {
        public float x;
        public float y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => default;
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static bool operator ==(Vector2 lhs, Vector2 rhs) => lhs.x == rhs.x && lhs.y == rhs.y;
        public static bool operator !=(Vector2 lhs, Vector2 rhs) => !(lhs == rhs);
        public override bool Equals(object obj) => obj is Vector2 other && this == other;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    public struct Ray
    {
        public Vector3 origin;
        public Vector3 direction;
        public Ray(Vector3 origin, Vector3 direction) { this.origin = origin; this.direction = direction; }
    }

    public struct RaycastHit
    {
        public Collider collider;
        public Vector2 textureCoord;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color black => default;
        public static Color white => default;
        public static Color red => default;
        public static Color green => default;
        public static Color blue => default;
        public static Color yellow => default;
        public static Color cyan => default;
        public static Color magenta => default;
        public static Color clear => default;
    }

    public struct Quaternion
    {
        public static Quaternion identity => default;
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => default;
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Vector2 position { get; set; }
        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            position = new Vector2(x, y);
        }
        public bool Contains(Vector2 point) => false;
    }

    public struct Resolution
    {
        public int width;
        public int height;
        public int refreshRate;
    }

    public enum TextureFormat
    {
        Alpha8 = 1, ARGB4444 = 2, RGB24 = 3, RGBA32 = 4, ARGB32 = 5, RGB565 = 7,
        R16 = 9, DXT1 = 10, DXT5 = 12, RGBA4444 = 13, BGRA32 = 14
    }

    public enum FilterMode { Point = 0, Bilinear = 1, Trilinear = 2 }

    public enum TextureWrapMode { Repeat = 0, Clamp = 1, Mirror = 2, MirrorOnce = 3 }

    public class Texture : Object
    {
        public int width { get; }
        public int height { get; }
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
    }

    public class Texture2D : Texture
    {
        public Texture2D(int width, int height) { }
        public Texture2D(int width, int height, TextureFormat format, bool mipChain) { }
        public Texture2D(int width, int height, TextureFormat format, bool mipChain, bool linear) { }
        public void LoadRawTextureData(byte[] data) { }
        public void LoadRawTextureData(IntPtr data, int size) { }
        public void Apply() { }
        public void Apply(bool updateMipmaps) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
        public void SetPixels(Color[] colors) { }
        public void SetPixel(int x, int y, Color color) { }
        public byte[] GetRawTextureData() => Array.Empty<byte>();
    }

    public class Material : Object
    {
        public Texture mainTexture { get; set; }
        public Vector2 mainTextureScale { get; set; }
        public Vector2 mainTextureOffset { get; set; }
    }

    public class Renderer : MonoBehaviour
    {
        public Material material { get; set; }
        public new GameObject gameObject { get; }
    }

    public class RectTransform : Transform
    {
        public Rect rect { get; }
        public Vector2 sizeDelta { get; set; }
    }

    public enum RenderMode
    {
        ScreenSpaceOverlay,
        ScreenSpaceCamera,
        WorldSpace
    }

    public class Canvas : MonoBehaviour
    {
        public static void ForceUpdateCanvases() { }
        public RenderMode renderMode { get; }
        public Camera worldCamera { get; }
    }

    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(
            RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint)
        {
            localPoint = default;
            return false;
        }
    }

    public enum TextAnchor
    {
        UpperLeft, UpperCenter, UpperRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        LowerLeft, LowerCenter, LowerRight
    }

    public enum TextClipping { Overflow, Clip }

    public enum FontStyle
    {
        Normal,
        Bold,
        Italic,
        BoldAndItalic
    }

    public class Font : Object { }

    public enum ScaleMode
    {
        StretchToFill, ScaleAndCrop, ScaleToFit
    }

    public class GUIStyleState
    {
        public Color textColor { get; set; }
    }

    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
        public TextAnchor alignment { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextClipping clipping { get; set; }
        public bool wordWrap { get; set; }
        public GUIStyleState normal { get; } = new GUIStyleState();
    }

    public class GUISkin
    {
        public GUIStyle label { get; } = new GUIStyle();
    }

    public class Event
    {
        public static Event current { get; } = new Event();
        public EventType type { get; set; }
        public int button { get; set; }
        public Vector2 mousePosition { get; set; }
        public void Use() { }
    }

    public enum EventType
    {
        MouseDown, MouseUp, MouseDrag, MouseMove, KeyDown, KeyUp, Repaint, Layout
    }

    public static class GUI
    {
        public static Color color { get; set; }
        public static GUISkin skin { get; } = new GUISkin();
        public static void DrawTexture(Rect position, Texture image) { }
        public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode, bool alphaBlend) { }
        public static void DrawTextureWithTexCoords(Rect position, Texture image, Rect texCoords, bool alphaBlend) { }
        public static void Label(Rect position, string text, GUIStyle style) { }
    }

    public class Collider : Component { }

    public class MeshCollider : Collider
    {
        public bool convex { get; set; }
    }

    public static class Physics
    {
        public static bool Raycast(Ray ray, out RaycastHit hit) { hit = default; return false; }
    }

    public class Camera : MonoBehaviour
    {
        public static Camera main { get; }
        public Ray ScreenPointToRay(Vector3 position) => default;
        public Ray ScreenPointToRay(Vector2 position) => default;
    }

    public static class Mathf
    {
        public static int RoundToInt(float v) => 0;
        public static int FloorToInt(float v) => 0;
        public static int CeilToInt(float v) => 0;
        public static float Abs(float v) => 0;
        public static int Abs(int v) => 0;
        public static float Clamp(float v, float min, float max) => 0;
        public static int Clamp(int v, int min, int max) => 0;
        public static float Clamp01(float v) => 0;
        public static float Max(float a, float b) => 0;
        public static int Max(int a, int b) => 0;
        public static float Min(float a, float b) => 0;
        public static int Min(int a, int b) => 0;
        public static float Sign(float v) => 0;
        public static float Sqrt(float v) => 0;
    }

    public static class Time
    {
        public static float deltaTime;
        public static float unscaledDeltaTime;
        public static float unscaledTime;
        public static float realtimeSinceStartup;
        public static int frameCount;
        public static float timeScale;
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogWarning(object message, Object context) { }
        public static void LogError(object message) { }
        public static void LogException(Exception e) { }
    }

    public static class Random
    {
        public static float value => 0;
        public static float Range(float min, float max) => 0;
        public static int Range(int min, int max) => 0;
    }

    public static class Screen
    {
        public static int width;
        public static int height;
        public static Resolution currentResolution;
    }

    public enum RuntimePlatform
    {
        OSXEditor, OSXPlayer, WindowsPlayer, WindowsEditor, IPhonePlayer, Android,
        LinuxPlayer, LinuxEditor, WebGLPlayer
    }

    public enum NetworkReachability { NotReachable, ReachableViaCarrierDataNetwork, ReachableViaLocalAreaNetwork }
    public enum LogType { Error, Assert, Warning, Log, Exception }

    public delegate void Application_LogCallback(string condition, string stackTrace, LogType type);

    public static class Application
    {
        public static string unityVersion;
        public static string version;
        public static string productName;
        public static int targetFrameRate;
        public static bool isEditor;
        public static bool isPlaying;
        public static bool isMobilePlatform;
        public static RuntimePlatform platform;
        public static NetworkReachability internetReachability;
        public static event Action quitting { add { } remove { } }
        public static event Application_LogCallback logMessageReceivedThreaded { add { } remove { } }
        public static void OpenURL(string url) { }
        public static void Quit() { }
    }

    public enum KeyCode
    {
        None = 0, Backspace = 8, Tab = 9, Return = 13, Escape = 27, Space = 32,
        BackQuote = 96, Tilde = 126,
        Delete = 127,
        Alpha0 = 48, Alpha1 = 49, Alpha2 = 50, Alpha3 = 51, Alpha4 = 52,
        Alpha5 = 53, Alpha6 = 54, Alpha7 = 55, Alpha8 = 56, Alpha9 = 57,
        A = 97, B = 98, C = 99, D = 100, E = 101, F = 102, G = 103, H = 104,
        I = 105, J = 106, K = 107, L = 108, M = 109, N = 110, O = 111, P = 112,
        Q = 113, R = 114, S = 115, T = 116, U = 117, V = 118, W = 119, X = 120,
        Y = 121, Z = 122,
        UpArrow = 273, DownArrow = 274, RightArrow = 275, LeftArrow = 276,
        Home = 278, End = 279, PageUp = 280, PageDown = 281,
        F1 = 282, F2 = 283, F3 = 284, F4 = 285, F5 = 286, F6 = 287, F7 = 288,
        F8 = 289, F9 = 290, F10 = 291, F11 = 292, F12 = 293,
        Keypad0 = 256, Keypad1 = 257, Keypad2 = 258, Keypad3 = 259, Keypad4 = 260,
        Keypad5 = 261, Keypad6 = 262, Keypad7 = 263, Keypad8 = 264, Keypad9 = 265,
        KeypadEnter = 271,
        LeftShift = 304, RightShift = 303, LeftControl = 306, RightControl = 305,
        LeftAlt = 308, RightAlt = 307,
        LeftCommand = 310, RightCommand = 309,
        LeftApple = 310, RightApple = 309,
        LeftWindows = 311, RightWindows = 312
    }

    public static class Input
    {
        public static bool GetKey(KeyCode key) => false;
        public static bool GetKeyDown(KeyCode key) => false;
        public static bool GetKeyUp(KeyCode key) => false;
        public static bool GetMouseButtonDown(int button) => false;
        public static bool GetMouseButtonUp(int button) => false;
        public static bool GetMouseButton(int button) => false;
        public static string inputString => string.Empty;
        public static Vector2 mousePosition => default;
        public static Vector2 mouseScrollDelta => default;
        public static int touchCount => 0;
    }

    public static class GUIUtility
    {
        public static string systemCopyBuffer { get; set; }
    }

    public enum TouchScreenKeyboardType
    {
        Default,
        ASCIICapable,
        NumbersAndPunctuation,
        URL,
        NumberPad,
        PhonePad,
        NamePhonePad,
        EmailAddress,
        NintendoNetworkAccount,
        Social,
        Search,
        DecimalPad,
        OneTimeCode,
    }

    public struct RangeInt
    {
        public int start;
        public int length;
    }

    public sealed class TouchScreenKeyboard
    {
        public enum Status
        {
            Hidden,
            Done,
            Canceled,
            LostFocus,
        }

        public static bool isSupported => false;
        public static bool visible => false;
        public bool active { get; set; }
        public string text { get; set; }
        public RangeInt selection { get; set; }
        public Status status { get; set; }

        public static TouchScreenKeyboard Open(
            string text,
            TouchScreenKeyboardType keyboardType,
            bool autocorrection,
            bool multiline,
            bool secure,
            bool alert,
            string textPlaceholder,
            int characterLimit)
        {
            return new TouchScreenKeyboard();
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeFieldAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class HideInInspectorAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class RequireComponentAttribute : Attribute
    {
        public RequireComponentAttribute(Type t) { }
        public RequireComponentAttribute(Type t0, Type t1) { }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class DefaultExecutionOrderAttribute : Attribute
    {
        public int order { get; }
        public DefaultExecutionOrderAttribute(int order) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header) { }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SpaceAttribute : Attribute
    {
        public SpaceAttribute() { }
        public SpaceAttribute(float height) { }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class ContextMenuAttribute : Attribute
    {
        public ContextMenuAttribute(string name) { }
    }

    public enum RuntimeInitializeLoadType
    {
        AfterSceneLoad,
        BeforeSceneLoad,
        AfterAssembliesLoaded,
        BeforeSplashScreen,
        SubsystemRegistration
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }
}

namespace UnityEngine.Serialization
{
    [AttributeUsage(AttributeTargets.Field)]
    public class FormerlySerializedAsAttribute : Attribute
    {
        public FormerlySerializedAsAttribute(string oldName) { }
    }
}

namespace UnityEngine.UI
{
    public class RawImage : MonoBehaviour
    {
        public Texture texture { get; set; }
        public RectTransform rectTransform { get; }
        public Rect uvRect { get; set; }
        public Canvas canvas { get; }
    }
}

namespace UnityEngine.Events
{
    public class UnityEventBase { }
    public class UnityEvent : UnityEventBase
    {
        public void Invoke() { }
        public void AddListener(Action action) { }
        public void RemoveListener(Action action) { }
        public void RemoveAllListeners() { }
    }
    public class UnityEvent<T0> : UnityEventBase
    {
        public void Invoke(T0 arg0) { }
        public void AddListener(Action<T0> action) { }
        public void RemoveListener(Action<T0> action) { }
        public void RemoveAllListeners() { }
    }
}
