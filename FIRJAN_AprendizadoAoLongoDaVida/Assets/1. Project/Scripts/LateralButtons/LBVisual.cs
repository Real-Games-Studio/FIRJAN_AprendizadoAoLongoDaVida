using TMPro;
using UnityEngine;

public class LBVisual : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI[] texts;
    [SerializeField] private Color targetColor;
    private Color initialColor;

    void Start()
    {
        initialColor = texts[0].color;
    }
    public void OnClick(TextMeshProUGUI text)
    {
        for (int i = 0; i < texts.Length; i++) texts[i].color = initialColor;
        text.color = targetColor;
    }
}
