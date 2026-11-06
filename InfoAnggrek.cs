using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Video;

public class InfoAnggrek : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public TMP_Text characteristicText;
    public VideoPlayer video;
    public AudioSource audioSource;
    
    private void Start()
    {
        titleText.text = SelectedOrchidData.displayName;
        descriptionText.text = "DESKRIPSI\n" + SelectedOrchidData.description;
        characteristicText.text = "CIRI-CIRI\n" + SelectedOrchidData.characteristic;
        
        video.clip = SelectedOrchidData.videoCare;
        video.audioOutputMode = VideoAudioOutputMode.None;
        video.playOnAwake = false;

        audioSource.clip = SelectedOrchidData.videoAudio;
        audioSource.volume = 1f;
        audioSource.spatialBlend = 0f;

        video.Prepare();
    }

    public void BackToPlay()
    {
        SceneManager.LoadScene("Play");
    }

    public void PlayVideo()
    {
        video.Play();
        audioSource.Play();
    }

    public void PauseVideo()
    {
        video.Pause();
        audioSource.Pause();
    }

    public void StopVideo()
    {
        video.Stop();
        audioSource.Stop();
    }

    public void Forward()
    {
        video.time += 10;
        if (audioSource.clip != null)
            audioSource.time = Mathf.Min(audioSource.clip.length, audioSource.time + 10);
    }

    public void Backward()
    {
        video.time = Mathf.Max(0, (float)video.time - 10);
        if (audioSource.clip != null)
            audioSource.time = Mathf.Max(0f, audioSource.time - 10);
    }
}
