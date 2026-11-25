using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;

public sealed class GameManager : MonoBehaviour
{
    private const float StartingTimeLimit = 600f;
    private const int RelicScore = 100;
    private const int GuardianScore = 250;
    private const string VolumePreferenceKey = "Greenfall.Volume";
    private const string SensitivityPreferenceKey = "Greenfall.MouseSensitivity";

    private static bool startGameAfterLoading;
    private static bool showStartPageAfterLoading;

    private enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        Won,
        Lost
    }

    [SerializeField] private PlayerCharacterController player;
    [SerializeField] private EnemyAI[] enemies = new EnemyAI[0];
    [SerializeField] private RelicPickup[] relics = new RelicPickup[0];

    private GameState state;
    private Canvas canvas;
    private GameObject mainMenu;
    private GameObject startPage;
    private GameObject titlePage;
    private GameObject levelPage;
    private GameObject settingsPage;
    private GameObject creditsPage;
    private GameObject loadingPage;
    private GameObject pauseMenu;
    private GameObject endMenu;
    private GameObject hud;
    private Text endTitle;
    private Text objectiveText;
    private Text healthText;
    private Text loadingStatus;
    private Text volumeValueText;
    private Text sensitivityValueText;
    private Image healthFill;
    private Image loadingFill;
    private GameObject loadingBackButton;
    private Slider volumeSlider;
    private Slider sensitivitySlider;
    private AudioSource musicSource;
    private AudioSource effectsSource;
    private AudioClip relicCollectedClip;
    private AudioClip guardianDefeatedClip;
    private int lastDefeatedCount;
    private int collectedRelics;
    private int score;
    private float timeRemaining;
    private float nextHudRefresh;
    private bool hasActiveRun;
    private bool isLoadingLevel;

    public void Configure(PlayerCharacterController playerController, EnemyAI[] opponents, RelicPickup[] pickups)
    {
        player = playerController;
        enemies = opponents ?? new EnemyAI[0];
        relics = pickups ?? new RelicPickup[0];
    }

    private void Awake()
    {
        Time.timeScale = 0f;
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerCharacterController>();
        }

        if (enemies == null || enemies.Length == 0)
        {
            EnemyAI[] foundEnemies = FindObjectsByType<EnemyAI>();
            enemies = foundEnemies;
        }

        if (relics == null || relics.Length == 0)
        {
            relics = FindObjectsByType<RelicPickup>();
        }

        if (player != null)
        {
            player.enabled = false;
        }

        foreach (EnemyAI enemy in enemies)
        {
            if (enemy == null)
            {
                continue;
            }

            enemy.enabled = false;
            EnemyCombat combat = enemy.GetComponent<EnemyCombat>();
            if (combat != null)
            {
                combat.enabled = false;
            }
        }

        foreach (RelicPickup relic in relics)
        {
            if (relic != null)
            {
                relic.SetGameManager(this);
            }
        }
    }

    private void Start()
    {
        EnsureEventSystem();
        CreateInterface();
        CreateAudio();
        ApplySavedSettings();
        if (startGameAfterLoading)
        {
            startGameAfterLoading = false;
            StartGame();
        }
        else if (showStartPageAfterLoading)
        {
            showStartPageAfterLoading = false;
            SetState(GameState.MainMenu);
            ShowMenuPage(startPage);
        }
        else
        {
            SetState(GameState.MainMenu);
            StartCoroutine(LoadStartupScene());
        }

        RefreshHud();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (state == GameState.Playing)
            {
                SetState(GameState.Paused);
            }
            else if (state == GameState.Paused)
            {
                SetState(GameState.Playing);
            }
            else if (state == GameState.MainMenu && !loadingPage.activeSelf)
            {
                if (titlePage.activeSelf)
                {
                    ShowMenuPage(startPage);
                }
                else if (!startPage.activeSelf)
                {
                    ShowMenuPage(titlePage);
                }
            }
        }

        if (state != GameState.Playing)
        {
            return;
        }

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        if (Time.unscaledTime >= nextHudRefresh)
        {
            nextHudRefresh = Time.unscaledTime + 0.15f;
            RefreshHud();
        }

        if (player != null && player.IsDead)
        {
            ShowEndMenu(false);
            return;
        }

        if (timeRemaining <= 0f)
        {
            ShowEndMenu(false, true);
            return;
        }

        int defeatedCount = CountDefeatedEnemies();
        if (defeatedCount > lastDefeatedCount)
        {
            score += (defeatedCount - lastDefeatedCount) * GuardianScore;
            effectsSource.PlayOneShot(guardianDefeatedClip);
            lastDefeatedCount = defeatedCount;
        }

        if (collectedRelics >= relics.Length && defeatedCount >= enemies.Length)
        {
            ShowEndMenu(true);
        }
    }

    public void CollectRelic()
    {
        collectedRelics++;
        score += RelicScore;
        effectsSource.PlayOneShot(relicCollectedClip);
        RefreshHud();
    }

    private void EnsureEventSystem()
    {
        EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }

    private void CreateInterface()
    {
        GameObject canvasObject = new GameObject("Greenfall UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CreateMainMenu();

        pauseMenu = CreateOverlay("JEDA", "PETUALANGAN DIJEDA", "Tarik napas, lalu lanjutkan perjalanan.", string.Empty);
        CreateButton(pauseMenu.transform, "LANJUTKAN", new Vector2(0f, -55f), () => SetState(GameState.Playing));
        CreateButton(pauseMenu.transform, "ULANGI PERMAINAN", new Vector2(0f, -130f), RestartGame);
        CreateButton(pauseMenu.transform, "MENU UTAMA", new Vector2(0f, -205f), ReturnToMainMenu);

        endMenu = CreateOverlay(string.Empty, string.Empty, string.Empty, string.Empty);
        endTitle = endMenu.transform.Find("Title").GetComponent<Text>();
        CreateButton(endMenu.transform, "MAIN LAGI", new Vector2(0f, -105f), RestartGame);
        CreateButton(endMenu.transform, "MENU UTAMA", new Vector2(0f, -180f), ReturnToMainMenu);

        hud = CreateHud();
    }

    private void CreateMainMenu()
    {
        mainMenu = new GameObject("Greenfall Main Menu", typeof(RectTransform), typeof(Image));
        mainMenu.transform.SetParent(canvas.transform, false);
        StretchToParent(mainMenu.GetComponent<RectTransform>());
        mainMenu.GetComponent<Image>().color = new Color(0.018f, 0.045f, 0.052f, 0.96f);

        GameObject accent = CreateImage(mainMenu.transform, "Gold Accent", new Color(0.88f, 0.69f, 0.3f, 0.95f),
            new Vector2(26f, 0f), new Vector2(5f, 1080f), new Vector2(0f, 0.5f));
        accent.GetComponent<Image>().raycastTarget = false;
        startPage = CreateMenuPage(mainMenu.transform, "Start Page");
        titlePage = CreateMenuPage(mainMenu.transform, "Title Page");
        levelPage = CreateMenuPage(mainMenu.transform, "Level Select Page");
        settingsPage = CreateMenuPage(mainMenu.transform, "Settings Page");
        creditsPage = CreateMenuPage(mainMenu.transform, "Credits Page");
        loadingPage = CreateMenuPage(mainMenu.transform, "Loading Page");
        loadingPage.AddComponent<Image>().raycastTarget = true;

        CreateText(startPage.transform, "Start Eyebrow", "SEBUAH PETUALANGAN DI HUTAN PURBA", 18,
            new Vector2(0f, 190f), new Vector2(900f, 40f), new Color(0.69f, 0.82f, 0.72f));
        CreateText(startPage.transform, "Start Title", "GREENFALL", 82,
            new Vector2(0f, 85f), new Vector2(1000f, 120f), new Color(0.94f, 0.77f, 0.39f));
        CreateText(startPage.transform, "Start Subtitle", "RELIK TERAKHIR", 31,
            new Vector2(0f, 5f), new Vector2(800f, 55f), Color.white);
        CreateImage(startPage.transform, "Start Divider", new Color(0.88f, 0.69f, 0.3f, 0.8f),
            new Vector2(0f, -45f), new Vector2(360f, 2f));
        CreateText(startPage.transform, "Start Description",
            "Cahaya hutan mulai padam. Takdir Greenfall ada di tanganmu.",
            20, new Vector2(0f, -100f), new Vector2(900f, 45f), new Color(0.84f, 0.89f, 0.86f));
        CreateButton(startPage.transform, "MULAI", new Vector2(0f, -205f),
            () => ShowMenuPage(titlePage), 320f, true);

        CreateText(titlePage.transform, "Eyebrow", "PETUALANGAN FANTASI  •  PC", 18,
            new Vector2(-490f, 300f), new Vector2(660f, 36f), new Color(0.69f, 0.82f, 0.72f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        CreateText(titlePage.transform, "Game Title", "GREENFALL", 76,
            new Vector2(-490f, 205f), new Vector2(700f, 110f), new Color(0.94f, 0.77f, 0.39f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        CreateText(titlePage.transform, "Game Subtitle", "RELIK TERAKHIR", 31,
            new Vector2(-490f, 132f), new Vector2(650f, 52f), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        CreateImage(titlePage.transform, "Title Divider", new Color(0.88f, 0.69f, 0.3f, 0.8f),
            new Vector2(-490f, 88f), new Vector2(520f, 2f));
        CreateText(titlePage.transform, "Story", "Cahaya hutan mulai padam.\nTemukan lima relik kuno dan hadapi\npara penjaga Greenfall.",
            23, new Vector2(-490f, 15f), new Vector2(620f, 120f), new Color(0.84f, 0.89f, 0.86f));
        CreateText(titlePage.transform, "Objective Summary", "5 RELIK  /  3 PENJAGA  /  10 MENIT", 18,
            new Vector2(-490f, -100f), new Vector2(600f, 40f), new Color(0.94f, 0.77f, 0.39f));
        CreateText(titlePage.transform, "Control Summary",
            "WASD bergerak     MOUSE kamera     SHIFT berlari\nSPACE melompat     KLIK KIRI menyerang     ESC jeda",
            17, new Vector2(-490f, -235f), new Vector2(660f, 76f), new Color(0.69f, 0.78f, 0.75f));
        CreateText(titlePage.transform, "Menu Heading", "JELAJAHI GREENFALL", 16,
            new Vector2(410f, 315f), new Vector2(440f, 34f), new Color(0.69f, 0.82f, 0.72f));
        CreateButton(titlePage.transform, "MULAI PETUALANGAN", new Vector2(410f, 230f),
            BeginNewGame, 410f, true);
        CreateButton(titlePage.transform, "PILIH LEVEL", new Vector2(410f, 140f),
            () => ShowMenuPage(levelPage), 410f);
        CreateButton(titlePage.transform, "LANJUTKAN GAME", new Vector2(410f, 50f),
            ContinueGame, 410f);
        CreateButton(titlePage.transform, "PENGATURAN", new Vector2(410f, -40f),
            () => ShowMenuPage(settingsPage), 410f);
        CreateButton(titlePage.transform, "KREDIT", new Vector2(410f, -130f),
            () => ShowMenuPage(creditsPage), 410f);
        CreateButton(titlePage.transform, "KELUAR", new Vector2(410f, -220f),
            QuitGame, 410f);

        CreatePageHeader(levelPage.transform, "PILIH LEVEL", "Pilih perjalanan yang ingin kamu mulai.");
        CreateImage(levelPage.transform, "Level Card", new Color(0.07f, 0.15f, 0.14f, 0.92f),
            new Vector2(0f, 20f), new Vector2(720f, 230f));
        CreateText(levelPage.transform, "Level Name", "01  /  HUTAN GREENFALL", 32,
            new Vector2(0f, 82f), new Vector2(650f, 56f), new Color(0.94f, 0.77f, 0.39f));
        CreateText(levelPage.transform, "Level Description", "Jelajahi hutan, kumpulkan relik, dan kalahkan tiga penjaga.",
            20, new Vector2(0f, 25f), new Vector2(650f, 48f), Color.white);
        CreateText(levelPage.transform, "Level Objective", "Kesulitan: Petualangan  •  Durasi: Maks. 10 menit",
            17, new Vector2(0f, -28f), new Vector2(650f, 42f), new Color(0.69f, 0.82f, 0.72f));
        CreateButton(levelPage.transform, "MULAI LEVEL", new Vector2(0f, -65f),
            BeginNewGame, 300f, true);
        CreateButton(levelPage.transform, "KEMBALI", new Vector2(0f, -250f),
            () => ShowMenuPage(titlePage), 300f);

        CreatePageHeader(settingsPage.transform, "PENGATURAN", "Sesuaikan pengalaman bermainmu.");
        CreateText(settingsPage.transform, "Volume Label", "VOLUME AUDIO", 20,
            new Vector2(-230f, 105f), new Vector2(300f, 40f), Color.white);
        volumeValueText = CreateText(settingsPage.transform, "Volume Value", "80%", 18,
            new Vector2(330f, 105f), new Vector2(100f, 40f), new Color(0.94f, 0.77f, 0.39f));
        volumeSlider = CreateSlider(settingsPage.transform, "Volume Slider", new Vector2(0f, 55f),
            0f, 1f, 0.8f, OnVolumeChanged);
        CreateText(settingsPage.transform, "Sensitivity Label", "SENSITIVITAS MOUSE", 20,
            new Vector2(-230f, -30f), new Vector2(360f, 40f), Color.white);
        sensitivityValueText = CreateText(settingsPage.transform, "Sensitivity Value", "0.12", 18,
            new Vector2(330f, -30f), new Vector2(100f, 40f), new Color(0.94f, 0.77f, 0.39f));
        sensitivitySlider = CreateSlider(settingsPage.transform, "Sensitivity Slider", new Vector2(0f, -80f),
            0.04f, 0.4f, 0.12f, OnSensitivityChanged);
        CreateButton(settingsPage.transform, "KEMBALI", new Vector2(0f, -245f),
            () => ShowMenuPage(titlePage), 300f);

        CreatePageHeader(creditsPage.transform, "KREDIT", "INFORMASI PEMBUAT GAME");
        CreateImage(creditsPage.transform, "Credits Card", new Color(0.07f, 0.15f, 0.14f, 0.92f),
            new Vector2(0f, 0f), new Vector2(760f, 320f));
        CreateText(creditsPage.transform, "Credits Project", "GREENFALL: RELIK TERAKHIR", 29,
            new Vector2(0f, 104f), new Vector2(690f, 48f), new Color(0.94f, 0.77f, 0.39f));
        CreateText(creditsPage.transform, "Credits Details",
            "Nama: Adin Prasetya\nNIM: 14012300049\nKelas: 7A-Sen\nMata Kuliah: Game Development\nDosen Pengampu: Ali Rohman, S.Kom., M.Kom.",
            19, new Vector2(0f, -17f), new Vector2(690f, 150f), Color.white);
        CreateButton(creditsPage.transform, "KEMBALI", new Vector2(0f, -240f),
            () => ShowMenuPage(titlePage), 300f);

        loadingPage.GetComponent<Image>().color = new Color(0.018f, 0.045f, 0.052f, 0.98f);
        CreateText(loadingPage.transform, "Loading Title", "MENYIAPKAN GREENFALL", 40,
            new Vector2(0f, 75f), new Vector2(850f, 70f), new Color(0.94f, 0.77f, 0.39f));
        loadingStatus = CreateText(loadingPage.transform, "Loading Status", "Membuka gerbang hutan...", 20,
            new Vector2(0f, 15f), new Vector2(700f, 42f), Color.white);
        GameObject loadingTrack = CreateImage(loadingPage.transform, "Loading Track",
            new Color(0.12f, 0.19f, 0.17f, 1f), new Vector2(0f, -35f), new Vector2(500f, 16f));
        GameObject fillObject = CreateImage(loadingTrack.transform, "Loading Fill",
            new Color(0.94f, 0.77f, 0.39f, 1f), Vector2.zero, Vector2.zero);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        loadingFill = fillObject.GetComponent<Image>();
        CreateText(loadingPage.transform, "Loading Hint", "Greenfall menantimu.", 17,
            new Vector2(0f, -90f), new Vector2(500f, 40f), new Color(0.69f, 0.82f, 0.72f));
        loadingBackButton = CreateButton(loadingPage.transform, "KEMBALI KE MENU",
            new Vector2(0f, -180f), () => ShowMenuPage(titlePage), 300f);
        loadingBackButton.SetActive(false);

        ShowMenuPage(titlePage);
    }

    private GameObject CreateMenuPage(Transform parent, string objectName)
    {
        GameObject page = new GameObject(objectName, typeof(RectTransform));
        page.transform.SetParent(parent, false);
        StretchToParent(page.GetComponent<RectTransform>());
        page.SetActive(false);
        return page;
    }

    private void CreatePageHeader(Transform parent, string title, string subtitle)
    {
        CreateText(parent, "Page Title", title, 46, new Vector2(0f, 300f),
            new Vector2(900f, 70f), new Color(0.94f, 0.77f, 0.39f));
        CreateText(parent, "Page Subtitle", subtitle, 20, new Vector2(0f, 245f),
            new Vector2(900f, 42f), new Color(0.84f, 0.89f, 0.86f));
    }

    private GameObject CreateImage(Transform parent, string objectName, Color color,
        Vector2 position, Vector2 size, Vector2? anchor = null)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor ?? new Vector2(0.5f, 0.5f);
        rect.anchorMax = anchor ?? new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return imageObject;
    }

    private Slider CreateSlider(Transform parent, string objectName, Vector2 position,
        float minimum, float maximum, float value, UnityAction<float> onValueChanged)
    {
        GameObject sliderObject = new GameObject(objectName, typeof(RectTransform), typeof(Slider), typeof(Image));
        sliderObject.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = position;
        sliderRect.sizeDelta = new Vector2(560f, 18f);
        sliderObject.GetComponent<Image>().color = new Color(0.12f, 0.19f, 0.17f, 1f);

        GameObject fillObject = CreateImage(sliderObject.transform, "Fill",
            new Color(0.88f, 0.69f, 0.3f, 1f), Vector2.zero, Vector2.zero);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handleObject = CreateImage(sliderObject.transform, "Handle",
            new Color(0.94f, 0.9f, 0.75f, 1f), Vector2.zero, new Vector2(24f, 30f));
        handleObject.GetComponent<Image>().raycastTarget = true;
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.value = value;
        slider.fillRect = fillRect;
        slider.handleRect = handleObject.GetComponent<RectTransform>();
        slider.targetGraphic = handleObject.GetComponent<Image>();
        slider.onValueChanged.AddListener(onValueChanged);
        return slider;
    }

    private void ShowMenuPage(GameObject page)
    {
        if (startPage == null)
        {
            return;
        }

        startPage.SetActive(page == startPage);
        titlePage.SetActive(page == titlePage);
        levelPage.SetActive(page == levelPage);
        settingsPage.SetActive(page == settingsPage);
        creditsPage.SetActive(page == creditsPage);
        loadingPage.SetActive(page == loadingPage);
    }

    private void ApplySavedSettings()
    {
        float volume = PlayerPrefs.GetFloat(VolumePreferenceKey, 0.8f);
        float sensitivity = PlayerPrefs.GetFloat(SensitivityPreferenceKey, 0.12f);
        volumeSlider.SetValueWithoutNotify(volume);
        sensitivitySlider.SetValueWithoutNotify(sensitivity);
        OnVolumeChanged(volume);
        OnSensitivityChanged(sensitivity);
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        if (volumeValueText != null)
        {
            volumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        PlayerPrefs.SetFloat(VolumePreferenceKey, value);
    }

    private void OnSensitivityChanged(float value)
    {
        if (player != null)
        {
            CharacterFollowCamera followCamera = Camera.main != null
                ? Camera.main.GetComponent<CharacterFollowCamera>()
                : null;
            followCamera?.SetMouseSensitivity(value);
        }

        if (sensitivityValueText != null)
        {
            sensitivityValueText.text = value.ToString("0.00");
        }

        PlayerPrefs.SetFloat(SensitivityPreferenceKey, value);
    }

    private void BeginNewGame()
    {
        if (!isLoadingLevel)
        {
            StartCoroutine(LoadLevelAndStart());
        }
    }

    private IEnumerator LoadLevelAndStart()
    {
        isLoadingLevel = true;
        ShowMenuPage(loadingPage);
        loadingBackButton.SetActive(false);
        loadingStatus.text = "Memuat " + SceneManager.GetActiveScene().name + "...";
        loadingFill.rectTransform.sizeDelta = new Vector2(0f, 16f);

        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        if (buildIndex < 0)
        {
            Debug.LogError("Greenfall cannot load the current level because it is not in Build Settings.");
            loadingStatus.text = "Level tidak terdaftar di Build Settings.";
            isLoadingLevel = false;
            loadingBackButton.SetActive(true);
            yield break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex);
        if (operation == null)
        {
            Debug.LogError("Greenfall could not start loading the selected level.");
            loadingStatus.text = "Level gagal dimuat. Kembali ke menu dan coba lagi.";
            isLoadingLevel = false;
            loadingBackButton.SetActive(true);
            yield break;
        }

        startGameAfterLoading = true;
        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            loadingFill.rectTransform.sizeDelta = new Vector2(500f * progress, 16f);
            yield return null;
        }
    }

    private IEnumerator LoadStartupScene()
    {
        isLoadingLevel = true;
        ShowMenuPage(loadingPage);
        loadingBackButton.SetActive(false);
        loadingStatus.text = "Memuat dunia Greenfall...";
        loadingFill.rectTransform.sizeDelta = new Vector2(0f, 16f);

        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        if (buildIndex < 0)
        {
            Debug.LogError("Greenfall cannot show its startup loading screen because the scene is not in Build Settings.");
            loadingStatus.text = "Scene belum terdaftar di Build Settings.";
            isLoadingLevel = false;
            ShowMenuPage(startPage);
            yield break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex);
        if (operation == null)
        {
            Debug.LogError("Greenfall could not start loading the startup scene.");
            loadingStatus.text = "Scene gagal dimuat. Tekan Mulai untuk melanjutkan.";
            isLoadingLevel = false;
            loadingBackButton.SetActive(true);
            yield break;
        }

        showStartPageAfterLoading = true;
        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            loadingFill.rectTransform.sizeDelta = new Vector2(500f * progress, 16f);
            yield return null;
        }
    }

    private void ContinueGame()
    {
        if (hasActiveRun)
        {
            SetState(GameState.Playing);
        }
        else
        {
            BeginNewGame();
        }
    }

    private void ReturnToMainMenu()
    {
        ShowMenuPage(titlePage);
        SetState(GameState.MainMenu);
    }

    private void CreateAudio()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = CreateAmbientClip();
        musicSource.loop = true;
        musicSource.volume = 0.16f;
        musicSource.spatialBlend = 0f;
        musicSource.Play();

        effectsSource = gameObject.AddComponent<AudioSource>();
        effectsSource.volume = 0.65f;
        effectsSource.spatialBlend = 0f;
        relicCollectedClip = CreateToneClip("Relic Collected", 880f, 0.5f, 0.2f);
        guardianDefeatedClip = CreateToneClip("Guardian Defeated", 260f, 0.45f, 0.18f);
    }

    private AudioClip CreateAmbientClip()
    {
        const int sampleRate = 22050;
        const float duration = 8f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float[] notes = { 130.81f, 164.81f, 196f, 261.63f };

        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)sampleRate;
            float envelope = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * time / duration);
            float value = 0f;
            foreach (float note in notes)
            {
                value += Mathf.Sin(2f * Mathf.PI * note * time) * 0.25f;
                value += Mathf.Sin(2f * Mathf.PI * note * 2f * time) * 0.04f;
            }

            samples[i] = value * envelope * 0.08f;
        }

        AudioClip clip = AudioClip.Create("Greenfall Ambient", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateToneClip(string clipName, float frequency, float duration, float volume)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)sampleRate;
            float envelope = Mathf.Exp(-time * 5f) * Mathf.Sin(Mathf.PI * time / duration);
            float sweep = frequency * (1f + time * 0.35f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * sweep * time) * envelope * volume;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private GameObject CreateOverlay(string title, string subtitle, string description, string controls)
    {
        GameObject panel = new GameObject(title + " Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.025f, 0.055f, 0.07f, 0.92f);

        CreateText(panel.transform, "Title", title, 54, new Vector2(0f, 205f), new Vector2(900f, 80f), new Color(0.92f, 0.78f, 0.42f));
        CreateText(panel.transform, "Subtitle", subtitle, 26, new Vector2(0f, 142f), new Vector2(900f, 48f), Color.white);
        CreateText(panel.transform, "Description", description, 22, new Vector2(0f, 88f), new Vector2(980f, 60f), new Color(0.82f, 0.88f, 0.86f));
        CreateText(panel.transform, "Controls", controls, 20, new Vector2(0f, -18f), new Vector2(900f, 95f), new Color(0.71f, 0.8f, 0.78f));
        return panel;
    }

    private GameObject CreateHud()
    {
        GameObject panel = new GameObject("HUD", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(30f, -30f);
        rect.sizeDelta = new Vector2(560f, 158f);
        panel.GetComponent<Image>().color = new Color(0.025f, 0.055f, 0.07f, 0.82f);

        healthText = CreateText(panel.transform, "Health", "KESEHATAN  100 / 100", 20,
            new Vector2(260f, 48f), new Vector2(520f, 32f), Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        healthText.alignment = TextAnchor.MiddleLeft;

        GameObject bar = new GameObject("Health Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(panel.transform, false);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0.5f);
        barRect.anchorMax = new Vector2(0f, 0.5f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.anchoredPosition = new Vector2(20f, 8f);
        barRect.sizeDelta = new Vector2(520f, 15f);
        bar.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.17f, 1f);

        GameObject fill = new GameObject("Health Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        healthFill = fill.GetComponent<Image>();
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthFill.color = new Color(0.35f, 0.78f, 0.43f);

        objectiveText = CreateText(panel.transform, "Objective", string.Empty, 19,
            new Vector2(260f, -42f), new Vector2(520f, 58f), new Color(0.92f, 0.78f, 0.42f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        objectiveText.alignment = TextAnchor.MiddleLeft;
        return panel;
    }

    private Text CreateText(Transform parent, string objectName, string value, int fontSize, Vector2 position,
        Vector2 size, Color color, Vector2? anchorMin = null, Vector2? anchorMax = null)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
        rect.anchorMax = anchorMax ?? new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private GameObject CreateButton(Transform parent, string label, Vector2 position, UnityAction action,
        float width = 300f, bool primary = false)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, primary ? 68f : 58f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = primary
            ? new Color(0.47f, 0.35f, 0.15f, 0.98f)
            : new Color(0.08f, 0.19f, 0.17f, 0.92f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.highlightedColor = primary
            ? new Color(0.65f, 0.49f, 0.21f, 1f)
            : new Color(0.17f, 0.35f, 0.29f, 1f);
        colors.pressedColor = new Color(0.13f, 0.26f, 0.23f, 1f);
        button.colors = colors;

        CreateText(buttonObject.transform, "Label", label, primary ? 21 : 18, Vector2.zero,
            new Vector2(width - 20f, primary ? 60f : 50f), Color.white);
        return buttonObject;
    }

    private void StartGame()
    {
        collectedRelics = 0;
        score = 0;
        timeRemaining = StartingTimeLimit;
        lastDefeatedCount = CountDefeatedEnemies();
        hasActiveRun = true;
        SetState(GameState.Playing);
        RefreshHud();
    }

    private void SetState(GameState newState)
    {
        state = newState;
        Time.timeScale = state == GameState.Playing ? 1f : 0f;
        Cursor.lockState = state == GameState.Playing ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = state != GameState.Playing;

        if (player != null)
        {
            player.enabled = state == GameState.Playing;
        }

        foreach (EnemyAI enemy in enemies)
        {
            if (enemy == null)
            {
                continue;
            }

            EnemyHealth health = enemy.GetComponent<EnemyHealth>();
            bool active = state == GameState.Playing && health != null && !health.IsDead;
            enemy.enabled = active;
            EnemyCombat combat = enemy.GetComponent<EnemyCombat>();
            if (combat != null && (health == null || !health.IsDead))
            {
                combat.enabled = active;
            }
        }

        if (mainMenu != null)
        {
            mainMenu.SetActive(state == GameState.MainMenu);
            pauseMenu.SetActive(state == GameState.Paused);
            endMenu.SetActive(state == GameState.Won || state == GameState.Lost);
            hud.SetActive(state == GameState.Playing || state == GameState.Paused);
            if (state == GameState.MainMenu)
            {
                ShowMenuPage(titlePage);
            }
        }
    }

    private void ShowEndMenu(bool won, bool timedOut = false)
    {
        endTitle.text = won ? "HUTAN TERSELAMATKAN" : "PETUALANGAN BERAKHIR";
        state = won ? GameState.Won : GameState.Lost;
        Time.timeScale = 0f;
        hasActiveRun = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (player != null)
        {
            player.enabled = false;
        }

        foreach (EnemyAI enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.enabled = false;
            }
        }

        mainMenu.SetActive(false);
        pauseMenu.SetActive(false);
        endMenu.SetActive(true);
        hud.SetActive(false);
        endMenu.transform.Find("Subtitle").GetComponent<Text>().text =
            won ? "Semua relik telah ditemukan dan para penjaga dikalahkan." :
            timedOut ? "Waktu habis. Hutan masih menunggu." : "Kesehatanmu habis. Hutan masih menunggu.";
        endMenu.transform.Find("Description").GetComponent<Text>().text =
            "Relik terkumpul: " + collectedRelics + " / " + relics.Length +
            "     |     Penjaga dikalahkan: " + CountDefeatedEnemies() + " / " + enemies.Length +
            "\nSkor akhir: " + score;
    }

    private void RefreshHud()
    {
        if (player == null || healthText == null)
        {
            return;
        }

        float ratio = player.MaximumHealth <= 0f ? 0f : Mathf.Clamp01(player.CurrentHealth / player.MaximumHealth);
        healthText.text = "KESEHATAN  " + Mathf.CeilToInt(player.CurrentHealth) + " / " +
                          Mathf.CeilToInt(player.MaximumHealth);
        healthFill.fillAmount = ratio;
        healthFill.color = Color.Lerp(new Color(0.86f, 0.22f, 0.17f), new Color(0.35f, 0.78f, 0.43f), ratio);
        objectiveText.text = "RELIK " + collectedRelics + "/" + relics.Length +
                             "  |  PENJAGA " + CountDefeatedEnemies() + "/" + enemies.Length +
                             "  |  SKOR " + score + "\nWAKTU " +
                             Mathf.FloorToInt(timeRemaining / 60f).ToString("00") + ":" +
                             (Mathf.CeilToInt(timeRemaining) % 60).ToString("00");
    }

    private int CountDefeatedEnemies()
    {
        int defeated = 0;
        foreach (EnemyAI enemy in enemies)
        {
            if (enemy != null)
            {
                EnemyHealth health = enemy.GetComponent<EnemyHealth>();
                if (health != null && health.IsDead)
                {
                    defeated++;
                }
            }
        }

        return defeated;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        BeginNewGame();
    }

    private void QuitGame()
    {
        Application.Quit();
    }

    private void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
