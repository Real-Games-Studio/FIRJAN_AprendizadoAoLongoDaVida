using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScreenGameover : CanvasScreen
{
    [SerializeField] private GameObject gameOverPanel;

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
}