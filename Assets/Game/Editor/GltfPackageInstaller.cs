using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public static class GltfPackageInstaller
{
    static AddRequest request;
    static double deadline;

    public static void Install()
    {
        Debug.Log("[Victorian Aristocrat] Installing com.unity.cloud.gltfast");
        request = Client.Add("com.unity.cloud.gltfast");
        deadline = EditorApplication.timeSinceStartup + 600;
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (!request.IsCompleted)
        {
            if (EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            Debug.LogError("[Victorian Aristocrat] glTFast install timed out.");
            EditorApplication.Exit(2);
            return;
        }
        EditorApplication.update -= Poll;
        if (request.Status == StatusCode.Success)
        {
            Debug.Log("[Victorian Aristocrat] glTFast installed: " + request.Result.version);
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("[Victorian Aristocrat] glTFast install failed: " + request.Error?.message);
            EditorApplication.Exit(1);
        }
    }
}
