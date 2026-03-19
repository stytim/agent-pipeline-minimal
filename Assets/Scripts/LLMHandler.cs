using LLMUnity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using Newtonsoft.Json;
using TMPro;

public class LLMHandler : MonoBehaviour
{
    [Header("Scripts")]
    public TTSHandler ttsHandler;

    [Header("LLM")]
    public LLMAgent llmCharacter;
    public bool waitingForReply = false;

    [Header("Status")]
    public Image statusIndicator; // UI element to show status
    bool isConnected = false;
    public TMP_Text llmResponseText;

    public TMP_Text subtitle;
    public float displayTime = 3f; // How long the subtitle stays fully visible
    public float fadeOutDuration = 0.5f;

    private Coroutine _activeFadeCoroutine;


    [Header("Test")]
    public string inputForLLM;
    public Texture2D imageForVLM;
    private DateTime startTime;
    float timer = 0.0f;
    public float timeout = 1.0f;
    private string latestQuestion;
    private string LatestResponse;

    private int retryCount = 1;
    private int currentRetryCount = 0;

    // Start is called before the first frame update
    void Start()
    {
       _ = llmCharacter.Warmup(WarmUpCallback);
    }

    // Update is called once per frame
    void Update()
    {
        // add timer when waitingForReply
        if (waitingForReply)
        {
            timer += Time.deltaTime;
            if (timer >= timeout)
            {
                Debug.LogWarning("LLM took too long");
                timer = 0.0f;
            }
        }
    }

    private void WarmUpCallback()
    {
        statusIndicator.color = Color.green;
    }

    [ContextMenu("Test LLM")]
    private void TestLLM()
    {
        Question(inputForLLM);
    }

    [ContextMenu("Test VLM")]
    private async void TestVLM()
    {
        byte[] jpg = imageForVLM.EncodeToJPG(90);
        string response = await llmCharacter.ChatWithImageBytes(
            inputForLLM,
            jpg,
            "image/jpeg"
        );
        ProcessLLMResponse(response);
    }

    public void ReTry()
    {
        Question(latestQuestion);
    }

    public async void Question(string inputString)
    {
        if (llmCharacter == null) return;

        latestQuestion = inputString;

        waitingForReply = true;
        startTime = DateTime.Now;

        string response = await llmCharacter.Chat(inputString,null, ReplyCompleted);
        ProcessLLMResponse(response);
    }
    public void ProcessLLMResponse(string response)
    {
        llmResponseText.text = response;

        var latency = DateTime.Now - startTime;
        Debug.Log($"LLM Latency: {latency.Milliseconds} ms.");
        //LogToFile(response);
        // Initialize default values
        string responseText = "";
        string command = "";

        try
        {
   
            var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
            if (jsonResponse != null)
            {
                jsonResponse.TryGetValue("responseText", out responseText);
                jsonResponse.TryGetValue("command", out command);
            }

            // Debug extracted values
            Debug.Log($"Parsed Response - Text: {responseText}, Command: {command}");

            // Process extracted values
            if (!string.IsNullOrEmpty(responseText))
            {
                
                HandlePatientReply(responseText);
            }

            if (!string.IsNullOrEmpty(command))
            {
                StartCoroutine(ExecuteCommandCoroutine(command));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error processing LLM response: {ex.Message}");
            ttsHandler.Speak("Sorry, could you repeat that?");
        }
    }


    void HandlePatientReply(string reply)
    {
        if (ttsHandler == null) return;

        //if (reply == LatestResponse) return;
        //LatestResponse = reply;

        Action<string, string> updateUIAndSpeak = (text, language) =>
        {
            if (subtitle != null)
            {
                subtitle.text = text; // Update subtitle text field
            }
            Debug.Log($"Input for Patient TTS: {text}");
            ttsHandler.Speak(text, language);
        };

        ShowSubtitle();
        updateUIAndSpeak(reply, "English");
        
    }

    public void ShowSubtitle()
    {
        if (subtitle != null)
        {
            subtitle.gameObject.SetActive(true); // Ensure the GameObject is active

            // Reset alpha to fully opaque
            Color subtitleColor = subtitle.color;
            subtitleColor.a = 1f; // 1 is fully opaque
            subtitle.color = subtitleColor;

            // If there's an old fade coroutine running, stop it
            if (_activeFadeCoroutine != null)
            {
                StopCoroutine(_activeFadeCoroutine);
            }

            // Start the new fade coroutine
            //_activeFadeCoroutine = StartCoroutine(FadeOutSubtitleAfterDelay());
        }
    }

    /// <summary>
    /// Public method to be called from another script to initiate the subtitle fade-out.
    /// </summary>
    public void TriggerSubtitleFadeOut()
    {
        // Check if the subtitle is active, has text, and is not already fading or fully transparent
        if (subtitle != null && subtitle.gameObject.activeInHierarchy && !string.IsNullOrEmpty(subtitle.text) && subtitle.color.a > 0)
        {
            if (_activeFadeCoroutine != null)
            {
                StopCoroutine(_activeFadeCoroutine); // Stop any existing fade if this is called again
            }
            _activeFadeCoroutine = StartCoroutine(FadeOutEffect());
        }
        else
        {
            if (subtitle != null && subtitle.color.a <= 0)
            {
                Debug.Log("Subtitle is already faded out or transparent. Fade-out trigger ignored.");
            }
            else
            {
                Debug.LogWarning("Cannot trigger subtitle fade-out: Subtitle is not active, has no text, or reference is missing.");
            }
        }
    }

    private IEnumerator FadeOutEffect()
    {
        // Fade out the subtitle
        float currentTime = 0f;
        Color startColor = subtitle.color; // Current color (should be opaque or partially faded if re-triggered)

        while (currentTime < fadeOutDuration)
        {
            currentTime += Time.deltaTime;
            // Calculate the new alpha value by linearly interpolating from current alpha to 0 (transparent)
            float alpha = Mathf.Lerp(startColor.a, 0f, currentTime / fadeOutDuration);

            subtitle.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null; // Wait for the next frame before continuing the loop
        }

        // Ensure alpha is exactly 0 at the end
        Color finalColor = subtitle.color;
        finalColor.a = 0f;
        subtitle.color = finalColor;

        // Optional: Deactivate the subtitle GameObject after it has faded out
        // subtitle.gameObject.SetActive(false);

        _activeFadeCoroutine = null; // Clear the reference to the completed coroutine
    }


    private IEnumerator FadeOutSubtitleAfterDelay()
    {
        // 1. Wait for the initial display time (3 seconds)
        yield return new WaitForSeconds(displayTime);

        // 2. Fade out the subtitle
        float currentTime = 0f;
        Color startColor = subtitle.color; // Should be fully opaque here

        while (currentTime < fadeOutDuration)
        {
            currentTime += Time.deltaTime;
            // Calculate the new alpha value by linearly interpolating from 1 (opaque) to 0 (transparent)
            float alpha = Mathf.Lerp(startColor.a, 0f, currentTime / fadeOutDuration);

            subtitle.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null; // Wait for the next frame before continuing the loop
        }

        // Ensure alpha is exactly 0 at the end
        subtitle.color = new Color(startColor.r, startColor.g, startColor.b, 0f);

        // Optional: Deactivate the subtitle GameObject after it has faded out
        subtitle.gameObject.SetActive(false);

        _activeFadeCoroutine = null; // Clear the reference to the completed coroutine
    }


    IEnumerator ExecuteCommandCoroutine(string command)
    {
        yield return null; // Ensures it does not block immediately
        ExecuteCommand(command);
    }

    public void ExecuteCommand(string command)
    {


    }


    void ReplyCompleted()
    {
        waitingForReply = false;
    }

    public void CancelReply()
    {
        try
        {
            llmCharacter.CancelRequests();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error cancelling LLM Handler reply: {ex.Message}");
        }
       
        waitingForReply = false;
    }
}
