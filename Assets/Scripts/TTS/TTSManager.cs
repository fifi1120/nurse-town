using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine.Audio;
using System.Collections;
using Newtonsoft.Json;
using System.Text;
// for animation
using System.Text.RegularExpressions;

public class TTSManager : MonoBehaviour
{
    public static TTSManager Instance { get; private set; } // 单例实例

    public AudioSource audioSource; // Reference to the AudioSource where the speech will be played.

    private string openAIApiKey; // API key for OpenAI
    private static readonly string ttsEndpoint = "https://api.openai.com/v1/audio/speech"; // Endpoint for TTS
    private const bool deleteCachedFile = true; // Flag to determine if the audio file should be deleted after playing
    // for animation
    private CharacterAnimationController animationController;
    private BloodEffectController bloodEffectController;    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // If you need to maintain this during scene transitions, please uncomment the following line.
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Load the API key securely, for example, from environment variables or a secure storage
        openAIApiKey = EnvironmentLoader.GetEnvVariable("OPENAI_API_KEY");
        animationController = GetComponent<CharacterAnimationController>();
        
        // Find the blood effect in the UI
        bloodEffectController = GameObject.FindObjectOfType<BloodEffectController>();
        if (bloodEffectController == null)
        {
            Debug.LogError("BloodEffectController not found in the scene. Make sure it exists in the UI!");
        }
    }

    // Public method to be called to convert text to speech
    public async void ConvertTextToSpeech(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.Log("TTS Manager: No text provided for TTS");
            return;
        }

        // Strip emotion code for TTS but keep original text for animation
        string ttsText = text;
        Match match = Regex.Match(text, @"\[([0123456789])\]$");
        if (match.Success)
        {
            ttsText = text.Substring(0, text.Length - 3).Trim();
        }

        // Get audio data from OpenAI's TTS service
        byte[] audioData = await GetTTSAudio(ttsText, "tts-1", "nova", "mp3", 1.0f);
        if (audioData != null)
        {
            ProcessAudioBytes(audioData, text); // Pass original text for animation
        }
        else
        {
            Debug.LogError("TTS Manager: Failed to get audio data");
        }
    }

    // Method to get TTS audio from OpenAI
    private async Task<byte[]> GetTTSAudio(string inputText, string model, string voice, string responseFormat = "mp3", float speed = 1.0f)
    {
        using (HttpClient client = new HttpClient())
        {
            // Set the authorization header with the API key
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + openAIApiKey);
            // Debug.Log("TTS Manager: Sending TTS request");

            // Create the request body
            var requestBody = new TTSRequest
            {
                model = model,
                input = inputText,
                voice = voice,
                response_format = responseFormat,
                speed = speed
            };

            // Serialize the request body to JSON
            string jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Send the POST request
            HttpResponseMessage response = await client.PostAsync(ttsEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                // If the request is successful, read the byte array of the audio data
                // Debug.Log("TTS Manager: TTS request successful");
                return await response.Content.ReadAsByteArrayAsync();
            }
            else
            {
                // Log error if the request fails
                string errorResponse = await response.Content.ReadAsStringAsync();
                Debug.LogError("Error with TTS API: " + response.ReasonPhrase + "\nDetails: " + errorResponse);
                return null;
            }
        }
    }

    // Method to process and play the audio bytes received
    private void ProcessAudioBytes(byte[] audioData, string messageContent)
    {
        // Save the audio data as a .mp3 file locally
        string filePath = Path.Combine(Application.persistentDataPath, "audio.mp3");
        File.WriteAllBytes(filePath, audioData);

        // Start coroutine to load and play the audio file
        StartCoroutine(LoadAndPlayAudio(filePath, messageContent));
    }

    // Coroutine to load and play the audio file
    private IEnumerator LoadAndPlayAudio(string filePath, string messageContent)
    {
        // Create a UnityWebRequest to load the audio file
        using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.MPEG);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            // If the file is successfully loaded, get the audio clip and play it
            AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
            // Debug.Log("Audio clip loaded successfully");
            audioSource.clip = audioClip;
            audioSource.Play();
            UpdateAnimation(messageContent);
        }
        else
        {
            // Log error if the file loading fails
            Debug.LogError("Audio file loading error: " + www.error);
        }

        // Optionally delete the file after playing
        if (deleteCachedFile)
        {
            File.Delete(filePath);
        }
    }

    // Nested class to structure the TTS request body
    public class TTSRequest
    {
        public string model { get; set; }
        public string input { get; set; }
        public string voice { get; set; }
        public string response_format { get; set; }
        public float speed { get; set; }
    }

        private void UpdateAnimation(string message)
    {
        Match match = Regex.Match(message, @"\[([0123456789])\]$");
        if (match.Success)
        {
            int emotionCode = int.Parse(match.Groups[1].Value);
            switch(emotionCode)
            {
                case 0:
                    animationController.PlayIdle();
                    break;
                case 1:
                    animationController.PlayHeadPain();
                    Debug.Log("changing to pain");
                    break;
                case 2:
                    animationController.PlayHappy();
                    break;
                case 3:
                    animationController.PlayShrug();
                    break;
                case 4:
                    animationController.PlayHeadNod();
                    break;
                case 5:
                    animationController.PlayHeadShake();
                    break;
                case 6:
                    animationController.PlayWrithingInPain();
                    break;
                case 7:
                    animationController.PlaySad();
                    break;
                case 8:
                    bloodEffectController.SetBloodVisibility(true);
                    animationController.PlayArmStretch();
                    break;
                case 9:
                    animationController.PlayNeckStretch();
                    break;
            }
        }
        else
        {
            Debug.LogWarning($"No emotion code found: {message}");
            animationController.PlayIdle();
        }
    }
}