using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.InferenceEngine;
using Unity.Collections;
using TMPro;

public class OrchidClassifier : MonoBehaviour
{
    [Header("Model ONNX")]
    public ModelAsset onnxModel;
    public BackendType backendType = BackendType.CPU;
    public string inputName = "input_image";
    public string outputName = "orchid_classification";

    [Header("AR Camera")]
    public ARCameraManager cameraManager;

    [Header("UI dan Stage")]
    public TMP_Text resultText;
    public OrchidPhaseViewer phaseViewer;

    [Header("Inference")]
    public float minLockConfidence = 0.7f;  // Nilai minimal confidence di lock (70%)
    public float minTopMargin = 0.05f;      // Nilai minimal margin prediksi 1 dan 2 (5%) bisa dinaikkan
    public float inferenceInterval = 0.35f; // Model melakukan inferensi setiap 0.35 detik
    public int stablePredictionCount = 3;   // Prediksi harus muncul stabil sebanyak 3 kali berturut-turut
    public float highTimeConf = 1f;         // Jika confidence tinggi, perlu tahan 1 detik untuk lock
    public float lowTimeConf = 1.5f;          // Jika confidence rendah, perlu tahan 1.5 detik untuk lock
    public float lowMinConf= 0.25f;         // Prediksi diabaikan saat confidence rendah (25%)
    public float lowMinMarg = 0.03f;        // Selisih antar kelas minimal 3% saat confidence rendah
    public float scanWait = 1.5f;             // Perlu menunggu selama 3 detik untuk memulai scan

    private const int inputSize = 224;      // Input masukan
    private bool lockPrediction = false;    // Set lockPrediction sebagai false
    private float scanStartTime;            // Menyimpan untuk scan dimulai

    private string candidateLabel = "";     // Label kandidat saat ini
    private float candidateStartTime = 0f;  // Menghitung kandidat mulai muncul
    private float candidateBestConf = 0f;   // Confidence terbaik yang pernah dicapai kandidat
    private bool candidateLowConf = false;  // Mengkategorikan kandidat di low atau high confidence

    // Labels yang digunakan
    private readonly string[] labels =
    {
        "cattleya", "dendrobium", "oncidium", "phalaenopsis", "vanda"
    };

    private Worker worker;                  // Menggunakan object Barracuda Worker untuk menjalankan model
    private Texture2D cameraTexture;        // Menyimpan gambar dari masukan kamera
    private float nextInferenceTime;        // Menentukan inferensi berikutnya dijalankan
    private bool scanningEnabled = true;    // Menentukan scanning aktif

    private string currentLabel = "";       // Label hasil akhir saat ini
    private string lastPredictedLabel = ""; // Menyimpan prediksi sebelumnya (menentukan kekonsistenannya)
    private int samePredictionCounter = 0;  // Menghitung jenis yang diprediksi muncul berturut-turut

    public string CurrentLabel => currentLabel;
    
    // Fungsi saat aplikasi dijalankan
    private void Start()
    {
        if (onnxModel == null) // Kasus bila model belum diisi
        {
            SetResult("Model ONNX belum diisi");
            enabled = false;
            return;
        }
        // Worker membaca model ONNX
        Model runtimeModel = ModelLoader.Load(onnxModel);
        worker = new Worker(runtimeModel, backendType);
        ResetPrediction();
    }

    private void Update()
    {
        // Menghentikan proses klasifikasi apabila scanning dinonaktifkan atau prediksi sudah berhasil dikunci
        if (!scanningEnabled || lockPrediction)
            return;
        // Memberikan jeda beberapa detik setelah kamera aktif agar pengguna dapat mengarahkan kamera ke objek
        if (Time.time - scanStartTime < scanWait)
        {
            SetResult("Arahkan kamera ke daun anggrek");
            return;
        }
        // Membatasi frekuensi inferensi sehingga model tidak dijalankan pada setiap frame (mengurangi beban CPU)
        if (Time.time < nextInferenceTime)
            return;
        // Menentukan waktu inferensi berikutnya
        nextInferenceTime = Time.time + inferenceInterval;
        // Mengambil frame kamera dan menjalankan klasifikasi
        if (GetCameraTexture(out Texture2D texture))
            Classify(texture);
    }

    // Mengambil frame terbaru dari kamera AR dan diubah menjadi Texture2D 224x224
    private bool GetCameraTexture(out Texture2D texture)
    {
        texture = null;
        // Memastikan AR Camera tersedia
        if (cameraManager == null)
            return false;
        // Mengambil frame terbaru dari kamera
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            return false;
        // Mengambil ukuran sisi terkecil sehingga gambar menjadi persegi (menjaga rasio aspek gambar)
        int squareSize = Mathf.Min(image.width, image.height);
        // Menghitung titik awal crop pada sumbu X dan Y
        int startX = (image.width - squareSize) / 2;
        int startY = (image.height - squareSize) / 2;
        using (image)
        {
            // Parameter konversi citra kamera
            var conversionParams = new XRCpuImage.ConversionParams
            {
                // Crop bagian tengah gambar
                inputRect = new RectInt(startX, startY, squareSize, squareSize),
                // Resize menjadi ukuran input model
                outputDimensions = new Vector2Int(inputSize, inputSize),
                // Menggunakan format RGBA
                outputFormat = TextureFormat.RGBA32,
                // Membalik gambar agar orientasi sesuai
                transformation = XRCpuImage.Transformation.MirrorY
            };
            // Menghitung ukuran buffer
            int size = image.GetConvertedDataSize(conversionParams);
            using var buffer = new NativeArray<byte>(size, Allocator.Temp);
            // Mengubah frame kamera menjadi data RGBA
            image.Convert(conversionParams, new NativeSlice<byte>(buffer));
            // Membuat Texture2D sekali
            if (cameraTexture == null)
                cameraTexture = new Texture2D(inputSize, inputSize, TextureFormat.RGBA32, false);
            // Mengisi Texture2D dengan data kamera
            cameraTexture.LoadRawTextureData(buffer);
            // Menerapkan perubahan
            cameraTexture.Apply();
            texture = cameraTexture;
            return true;
        }
    }

    // Menjalankan model ONNX dengan input kamera
    // Melakukan inferensi menggunakan model ONNX
    private void Classify(Texture2D texture)
    {
        // Mengambil seluruh pixel gambar
        Color32[] pixels = texture.GetPixels32();
        // Array input untuk model
        float[] inputData = new float[inputSize * inputSize * 3];
        // Mengubah gambar menjadi tensor NHWC: [Batch, Height, Width, Channel]
        for (int y = 0; y < inputSize; y++)
        {
            for (int x = 0; x < inputSize; x++)
            {
                Color32 pixel = pixels[y * inputSize + x];
                int index = (y * inputSize + x) * 3;
                // Normalisasi nilai RGB dari 0-255 menjadi 0-1
                inputData[index + 0] = pixel.r / 255f;
                inputData[index + 1] = pixel.g / 255f;
                inputData[index + 2] = pixel.b / 255f;
            }
        }
        // Membuat tensor dengan bentuk: [1, 224, 224, 3]
        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, inputSize, inputSize, 3), inputData);
        // Mengirim tensor ke model
        worker.SetInput(inputName, inputTensor);
        // Menjalankan inferensi
        worker.Schedule();
        // Mengambil output model
        Tensor<float> outputTensor = string.IsNullOrEmpty(outputName)
            ? worker.PeekOutput() as Tensor<float>
            : worker.PeekOutput(outputName) as Tensor<float>;
        if (outputTensor == null)
        {
            SetResult("Output model tidak ditemukan");
            return;
        }
        // Memindahkan output dari GPU ke CPU dan mengubah tensor menjadi array float
        using Tensor<float> readableOutput = outputTensor.ReadbackAndClone();
        float[] scores = readableOutput.DownloadToArray();
        
        // Mencari indeks kelas dengan nilai probabilitas tertinggi (prediksi utama)
        int bestPred = ArgMax(scores);
        // Mengambil nilai probabilitas tertinggi sebagai confidence
        float confidence = scores[bestPred];
        // Mengambil nama genus sesuai indeks prediksi tertinggi
        string predictedLabel = labels[bestPred];
        
        // Mencari prediksi dengan probabilitas terbesar kedua
        int secondPred = SecondPrediction(scores, bestPred);
        // Mengambil nilai probabilitas kedua
        float secondConf = scores[secondPred];
        // Menghitung selisih probabilitas prediksi pertama dan kedua (top margin)
        float margin = confidence - secondConf;
        // Mengambil nama genus prediksi kedua
        string secondLabel = labels[secondPred];

        // Cek apakah confidence memenuhi syarat
        bool highConf = confidence >= minLockConfidence && margin >= minTopMargin;
        // Cek apakah confidence masih berada pada kategori rendah
        bool lowConf = confidence >= lowMinConf && margin >= lowMinMarg;

        // Jika kedua syarat tidak terpenuhi, hasil dianggap belum valid
        if (!highConf && !lowConf)
        {
            ClearCandidate();
            SetResult(
                $"Daun masih belum jelas, dekatkan lagi"
            );
            return;
        }
        // Menyimpan kandidat prediksi yang sedang diamati
        UpdateCandidate(predictedLabel, confidence, !highConf);
        
        // Menentukan lama waktu validasi berdasarkan confidence
        float requiredTime = candidateLowConf        
            ? lowTimeConf : highTimeConf;
        // Menghitung lama kandidat telah terdeteksi
        float duration = Time.time - candidateStartTime;
        // Menampilkan informasi proses analisis pada layar
        SetResult(
            $"Menganalisis, pertahankan posisi\n" +
            $"Stabil: {duration:0.0}/{requiredTime:0.0} detik"
        );
        // Jika waktu stabil belum terpenuhi, maka perlu menunggu
        if (duration < requiredTime)
            return;
            // Mengunci hasil prediksi sebagai hasil akhir
            PredictionLock(candidateLabel, candidateBestConf);
    }

    // Mengurangi prediksi yang berubah akibat kamera bergerak
    private void StablePrediction(string predictedLabel, float confidence)
    {
        if (predictedLabel == lastPredictedLabel)
        {
            samePredictionCounter++;    // Jika stabil maka jenis yang diprediksi akan ditambah terus-menerus
        }
        else    // Jika prediksi sebelmnya berbeda dengan prediksi yang sekarang
        {
            lastPredictedLabel = predictedLabel;    // predictedLabel merupakan label yang sekarang menggantikan lastPredictionLabel
            samePredictionCounter = 1;  // Penghitung diulang dari awal
        }

        if (samePredictionCounter < stablePredictionCount) // Jika jumlah prediksi yang sama belum batas maksimal, proses dihentikan sementara
            return;
        // Menyimpan genus hasil klasifikasi sebagai hasil prediksi akhir
        currentLabel = predictedLabel;
        // Mengunci hasil prediksi agar tidak berubah selama proses berikutnya
        lockPrediction = true;
        // Menghentikan proses klasifikasi karena hasil sudah diperoleh
        scanningEnabled = false;
        SetResult($"{currentLabel}");
        // Menampilkan nama genus pada tampilan aplikasi
        if (phaseViewer != null)
            phaseViewer.SetOrchid(currentLabel);
    }

    private int SecondPrediction(float[] values, int bestPred)
    {
        int secondPred = bestPred == 0 ? 1 : 0;
        float secondValue = values[secondPred];

        for (int i = 0; i < values.Length; i++)
        {
            if (i == bestPred)
                continue;
            if (values[i] > secondValue)
            {
                secondValue = values[i];
                secondPred = i;
            }
        }
        return secondPred;
    }

    private void UpdateCandidate(string label, float confidence, bool isLowConfidence)
    {
        if (label != candidateLabel || isLowConfidence != candidateLowConf)
        {
            candidateLabel = label;
            candidateStartTime = Time.time;
            candidateBestConf = confidence;
            candidateLowConf = isLowConfidence;
            return;
        }

        if (confidence > candidateBestConf)
            candidateBestConf = confidence;
    }

    private void ClearCandidate()
    {
        candidateLabel = "";
        candidateStartTime = Time.time;
        candidateBestConf = 0f;
        candidateLowConf = false;
    }

    private void PredictionLock(string label, float confidence)
    {
        currentLabel = label;
        lockPrediction = true;
        scanningEnabled = false;
        SetResult($"Prediksi: {currentLabel}");

        if (phaseViewer != null)
            phaseViewer.SetOrchid(currentLabel);
    }

    public void ResetPrediction()
    {
        currentLabel = "";
        lockPrediction = false;
        scanningEnabled = true;
        scanStartTime = Time.time;
        nextInferenceTime = 0f;

        ClearCandidate();
        
        if (phaseViewer != null)
            phaseViewer.ClearOrchid();
    }

    public void SetScanning(bool value)
    {
        scanningEnabled = value;
        if (value)
            ResetPrediction();
    }

    private int ArgMax(float[] values)
    {
        int bestIndex = 0;
        float bestValue = values[0];

        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > bestValue)
            {
                bestValue = values[i];
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private void SetResult(string message)
    {
        if (resultText != null)
            resultText.text = message;
    }

    private void OnEnable()
    {
        ResetPrediction();
    }

    private void OnDestroy()
    {
        worker?.Dispose();
        if (cameraTexture != null)
            Destroy(cameraTexture);
    }
}
