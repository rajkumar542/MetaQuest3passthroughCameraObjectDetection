using UnityEngine;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using System.Collections.Generic;
using PassthroughCameraSamples;
using TMPro;
using System;
using System.Threading.Tasks;

public class PassthroughCameraDescription : MonoBehaviour
{
    [Header("Camera Settings")]
    public WebCamTextureManager webcamManager;
    
    [Header("OpenAI Settings")]
    public OpenAIConfiguration configuration;
    
    [Header("UI Settings")]
    public TextMeshProUGUI resultText;
    
    private Texture2D picture;
    private bool isProcessingImage = false;
    
    void Start()
    {
        if (resultText != null)
        {
            resultText.text = "Ready - Press A button to capture";
        }
        
        Debug.Log("PassthroughCameraDescription initialized");
    }
    
    void Update()
    {
        // Check if webcam is available
        if (webcamManager?.WebCamTexture == null || !webcamManager.WebCamTexture.isPlaying)
        {
            if (resultText != null && resultText.text == "Ready - Press A button to capture")
            {
                resultText.text = "Waiting for camera...";
            }
            return;
        }
        
        // Show ready message once camera is available
        if (resultText != null && resultText.text == "Waiting for camera...")
        {
            resultText.text = "Ready - Press A button to capture";
        }
        
        // Manual capture with VR controller A button (Button.One)
        if (!isProcessingImage && OVRInput.GetDown(OVRInput.Button.One))
        {
            Debug.Log("Manual capture triggered - A button pressed");
            CaptureAndDescribe();
        }
    }
    
    private void CaptureAndDescribe()
    {
        if (isProcessingImage)
        {
            Debug.Log("Already processing image, skipping...");
            return;
        }
        
        isProcessingImage = true;
        
        Debug.Log("Starting capture and describe process");
        
        if (resultText != null)
        {
            resultText.text = "Capturing image...";
        }
        
        if (TakePicture())
        {
            Debug.Log("Picture taken successfully, submitting to OpenAI");
            _ = SubmitImageAsync(); // Fire and forget async call
        }
        else
        {
            Debug.LogError("Failed to take picture");
            isProcessingImage = false;
            if (resultText != null)
            {
                resultText.text = "Failed to capture image";
            }
        }
    }
    
    private async Task SubmitImageAsync()
    {
        if (picture == null)
        {
            Debug.LogError("No picture to submit!");
            isProcessingImage = false;
            return;
        }
        
        try
        {
            Debug.Log("Starting image analysis...");
            
            if (resultText != null)
            {
                resultText.text = "Analyzing image...";
            }
            
            // Create OpenAI client
            var api = new OpenAIClient(configuration);
            Debug.Log("OpenAI client created");
            
            // Convert texture to base64
            Debug.Log($"Encoding image to JPEG... Image size: {picture.width}x{picture.height}");
            byte[] imageBytes = picture.EncodeToJPG(75);
            
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new Exception("Failed to encode image to JPEG");
            }
            
            Debug.Log($"Image encoded successfully. Size: {imageBytes.Length} bytes");
            string base64Image = Convert.ToBase64String(imageBytes);
            Debug.Log($"Base64 conversion complete. Length: {base64Image.Length}");
            
            // Create messages for OpenAI
            var messages = new List<Message>();
            
            // System message
            Message systemMessage = new Message(Role.System, 
                "Describe the main objects and scene in this image clearly and concisely in 20 words or less " +
                "Focus on the most prominent elements. Ignore hands, arms." + "and  Ignore VR controllers in the hand.");
            
            // User message with image
            var imageContent = new List<Content>
            {
                new Content(ContentType.Text, "What do you see in this image?"),
                new Content(ContentType.ImageUrl, $"data:image/jpeg;base64,{base64Image}")
            };
            
            Message imageMessage = new Message(Role.User, imageContent);
            
            messages.Add(systemMessage);
            messages.Add(imageMessage);
            
            Debug.Log("Messages prepared, sending to OpenAI...");
            
            // Make the API call
            var chatRequest = new ChatRequest(messages, Model.GPT4o);
            var result = await api.ChatEndpoint.GetCompletionAsync(chatRequest);
            
            Debug.Log("Response received from OpenAI");
            
            // Extract and display the result
            if (result?.FirstChoice?.Message?.Content != null)
            {
                string description = result.FirstChoice.Message.Content.ToString();
                Debug.Log($"Image Description: {description}");
                
                // Update UI on main thread
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    if (resultText != null)
                    {
                        resultText.text = description;
                    }
                });
            }
            else
            {
                throw new Exception("No valid response from OpenAI");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in SubmitImageAsync: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
            
            // Update UI on main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (resultText != null)
                {
                    resultText.text = $"Error: {e.Message}";
                }
            });
        }
        finally
        {
            isProcessingImage = false;
            Debug.Log("Image processing completed");
        }
    }
    
    public bool TakePicture()
    {
        try
        {
            if (webcamManager?.WebCamTexture == null || !webcamManager.WebCamTexture.isPlaying)
            {
                Debug.LogError("WebCam texture is not available!");
                return false;
            }
            
            int width = webcamManager.WebCamTexture.width;
            int height = webcamManager.WebCamTexture.height;
            
            Debug.Log($"Taking picture with dimensions: {width}x{height}");
            
            // Create or recreate texture if dimensions changed
            if (picture == null || picture.width != width || picture.height != height)
            {
                if (picture != null)
                    DestroyImmediate(picture);
                    
                picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                Debug.Log("Created new texture");
            }
            
            // Get pixels from webcam and apply to texture
            Color32[] pixels = webcamManager.WebCamTexture.GetPixels32();
            if (pixels == null || pixels.Length == 0)
            {
                Debug.LogError("Failed to get pixels from webcam");
                return false;
            }
            
            picture.SetPixels32(pixels);


            picture.Apply();
            
            Debug.Log($"Picture taken successfully: {width}x{height}, pixels: {pixels.Length}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error taking picture: {e.Message}");
            return false;
        }
    }
    
    // Public method to trigger capture manually from other scripts if needed
    public void ForceCaptureNow()
    {
        if (!isProcessingImage)
        {
            Debug.Log("Force capture requested");
            CaptureAndDescribe();
        }
        else
        {
            Debug.Log("Cannot force capture - already processing");
        }
    }
    
    void OnDestroy()
    {
        if (picture != null)
        {
            DestroyImmediate(picture);
        }
    }
}

// Helper class for main thread operations
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private Queue<System.Action> _executionQueue = new Queue<System.Action>();

    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            var go = new GameObject("MainThreadDispatcher");
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
        return _instance;
    }

    void Update()
    {
        while (_executionQueue.Count > 0)
        {
            _executionQueue.Dequeue().Invoke();
        }
    }

    public void Enqueue(System.Action action)
    {
        _executionQueue.Enqueue(action);
    }
}