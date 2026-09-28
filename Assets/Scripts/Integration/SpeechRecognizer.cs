using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HuggingFace.API; // Пространство имён официального пакета

public class SpeechRecognizer : MonoBehaviour
{
    [SerializeField] private Button recordButton;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text statusText;

    private AudioClip recordedClip;
    private bool isRecording = false;
    private bool isProcessing = false;

    private const int MaxRecordingSeconds = 10;

    // API-ключ настраивается в окне Hugging Face API Wizard, в коде его нет

    private void Start()
    {
        recordButton.onClick.AddListener(OnButtonPressed);
        statusText.text = "Готов";
        resultText.text = "";
    }

    private void OnDisable()
    {
        recordButton.onClick.RemoveListener(OnButtonPressed);
    }

    private void OnButtonPressed()
    {
        if (isProcessing) return;

        if (!isRecording)
            StartRecording();
        else
            StopRecordingAndSend();
    }

    private void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            statusText.text = "Микрофон не найден";
            return;
        }

        // 44100 Гц — стандарт для микрофона, Whisper сам приведёт к нужной частоте
        recordedClip = Microphone.Start(null, false, MaxRecordingSeconds, 44100);
        isRecording = true;

        statusText.text = "Идёт запись...";
        resultText.text = "";
    }

    // Автоматическая остановка по истечении 10 секунд
    private void Update()
    {
        if (isRecording && recordedClip != null)
        {
            if (Microphone.GetPosition(null) >= recordedClip.samples)
            {
                StopRecordingAndSend();
            }
        }
    }

    private void StopRecordingAndSend()
    {
        if (!isRecording) return;

        if (Microphone.IsRecording(null))
            Microphone.End(null);

        isRecording = false;
        isProcessing = true;
        recordButton.interactable = false;
        statusText.text = "Отправка...";

        if (recordedClip == null)
        {
            statusText.text = "Ошибка: нет записи";
            ResetState();
            return;
        }

        byte[] wavData = EncodeAudioClipToWav(recordedClip);
        SendAudioToOfficialApi(wavData);
    }

    private void SendAudioToOfficialApi(byte[] wavData)
    {
        // Официальный метод сам добавляет заголовок Authorization и Content-Type
        HuggingFaceAPI.AutomaticSpeechRecognition(wavData,
            response => {
                // Успех
                resultText.text = response;
                statusText.text = "Готов";
                ResetState();
            },
            error => {
                // Ошибка
                statusText.text = "Ошибка";
                resultText.text = error;
                Debug.LogError($"[SpeechRecognizer] Ошибка API: {error}");
                ResetState();
            });
    }

    private void ResetState()
    {
        isProcessing = false;
        recordButton.interactable = true;
    }

    // Преобразование AudioClip в WAV-байты
    private byte[] EncodeAudioClipToWav(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        using (var memoryStream = new System.IO.MemoryStream(44 + samples.Length * 2))
        using (var writer = new System.IO.BinaryWriter(memoryStream))
        {
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + samples.Length * 2);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);                              // Subchunk1Size
            writer.Write((ushort)1);                       // AudioFormat PCM
            writer.Write((ushort)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2); // ByteRate
            writer.Write((ushort)(clip.channels * 2));     // BlockAlign
            writer.Write((ushort)16);                      // BitsPerSample
            writer.Write("data".ToCharArray());
            writer.Write(samples.Length * 2);              // Subchunk2Size

            foreach (var sample in samples)
            {
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * 32767f));
            }

            return memoryStream.ToArray();
        }
    }
}