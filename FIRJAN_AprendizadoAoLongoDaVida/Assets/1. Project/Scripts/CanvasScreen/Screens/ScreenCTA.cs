using TMPro;
using UnityEngine;

public class ScreenCTA : CanvasScreen
{
	[SerializeField]
	private ARTrackingImageController trackingController;
	[Header("Localization")]
	[SerializeField] private TMP_Text titleText;
	[SerializeField] private TMP_Text descriptionText;
	[SerializeField] private TMP_Text buttonLabelText;

	public override void OnValidate()
	{
		base.OnValidate();
		EnsureTrackingController();
	}

	public override void OnEnable()
	{
		base.OnEnable();
		EnsureTrackingController();
	}

	public override void TurnOn()
	{
		base.TurnOn();
		UpdateTrackingState(true);
	}

	public override void TurnOff()
	{
		UpdateTrackingState(false);
		base.TurnOff();
	}

	protected override void OnLocalizationApplied()
	{
		base.OnLocalizationApplied();
		SetLocalizedText(titleText, "cta.title");
		SetLocalizedText(descriptionText, "cta.description");
		SetLocalizedText(buttonLabelText, "cta.button");
	}

	private void EnsureTrackingController()
	{
		if (trackingController != null)
		{
			return;
		}

#if UNITY_2023_1_OR_NEWER
		trackingController = FindFirstObjectByType<ARTrackingImageController>();
#else
		trackingController = FindObjectOfType<ARTrackingImageController>();
#endif
	}

	private void UpdateTrackingState(bool suspend)
	{
		if (trackingController == null)
		{
			return;
		}

		trackingController.SetTrackingSuspended(suspend);
	}
}
