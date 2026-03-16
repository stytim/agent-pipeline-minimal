using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConversationHandler : MonoBehaviour
{
    [Header("Scripts")]
    public STTHandler sttHandler;
    public LLMHandler llmHandler;
    public TTSHandler ttsHandler;

    [Header("Settings")]
    public bool allowInterruptions = false;
    public string userRoleName = "Customer";

    [Header("Conversation")]
    public AudioSource audioSource;

    public Image statusIndicator; // UI element to show status
    private Coroutine conversationRoutine;
    private bool isAudioPlaying = false;

    // Start is called before the first frame update
    void Start()
    {
        // sttHandler.SetUserRoleName(userRoleName);
        AECAudioProcessor.Instance.ProcessAEC = allowInterruptions;

        if (sttHandler != null)
        {
            sttHandler.OnSpeechStartDetected.AddListener(HandleVoiceInterruption);
        }
    }

    // Update is called once per frame
    void Update()
    {


    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 140, 0, 140, 120));

        if (GUILayout.Button("Start Conversation"))
        {
            StartConversation();
        }

        if (GUILayout.Button("Stop Conversation"))
        {
            StopConversation();
        }

        GUILayout.EndArea();
    }

    public void TriggerConversation()
    {
        if (conversationRoutine == null)
        {
            llmHandler.Question("System: Start the Conversation");
            StartConversation();
            statusIndicator.color = Color.blue; 
        }
        else
        {
            StopConversation();
            statusIndicator.color = Color.white; 
        }
    }

    public void RedoLastRequest()
    {
        StopConversation();
        StartConversation();
        llmHandler.ReTry();

    }

    public void ToggleInterruptions()
    {
        allowInterruptions = !allowInterruptions;
        AECAudioProcessor.Instance.ProcessAEC = allowInterruptions;
    }
    private void StartConversation()
    {
        if ((sttHandler == null) || (llmHandler == null) || (audioSource == null) || conversationRoutine != null) return;

        conversationRoutine = StartCoroutine(ListeningRoutine());
    }

    private void StopConversation()
    {
        if (conversationRoutine != null)
        {
            StopCoroutine(conversationRoutine);
            conversationRoutine = null;
        }

        // _ = sttHandler.Disconnect();
        sttHandler.Mute();

        try
        {
            if (llmHandler != null)
            {
                llmHandler.CancelReply();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error cancelling LLM Handler reply: {ex.Message}");
        }

        // llmHandler.CancelReply();
        ttsHandler.CancelSpeak();

    }

    public void HandleVoiceInterruption()
    {
        if (!allowInterruptions) return;

        Debug.Log("Voice interruption detected.");
        if (llmHandler != null) llmHandler.CancelReply();
        if (ttsHandler != null) ttsHandler.CancelSpeak();
    }

    string GetTimeOfDay()
    {
        System.DateTime now = System.DateTime.Now;
        int hour = now.Hour;

        if (hour >= 5 && hour < 12) return "Morning";
        else if (hour >= 12 && hour < 17) return "Afternoon";
        else if (hour >= 17 && hour < 21) return "Evening";
        else return "Night";
    }

    string GetGreeting(string timeOfDay, string yourName, string myName, string template)
    {
        string result = string.Format("Good {0}, {1}. I'm {2}. {3}", timeOfDay, yourName, myName, template);

        return result;
    }

    private IEnumerator ListeningRoutine()
    {
        while (true)
        {
            if (allowInterruptions)
            {
               // If interruptions are allowed, STT should generally always be recording.
               // The interruption is handled when STT detects speech while TTS is active.
               sttHandler.Unmute();
            }
            else
            {
                Debug.Log($"audioSource.isPlaying: {audioSource.isPlaying}");
                Debug.Log($"llmHandler.waitingForReply: {llmHandler.waitingForReply}");
                Debug.Log($"ttsHandler.waitingForAudioSynthesize: {ttsHandler.waitingForAudioSynthesize()}");
                if (audioSource.isPlaying || llmHandler.waitingForReply || ttsHandler.waitingForAudioSynthesize())
                {
                    sttHandler.Mute();
                }
                else
                {
                    sttHandler.Unmute();
                }
            }

            yield return null;
        }
    }
}