using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TTSHandler : MonoBehaviour
{
    public enum TTSOption
    {
        KokoroTTS,
    }

    [Header("Select Local TTS")]
    public TTSOption selectedLocalTTS = TTSOption.KokoroTTS;

    [Header("TTS Speakers")]
    public KokoroTTSSpeaker kokoroTTSSpeaker;

    private ITTSSpeaker TSSpeaker;

    // Start is called before the first frame update
    void Start()
    {
        TSSpeaker = GetSelectedSpeaker();
        TestTTSConnection();
    }

    private void TestTTSConnection()
    {
        TSSpeaker.TestConnection();
    }

    private ITTSSpeaker GetSelectedSpeaker()
    {
        switch (selectedLocalTTS)
        {
            case TTSOption.KokoroTTS:
                return kokoroTTSSpeaker;
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