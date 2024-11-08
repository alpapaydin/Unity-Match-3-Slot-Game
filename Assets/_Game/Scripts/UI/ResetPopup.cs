using UnityEngine;
using System.Collections;
using System;

public class ResetPopup: MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextAnimator textAnim;
    [SerializeField] private GameObject resetButtonPrefab;

    [Header("Reset Button Animation")]
    [SerializeField] private float impactDuration = 0.1f;
    [SerializeField] private float springDuration = 0.3f;
    [SerializeField] private Vector2 finalScale = new Vector2(15f, 5f);
    [SerializeField] private Vector2 impactScale = new Vector2(20f, 6.5f);
    [SerializeField] private float springiness = 0.2f;
    [SerializeField] private int oscillations = 2;
    [SerializeField] private AudioClip thudSound;

    public event Action OnRestartRequested;

    private GameButton resetButton;
    private AudioSource resetAudioSource;

    void Start()
    {
        if (resetAudioSource == null) {
            resetAudioSource = GetComponent<AudioSource>();
            resetAudioSource.playOnAwake = false;
            resetAudioSource.spatialBlend = 0f;
        }
        if (textAnim != null) { textAnim.OnTextAnimEnd += ShowResetButton; }
    }

    private void ShowResetButton()
    {
        StartCoroutine(ShowGameOverPanel());
    }

    private IEnumerator ShowGameOverPanel()
    {
        yield return new WaitForSeconds(0.5f);
        if (resetButtonPrefab != null)
        {
            GameObject resetObj = Instantiate(resetButtonPrefab, transform);
            resetButton = resetObj.GetComponent<GameButton>();
            resetObj.name = "ResetButton";
            resetButton.transform.localScale = new Vector3 (15,5,1);
            resetButton.transform.localScale = Vector3.zero;
            resetButton.OnClick += ResetPressed;
            resetAudioSource.PlayOneShot(thudSound);
            yield return StartCoroutine(AnimateResetButtonScale(resetButton.transform));
        }
    }

    private void ResetPressed()
    {
        OnRestartRequested?.Invoke();
    }

    private IEnumerator AnimateResetButtonScale(Transform buttonTransform)
    {
        float elapsed = 0f;
        while (elapsed < impactDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / impactDuration;
            progress = 1f - (1f - progress) * (1f - progress);
            Vector2 currentScale = Vector2.Lerp(impactScale, finalScale, progress);
            buttonTransform.localScale = new Vector3(currentScale.x, currentScale.y, 1f);
            yield return null;
        }
        elapsed = 0f;
        Vector2 startScale = buttonTransform.localScale;
        while (elapsed < springDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / springDuration;
            float springEffect = 1f + Mathf.Sin(progress * oscillations * Mathf.PI) * springiness * (1f - progress);
            Vector2 currentScale = Vector2.Lerp(startScale, finalScale, progress);
            currentScale *= springEffect;
            buttonTransform.localScale = new Vector3(currentScale.x, currentScale.y, 1f);
            yield return null;
        }
        buttonTransform.localScale = new Vector3(finalScale.x, finalScale.y, 1f);
    }
}
