using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using App.Audio;

public class TTSHandler : MonoBehaviour
{
    public enum TTSOption
    {
        KokoroTTS,
        ElevenLabsTTS
    }

    [Header("Select Local TTS")]
    public TTSOption selectedTTS = TTSOption.KokoroTTS;
    public UnityEngine.UI.Image statusIndicator;

    [Header("TTS Speakers")]
    public KokoroTTSSpeaker kokoroTTSSpeaker;
    public ElevenLabsTTS elevenLabsTTS;

    private ITTSSpeaker TSSpeaker;

    void Awake()
    {
        TSSpeaker = GetSelectedSpeaker();

        if (selectedTTS == TTSOption.KokoroTTS)
        {
            if (kokoroTTSSpeaker != null) kokoroTTSSpeaker.enabled = true;
            if (elevenLabsTTS != null) elevenLabsTTS.enabled = false;
        }
        else if (selectedTTS == TTSOption.ElevenLabsTTS)
        {
            if (kokoroTTSSpeaker != null) kokoroTTSSpeaker.enabled = false;
            if (elevenLabsTTS != null) elevenLabsTTS.enabled = true;
            var streamAudioSource = GetComponent<SyncAudioSource>();
            if (streamAudioSource != null) streamAudioSource.enabled = true;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        TestTTSConnection();
    }

    private void TestTTSConnection()
    {
        TSSpeaker.TestConnection((isSuccess) => {
            if (statusIndicator != null)
            {
                statusIndicator.color = isSuccess ? Color.green : Color.red;
            }
        });
    }

    private ITTSSpeaker GetSelectedSpeaker()
    {
        switch (selectedTTS)
        {
            case TTSOption.KokoroTTS:
                return kokoroTTSSpeaker;
            case TTSOption.ElevenLabsTTS:
                return elevenLabsTTS; 
            default:
                return kokoroTTSSpeaker;
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Speak(string input, string language = "English")
    {
        TSSpeaker.Speak(input, language);
    }

    public bool waitingForAudioSynthesize()
    {
        return TSSpeaker.GetStatus() == ITTSSpeaker.AudioStatus.Playing;
    }

    public void ReportError()
    {
        TSSpeaker.Speak("I'm sorry, could you repeat that?");
    }

    public void CancelSpeak()
    {
        TSSpeaker.Stop();
        // sttHandler.NotifyTTSStopped();
    }
}