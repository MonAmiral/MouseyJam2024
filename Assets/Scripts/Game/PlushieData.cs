using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlushieData : ScriptableObject
{
	public string Name;
	public Sprite Sprite;
	public Sprite SilhouetteSprite;

	[TextArea]
	public string Description;
	public PlushieQuality Quality;

	public int BaseScore;
	public int BasePrice;

	public bool IsVChiBan;

	[System.NonSerialized]
	public bool HasBeenFound;

	public string GetDescription()
	{
		string description = this.Description;

		switch (this.Quality)
		{
			case PlushieQuality.Normal:
				break;
			case PlushieQuality.Gold:
				description += "\nThis one is golden! A rare find!";
				break;
			case PlushieQuality.Bad:
				description += "\nThis one is a weird-looking knock-off! Fortunately it's rare!";
				break;
		}

		return description;
	}

	public enum PlushieQuality
	{
		Normal,
		Gold,
		Bad,
	}
}