using UnityEngine;

public class GameUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameBoard gameBoard;  // Must be assigned in inspector
    [SerializeField] private GameObject spinButtonPrefab;
    [SerializeField] private GameObject stopButtonPrefab;

    [Header("UI Settings")]
    [SerializeField] private float buttonHeightPercentage = 0.15f;

    private GameButton spinButton;
    private GameButton stopButton;
    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (gameBoard == null)
        {
            Debug.LogError("GameBoard reference not set in GameUIManager! Please assign it in the inspector.");
            return;
        }

        InstantiateButtons();
        gameBoard.OnSpinComplete += OnBoardSpinComplete;
    }

    private void OnDestroy()
    {
        if (gameBoard != null)
        {
            gameBoard.OnSpinComplete -= OnBoardSpinComplete;
        }

        if (spinButton != null)
            spinButton.OnClick -= OnSpinButtonClicked;
        if (stopButton != null)
            stopButton.OnClick -= OnStopButtonClicked;
    }

    private void InstantiateButtons()
    {
        if (spinButtonPrefab == null || stopButtonPrefab == null)
        {
            Debug.LogError("Button prefabs not assigned to GameUIManager!");
            return;
        }

        GameObject spinObj = Instantiate(spinButtonPrefab, transform);
        GameObject stopObj = Instantiate(stopButtonPrefab, transform);

        spinButton = spinObj.GetComponent<GameButton>();
        stopButton = stopObj.GetComponent<GameButton>();

        if (spinButton == null || stopButton == null)
        {
            Debug.LogError("GameButton component not found on prefab!");
            return;
        }

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

        Debug.Log("Starting spin!");
        spinButton.IsInteractable = false;
        stopButton.IsInteractable = true;

        gameBoard.StartSpin();
    }

    private void OnStopButtonClicked()
    {
        if (gameBoard == null || !gameBoard.IsSpinning) return;

        Debug.Log("Stopping spin!");
        stopButton.IsInteractable = false;

        gameBoard.StopSpin();
    }

    private void OnBoardSpinComplete()
    {
        Debug.Log("Spin complete!");
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
    private void OnValidate()
    {
        if (gameBoard == null)
        {
            Debug.LogWarning("GameBoard reference is missing in GameUIManager!");
        }
    }

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