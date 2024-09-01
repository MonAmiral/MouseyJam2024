using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapBooth : MonoBehaviour
{
	public RectTransform RectTransform;
	public Image BoothImage;
	public Image PlushImage;

	private void LateUpdate()
	{
		this.PlushImage.transform.rotation = Quaternion.identity;
	}
}
