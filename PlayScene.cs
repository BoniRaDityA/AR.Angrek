using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

public class PlayScene : MonoBehaviour
{
    // AR Session digunakan untuk reset tracking AR ketika masuk atau keluar scene Play
    public ARSession arSession;

    // classifier untuk reset prediksi dan mengaktifkan atau mematikan scanning
    public OrchidClassifier classifier;

    // phaseViewer untuk menyembunyikan tombol fase anggrek
    public OrchidPhaseViewer phaseViewer;

    // planePlacer untuk menghapus objek 3D yang sedang tampil
    public ARPlaneObjectPlacer planePlacer;

    // panel yang berisi tombol benih, remaja, dewasa, dan informasi
    public GameObject phaseButtonPanel;

    // teks di atas laar untuk menampilkan prediksi
    public TMP_Text resultText;

    private void Start()
    {
        ResetPlayState();
    }

    // Mengembalikan scene Play ke kondisi awal
    // Dipanggil saat scene Play pertama kali dibuka

    public void ResetPlayState()
    {
        if (phaseButtonPanel != null)
            phaseButtonPanel.SetActive(false);

        if (planePlacer != null)
            planePlacer.ClearPlacedObject();

        if (phaseViewer != null)
            phaseViewer.ClearOrchid();

        if (classifier != null)
        {
            classifier.ResetPrediction();
            classifier.SetScanning(true);
        }

        if (arSession != null)
            arSession.Reset();
        
        SetStatus("Arahkan Kamera Pada Anggrek");
    }

    // Dipanggil oleh tombol Back di scene Play
    // Berfungsi untuk kembali ke menu utama

    public void BackMenu()
    {
        if (classifier != null)
            classifier.SetScanning(false);

        if (planePlacer != null)
            planePlacer.ClearPlacedObject();

        if (arSession != null)
            arSession.Reset();
        
        SceneManager.LoadScene("SampleScene");
    }

    public void SetStatus(string message)
    {
        if (resultText != null)
            resultText.text = message;
    }
}
