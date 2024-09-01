using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PersuasionAction : MonoBehaviour
{
	public int PatienceChange, PersuasionChange;
	public int AltChance, AltPatienceChange, AltPersuasionChange;

	public string Animation, AltAnimation;

	public Animator Animator;

	public void UI_Click()
	{
		PlayerController.Instance.UI_ClickPersuasionAction(this);
	}
}