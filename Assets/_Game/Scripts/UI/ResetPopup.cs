using UnityEngine;

public class ResetPopup: MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextAnimator textAnim;
    [SerializeField] private GameObject resetButtonPrefab;

    void Start()
    {
        if (textAnim != null)
        {
            textAnim.OnTextAnimEnd += ShowResetButton;
        }
    }

    private void ShowResetButton()
    {
        Debug.Log("SHOWING RESET");
    }
}
