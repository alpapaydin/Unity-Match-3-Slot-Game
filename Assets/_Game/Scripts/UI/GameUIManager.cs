using UnityEngine;
using System.Collections;

public class GameUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameBoard gameBoard;
    [SerializeField] private GameObject spinButtonPrefab;
    [SerializeField] private GameObject stopButtonPrefab;
    [SerializeField] private GameObject movePanelPrefab;
    [SerializeField] private GameObject gameOverPanelPrefab;

    [Header("UI Settings")]
    [SerializeField] private float buttonHeightPercentage = 0.15f;
    [SerializeField] private float panelHeightPercentage = 0.3f;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip spinSound;
    [SerializeField] private AudioClip stopSound;
    [SerializeField] private AudioClip initializeSpinSound;
    [SerializeField] private AudioClip keepSpinningSound;
    [SerializeField] private AudioClip stopFinalSound;
    [SerializeField] private AudioClip failSound;

    private AudioSource audioSource;
    private AudioSource spinAudioSource;
    private GameButton spinButton;
    private GameButton stopButton;
    private Camera mainCamera;
    private MovePanel currentPanel;
    private GameObject gameOverObj;
    private bool isPanelOpen;

    private void Awake()
    {
        mainCamera = Camera.main;
        InstantiateUI();
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
        gameBoard.OnSwipePhaseStarted += OpenPanel;
        gameBoard.OnSwipe += UpdateMoveCount;
        gameBoard.OnGameOver += GameOver;
        gameBoard.OnGameWon += ClosePanel;
    }

    private void UpdateMoveCount(int remaining)
    {
        if (!currentPanel) return;
        currentPanel.SetMoveCount(remaining);
    }

    private void GameOver()
    {
        audioSource.PlayOneShot(failSound);
        StartCoroutine(ShowGameOverPanel());
    }

    private IEnumerator ShowGameOverPanel()
    {
        yield return new WaitForSeconds(2f);
        currentPanel.SetFinal();
        if (gameOverPanelPrefab != null)
        {
            gameOverObj = Instantiate(gameOverPanelPrefab, transform);
            ResetPopup gameOver = gameOverObj.GetComponent<ResetPopup>();
            if (gameOver != null)
            {
                gameOver.OnRestartRequested += RestartGame;
            }
        }
    }

    private void RestartGame()
    {
        Destroy(gameOverObj);
        ClosePanel();
        gameBoard.ResetGame();
    }

    private void InitializeSpinSound()
    {
        audioSource.PlayOneShot(initializeSpinSound);
    }

    private void SpinStopSound()
    {
        audioSource.PlayOneShot(stopFinalSound);
    }

    private void InstantiateUI()
    {
        GameObject spinObj = Instantiate(spinButtonPrefab, transform);
        GameObject stopObj = Instantiate(stopButtonPrefab, transform);
        spinButton = spinObj.GetComponent<GameButton>();
        stopButton = stopObj.GetComponent<GameButton>();
        spinObj.name = "SpinButton";
        stopObj.name = "StopButton";
        SetupUI();
    }

    private void SetupUI()
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

    public void TogglePanel()
    {
        if (isPanelOpen)
            ClosePanel();
        else
            OpenPanel();
    }

    public void OpenPanel()
    {
        if (isPanelOpen || currentPanel != null) return;

        float screenHeight = 2f * mainCamera.orthographicSize;
        float screenWidth = screenHeight * mainCamera.aspect;
        float buttonHeight = screenHeight * buttonHeightPercentage;
        float panelHeight = screenHeight * panelHeightPercentage;
        float buttonY = -screenHeight / 2f + buttonHeight / 2f;
        GameObject panelObj = Instantiate(movePanelPrefab, transform);
        currentPanel = panelObj.GetComponent<MovePanel>();
        float scaleX = screenWidth;
        float scaleY = panelHeight;
        panelObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        Vector3 closedPosition = new Vector3(0f, buttonY - panelHeight / 2f, 0f);
        Vector3 openPosition = new Vector3(0f, buttonY + panelHeight / 2f, 0f);
        currentPanel.Initialize(closedPosition, openPosition, () => isPanelOpen = true);
    }

    public void ClosePanel()
    {
        if (!isPanelOpen || currentPanel == null) return;
        currentPanel.SlideDown(() => {
            Destroy(currentPanel.gameObject);
            currentPanel = null;
            isPanelOpen = false;
        });
    }

    private void OnDestroy()
    {
        if (gameBoard != null)
        {
            gameBoard.OnSpinComplete -= OnBoardSpinComplete;
            gameBoard.OnBoardInitialized -= EnableSpinButton;
            gameBoard.OnSpinAnimationStarted -= spinAudioSource.Play;
            gameBoard.OnSpinComplete -= spinAudioSource.Stop;
            gameBoard.OnSingleColumnStopped -= SpinStopSound;
            gameBoard.OnExtrasAppeared -= InitializeSpinSound;
        }
        if (spinButton != null)
            spinButton.OnClick -= OnSpinButtonClicked;
        if (stopButton != null)
            stopButton.OnClick -= OnStopButtonClicked;
        if (currentPanel != null)
            Destroy(currentPanel.gameObject);
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