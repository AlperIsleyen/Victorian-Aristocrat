using UnityEditor;
using UnityEngine;
using System.IO;

public static class GameModelBuilder
{
    static readonly string[] Names = { "bay-horse", "dutch-canal-house", "timber-frame-house", "horse-cart", "shopkeeper-man", "shopkeeper-woman" };
    const string Source = "Assets/Game/Resources/Models/";

    public static void Inspect()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string name in Names)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Source + name + ".glb");
            if (asset == null) { Debug.LogError("[Models] Missing imported GameObject: " + name); continue; }
            var renderers = asset.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds();
            bool first = true;
            int triangles = 0;
            foreach (var renderer in renderers)
            {
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null) for (int s = 0; s < mesh.subMeshCount; s++) triangles += (int)mesh.GetIndexCount(s) / 3;
            }
            Debug.Log($"[Models] {name}: renderers={renderers.Length}, triangles={triangles}, bounds={bounds.size}, center={bounds.center}, shader={(renderers.Length > 0 ? renderers[0].sharedMaterial?.shader?.name : "none")}");
        }
    }

    public static void RenderPreviews()
    {
        Directory.CreateDirectory("Builds/ModelPreviews");
        foreach (string name in Names)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Source + name + ".glb");
            if (asset == null) continue;
            var model = Object.Instantiate(asset);
            Bounds bounds = new Bounds(); bool first = true;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            }
            var lightObject = new GameObject("Preview light");
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f;
            lightObject.transform.rotation = Quaternion.Euler(45, -30, 0);
            var cameraObject = new GameObject("Preview camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.backgroundColor = new Color(.30f, .34f, .35f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x, bounds.size.z) * .72f;
            camera.transform.position = bounds.center + new Vector3(bounds.size.x + 2, bounds.size.y * .5f + 1, -(bounds.size.z + 2));
            camera.transform.LookAt(bounds.center);
            var target = new RenderTexture(700, 700, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(700, 700, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 700, 700), 0, 0); image.Apply();
            File.WriteAllBytes("Builds/ModelPreviews/" + name + ".png", image.EncodeToPNG());
            Debug.Log("[Models] Preview saved: " + name);
            RenderTexture.active = null;
            Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lightObject); Object.DestroyImmediate(model);
        }
    }
}
