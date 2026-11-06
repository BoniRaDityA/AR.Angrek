using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

[System.Serializable]
public class OrchidPhaseData
{
    public string orchidName;
    public string displayName;

    [TextArea(3, 8)]
    public string description;

    [TextArea(3, 8)]
    public string characteristic;

    public VideoClip videoCare;
    public AudioClip videoAudio;
    
    public GameObject benihPrefab;
    public GameObject remajaPrefab;
    public GameObject dewasaPrefab;
}

public class OrchidPhaseViewer : MonoBehaviour
{
    [Header("Data Anggrek")]
    public OrchidPhaseData[] orchidData;

    [Header("UI")]
    public GameObject phaseButtonPanel;
    public Button benihButton;
    public Button remajaButton;
    public Button dewasaButton;
    public Button infoButton;

    [Header("Controller")]    
    public PlayScene playSceneController;
    public ARPlaneObjectPlacer planePlacer;
    public string infoSceneName = "Info";

    private OrchidPhaseData currentOrchid;

    private void Start()
    {
        ClearOrchid();
    }

    // Dipanggil classifier saat jenis anggrek berhasil terdeteksi
    public void SetOrchid(string orchidName)
    {
        currentOrchid = FindOrchidData(orchidName);

        if (currentOrchid == null)
        {
            ClearOrchid();
            return;
        }

        phaseButtonPanel.SetActive(true);
        benihButton.gameObject.SetActive(currentOrchid.benihPrefab != null);
        remajaButton.gameObject.SetActive(currentOrchid.remajaPrefab != null);
        dewasaButton.gameObject.SetActive(currentOrchid.dewasaPrefab != null);
        infoButton.gameObject.SetActive(true);
    }

    // Mencari data anggrek berdasarkan nama label hasil prediksi
    private OrchidPhaseData FindOrchidData(string orchidName)
    {
        foreach (var data in orchidData)
        {
            if (data.orchidName.ToLower() == orchidName.ToLower())
                return data;
        }
        return null;
    } 

    // Menyembunyikan semua tombol
    public void ClearOrchid()
    {
        currentOrchid = null;

        if (phaseButtonPanel != null)
            phaseButtonPanel.SetActive(false);
        
        if (benihButton != null)
            benihButton.gameObject.SetActive(false);

        if (remajaButton != null)
            remajaButton.gameObject.SetActive(false);

        if (dewasaButton != null)
            dewasaButton.gameObject.SetActive(false);

        if (infoButton != null)
            infoButton.gameObject.SetActive(false);
    } 

    private void PlacePrefab(GameObject prefab)
    {
        if (prefab == null || planePlacer == null)
            return;
        
        planePlacer.PlaceOnCenter(prefab);
        playSceneController.SetStatus("Arahkan kamera ke bidang datar");
    }

    public void ShowBenih()
    {
        PlacePrefab(currentOrchid?.benihPrefab);
    }

    public void ShowRemaja()
    {
        PlacePrefab(currentOrchid?.remajaPrefab);
    }

    public void ShowDewasa()
    {
        PlacePrefab(currentOrchid?.dewasaPrefab);
    }

    public void ShowInfo()
    {
        if (currentOrchid == null)
            return;

        SelectedOrchidData.orchidName = currentOrchid.orchidName;
        SelectedOrchidData.displayName = currentOrchid.displayName;
        SelectedOrchidData.description = currentOrchid.description;
        SelectedOrchidData.characteristic = currentOrchid.characteristic;
        SelectedOrchidData.videoCare = currentOrchid.videoCare;
        SelectedOrchidData.videoAudio = currentOrchid.videoAudio;
        SceneManager.LoadScene(infoSceneName);
    }
}
