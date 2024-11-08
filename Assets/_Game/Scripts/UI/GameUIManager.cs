using UnityEngine;

public class GameUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameBoard gameBoard;
    [SerializeField] private GameObject spinButtonPrefab;
    [SerializeField] private GameObject stopButtonPrefab;

    [Header("UI Settings")]
    [SerializeField] private float buttonHeightPercentage = 0.15f;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip spinSound;
    [SerializeField] private AudioClip stopSound;
    [SerializeField] private AudioClip initializeSpinSound;
    [SerializeField] private AudioClip keepSpinningSound;
    [SerializeField] private AudioClip stopFinalSound;

    private AudioSource audioSource;
    private AudioSource spinAudioSource;
    private GameButton spinButton;
    private GameButton stopButton;
    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
        InstantiateButtons();
        gameBoard.OnSpinComplete += OnBoardSpinComplete;
        gameBoard.OnBoardInitialized += EnableSpinButton;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null || spinAudioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            spinAudioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        spinAudioSource.playOnAwake = false;
        spinAudioSource.spatialBlend = 0f;
        spinAudioSource.loop = true;
        spinAudioSource.clip = keepSpinningSound;
    }

    private void Start()
    {
        gameBoard.OnSpinAnimationStarted += spinAudioSource.Play;
        gameBoard.OnSpinComplete += spinAudioSource.Stop;
        gameBoard.OnSingleColumnStopped += SpinStopSound;
        gameBoard.OnExtrasAppeared += InitializeSpinSound;
    }

    private void InitializeSpinSound()
    {
        audioSource.PlayOneShot(initializeSpinSound);
    }

    private void SpinStopSound()
    {
        audioSource.PlayOneShot(stopFinalSound);
    }

    private void OnDestroy()
    {
        if (gameBoard != null)
        {
            gameBoard.OnSpinComplete -= OnBoardSpinComplete;
            gameBoard.OnBoardInitialized -= EnableSpinButton;
        }

        if (spinButton != null)
            spinButton.OnClick -= OnSpinButtonClicked;
        if (stopButton != null)
            stopButton.OnClick -= OnStopButtonClicked;
    }

    private void InstantiateButtons()
    {
        GameObject spinObj = Instantiate(spinButtonPrefab, transform);
        GameObject stopObj = Instantiate(stopButtonPrefab, transform);
        spinButton = spinObj.GetComponent<GameButton>();
        stopButton = stopObj.GetComponent<GameButton>();
        spinObj.name = "SpinButton";
        stopObj.name = "StopButton";
        SetupButtons();
    }
    private void SetupButtons()
    {
        float screenHeight = 2f * mainCamera.orthographicSize;
        float screenWidth = screenHeight * mainCamera.aspect;
        float buttonHeight = screenHeight * buttonHeightPercentage;
        float buttonWidth = screenWidth * 0.5f;
        float buttonY = -screenHeight / 2f + buttonHeight / 2f;
        spinButton.transform.position = new Vector3(-buttonWidth / 2f, buttonY, 0);
        spinButton.SetSize(buttonWidth, buttonHeight);
        stopButton.transform.position = new Vector3(buttonWidth / 2f, buttonY, 0);
        stopButton.SetSize(buttonWidth, buttonHeight);
        stopButton.IsInteractable = false;
        spinButton.OnClick += OnSpinButtonClicked;
        stopButton.OnClick += OnStopButtonClicked;
    }

    private void OnSpinButtonClicked()
    {
        if (gameBoard == null || gameBoard.IsSpinning) return;
        audioSource.PlayOneShot(spinSound);
        spinButton.IsInteractable = false;
        stopButton.IsInteractable = true;
        gameBoard.StartSpin();
    }

    private void OnStopButtonClicked()
    {
        if (gameBoard == null || !gameBoard.IsSpinning) return;
        audioSource.PlayOneShot(stopSound);
        stopButton.IsInteractable = false;
        gameBoard.StopSpin();
    }

    private void OnBoardSpinComplete()
    {
        EnableSpinButton();
    }

    public void EnableSpinButton()
    {
        spinButton.IsInteractable = true;
        stopButton.IsInteractable = false;
    }

    public void DisableAllButtons()
    {
        spinButton.IsInteractable = false;
        stopButton.IsInteractable = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || mainCamera == null) return;
        float screenHeight = 2f * mainCamera.orthographicSize;
        float screenWidth = screenHeight * mainCamera.aspect;
        float buttonHeight = screenHeight * buttonHeightPercentage;
        float buttonWidth = screenWidth * 0.5f;
        float buttonY = -screenHeight / 2f + buttonHeight / 2f;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(
            new Vector3(-buttonWidth / 2f, buttonY, 0),
            new Vector3(buttonWidth, buttonHeight, 0.1f)
        );
        Gizmos.DrawWireCube(
            new Vector3(buttonWidth / 2f, buttonY, 0),
            new Vector3(buttonWidth, buttonHeight, 0.1f)
        );
    }
#endif
}