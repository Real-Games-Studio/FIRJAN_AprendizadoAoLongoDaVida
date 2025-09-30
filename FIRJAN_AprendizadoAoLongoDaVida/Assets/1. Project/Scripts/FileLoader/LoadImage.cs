using UnityEngine;
using RealGames;
using UnityEngine.UI;

public class LoadImage : MonoBehaviour
{
    public ImageWrapper imageWrapper; // Reference to the ImageWrapper component

    private void OnValidate()
    {
        if (imageWrapper.targetImage == null)
            imageWrapper.targetImage = GetComponent<Image>();
    }
    private void Awake()
    {
        imageWrapper.LoadAndApplySprite();
    }
}
