using UnityEngine;
using PassthroughCameraSamples;

public class PassthroughCameraDisplay : MonoBehaviour
{
    public WebCamTextureManager webCamManager;
    public Renderer quadRenderer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (webCamManager.WebCamTexture != null)
        {
            quadRenderer.material.mainTexture = webCamManager.WebCamTexture;
        }
        
    }
}
