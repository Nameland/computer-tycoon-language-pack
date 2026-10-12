using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class MapPreviewSpec
    {
        internal readonly string Row, OriginalSprite, File;
        internal MapPreviewSpec(string row, string originalSprite, string file)
        {
            Row = row;
            OriginalSprite = originalSprite;
            File = file;
        }
    }
    private sealed class MapPreviewState
    {
        internal Image Image;
        internal Sprite Original;
        internal MapPreviewSpec Spec;
    }
    private sealed class MapPreviewAsset
    {
        internal int CheckedVersion = -1;
        internal long Length = -1, LastWriteTicks;
        internal Texture2D Texture;
        internal Sprite Sprite;
    }
    private static readonly MapPreviewSpec[] MapPreviewSpecs = {
        new("Title (1)", "world_200", "map-vanilla.png"),
        new("Title (2)", "historical", "map-historical.png"),
        new("Title (3)", "simplified", "map-simplified.png")
    };
    private static readonly Dictionary<int, MapPreviewState> MapPreviews = new();
    private static readonly Dictionary<string, MapPreviewAsset> MapPreviewAssets = new(StringComparer.OrdinalIgnoreCase);
    private static float _nextMapPreviewDiscovery;
    private static string _mapPreviewLanguage;
    private const int MaxMapPreviewBytes = 8 * 1024 * 1024;
    private const uint MaxMapPreviewDimension = 4096;

    // Called from the 0.25-second update branch. Discovery is limited to once per two seconds.
    // Only the three verified map-selector Images can receive optional language artwork.
    private static void RefreshMapPreviewLocalization()
    {
        if (Catalog is null)
            return;
        if (Time.unscaledTime >= _nextMapPreviewDiscovery)
        {
            _nextMapPreviewDiscovery = Time.unscaledTime + 2f;
            foreach (var candidate in DiscoverImagesThisFrame())
            {
                try
                {
                    if (!candidate || !candidate.gameObject.scene.IsValid() || candidate.name != "Image (1)")
                        continue;
                    var id = candidate.GetInstanceID();
                    if (MapPreviews.TryGetValue(id, out var tracked) && tracked.Image && tracked.Image.Pointer == candidate.Pointer)
                        continue;
                    if (!candidate.sprite)
                        continue;
                    var spec = MapPreviewSpecs.FirstOrDefault(s => candidate.sprite.name == s.OriginalSprite &&
                        HasContextPath(candidate, "DoozyUI", "UI Container", "MapSelector", "Main_pl", s.Row, "Image (1)"));
                    if (spec is null)
                        continue;
                    MapPreviews[id] = new MapPreviewState { Image = candidate, Original = candidate.sprite, Spec = spec };
                }
                catch (Exception error) { Report("discover localized map previews", error); }
            }
        }
        var language = Catalog.ActiveLanguageId;
        if (_mapPreviewLanguage != language)
        {
            // Detach before destroying Unity resources. Retain at most one language.
            foreach (var state in MapPreviews.Values)
                if (state.Image && state.Original)
                    state.Image.sprite = state.Original;
            foreach (var asset in MapPreviewAssets.Values)
                ClearMapPreviewAsset(asset);
            MapPreviewAssets.Clear();
            _mapPreviewLanguage = language;
        }
        var localized = !language.Equals("en", StringComparison.OrdinalIgnoreCase) && ValidMapPreviewLanguageId(language);
        foreach (var entry in MapPreviews.ToArray())
        {
            try
            {
                var state = entry.Value;
                if (!state.Image || !state.Original)
                {
                    MapPreviews.Remove(entry.Key);
                    continue;
                }
                if (!state.Image.gameObject.activeInHierarchy)
                    continue;
                var replacement = localized ? LoadMapPreviewAsset(language, state.Spec.File) : null;
                var target = replacement ? replacement : state.Original;
                if (!state.Image.sprite || state.Image.sprite.Pointer != target.Pointer)
                    state.Image.sprite = target;
            }
            catch (Exception error) { Report("refresh localized map previews", error); }
        }
    }

    private static bool ValidMapPreviewLanguageId(string id) => id.Length is >= 1 and <= 32 &&
        ((id[0] >= 'a' && id[0] <= 'z') || (id[0] >= 'A' && id[0] <= 'Z')) &&
        id.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-');

    private static Sprite LoadMapPreviewAsset(string language, string file)
    {
        var key = language + "/" + file;
        if (!MapPreviewAssets.TryGetValue(key, out var cached))
            MapPreviewAssets[key] = cached = new MapPreviewAsset();
        if (cached.CheckedVersion == _version)
            return cached.Sprite;
        cached.CheckedVersion = _version;
        Texture2D texture = null;
        Sprite sprite = null;
        try
        {
            // language is restricted to the same ASCII identifier grammar as LanguageCatalog;
            // file is one of the three constants above. No path is accepted from pack contents.
            var path = Path.Combine(Plugin.Instance.PluginDirectory, "Languages", language + "-assets", file);
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                ClearMapPreviewAsset(cached);
                return null;
            }
            if (cached.Sprite && cached.Length == info.Length && cached.LastWriteTicks == info.LastWriteTimeUtc.Ticks)
                return cached.Sprite;
            byte[] png;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < 33 || stream.Length > MaxMapPreviewBytes)
                    throw new InvalidDataException("Map preview PNG must be between 33 bytes and 8 MiB.");
                png = new byte[(int)stream.Length];
                var read = 0;
                while (read < png.Length)
                {
                    var count = stream.Read(png, read, png.Length - read);
                    if (count == 0)
                        throw new EndOfStreamException("Map preview PNG changed while reading.");
                    read += count;
                }
                if (stream.ReadByte() != -1)
                    throw new InvalidDataException("Map preview PNG changed while reading.");
            }
            var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (!png.AsSpan(0, 8).SequenceEqual(signature) || ReadMapPngUInt(png, 8) != 13 ||
                png[12] != 'I' || png[13] != 'H' || png[14] != 'D' || png[15] != 'R')
                throw new InvalidDataException("Map preview must have a PNG signature and initial IHDR chunk.");
            var width = ReadMapPngUInt(png, 16);
            var height = ReadMapPngUInt(png, 20);
            if (width == 0 || height == 0 || width > MaxMapPreviewDimension || height > MaxMapPreviewDimension)
                throw new InvalidDataException("Map preview dimensions must be between 1 and 4096 pixels.");
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(texture, new Il2CppStructArray<byte>(png), true) || texture.width != width || texture.height != height)
                throw new InvalidDataException("Unity could not decode map preview PNG with its declared dimensions.");
            texture.name = "LanguageMod:" + key;
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            if (!sprite)
                throw new InvalidDataException("Unity could not create map preview sprite.");
            sprite.name = "LanguageMod:" + key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            ClearMapPreviewAsset(cached);
            cached.Texture = texture;
            cached.Sprite = sprite;
            cached.Length = png.Length;
            cached.LastWriteTicks = info.LastWriteTimeUtc.Ticks;
            _log.LogInfo($"Loaded localized map preview: {key} ({width}x{height}).");
            return sprite;
        }
        catch (Exception error)
        {
            if (sprite)
                UnityEngine.Object.Destroy(sprite);
            if (texture)
                UnityEngine.Object.Destroy(texture);
            ClearMapPreviewAsset(cached);
            Report("load map preview " + key, error);
            return null;
        }
    }

    private static uint ReadMapPngUInt(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

    private static void ClearMapPreviewAsset(MapPreviewAsset asset)
    {
        if (asset.Sprite)
            UnityEngine.Object.Destroy(asset.Sprite);
        if (asset.Texture)
            UnityEngine.Object.Destroy(asset.Texture);
        asset.Sprite = null;
        asset.Texture = null;
        asset.Length = -1;
        asset.LastWriteTicks = 0;
    }
}
