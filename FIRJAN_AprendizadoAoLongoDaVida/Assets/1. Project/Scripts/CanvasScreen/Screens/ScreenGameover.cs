using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScreenGameover : CanvasScreen
{
    [SerializeField] private GameObject gameOverPanel;
    [Header("Localization")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text finalMessageText;
    [SerializeField] private TMP_Text pillarOneText;
    [SerializeField] private TMP_Text pillarTwoText;
    [SerializeField] private TMP_Text pillarThreeText;
    [SerializeField] private float timeOnScreen = 5f;


    override public void TurnOn()
    {
        base.TurnOn();
        StartCoroutine(HideGameOverAfterDelay());
    }

    IEnumerator HideGameOverAfterDelay()
    {
        yield return new WaitForSeconds(timeOnScreen);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    protected override void OnLocalizationApplied()
    {
        base.OnLocalizationApplied();
        SetLocalizedText(titleText, "gameover.title");
        SetLocalizedText(finalMessageText, "gameover.lastMessage");
        SetLocalizedText(pillarOneText, "gameover.p1");
        SetLocalizedText(pillarTwoText, "gameover.p2");
        SetLocalizedText(pillarThreeText, "gameover.p3");
    }
}
