using UnityEngine;

[DefaultExecutionOrder(-100)]
public class CameraSetup : MonoBehaviour
{
    private static bool isInitialized = false;

    private void Awake()
    {
        SetupCamera();
        isInitialized = true;
    }

    private void SetupCamera()
    {
        Camera cam = GetComponent<Camera>();
        cam.orthographic = true;

        // Set reference resolution (2436x1125)
        float targetWidth = 1125f;
        float targetHeight = 2436f;
        float targetAspect = targetWidth / targetHeight;

        // Get current aspect ratio
        float currentAspect = (float)Screen.width / Screen.height;

        // Base ortho size (half height)
        float baseOrthoSize = 10f;

        if (currentAspect >= targetAspect)
        {
            // Screen is wider, adjust size based on height
            cam.orthographicSize = baseOrthoSize;
        }
        else
        {
            // Screen is taller, maintain width
            cam.orthographicSize = baseOrthoSize * (targetAspect / currentAspect);
        }

        Debug.Log($"Camera Setup - Ortho Size: {cam.orthographicSize}, Aspect: {currentAspect}");
    }

    public static bool IsCameraReady()
    {
        return isInitialized && Camera.main != null;
    }
}