using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Booth : Interaction
{
	public SpriteRenderer Merchant;
	public Sprite[] MerchantSprites;
	public Color[] MerchantColors;

	[Space]
	public MeshRenderer Renderer;
	public Material[] BodyMaterials, TopMaterials, LeftMaterials, RightMaterials;
	public Sprite[] MinimapSprites;
	public Sprite MinimapSprite;

	[Space]
	public SpriteRenderer PlushieRenderer;
	public TMPro.TextMeshPro PriceTag;
	public GameObject SoldoutGroup;
	public GameObject AvailableGroup;

	[Space]
	public bool IsConnorBooth;

	public static bool CassAlreadyAppeared;

	public PlushieData PlushieData;

	[System.NonSerialized]
	public PlayerController.Direction InteractionDirection;

	[System.NonSerialized]
	public MinimapBooth MinimapBooth;

	private void LateUpdate()
	{
		Vector3 lookDirection = PlayerController.Instance.Camera.position - this.Merchant.transform.position;
		lookDirection.y = 0;

		this.Merchant.transform.rotation = Quaternion.LookRotation(lookDirection);
	}

	public void Randomize(System.Random random, bool north, bool south, bool west, bool east)
	{
		if (this.Renderer)
		{
			int bodyIndex = random.Next(this.BodyMaterials.Length);

			Material[] materials = this.Renderer.sharedMaterials;
			materials[0] = this.BodyMaterials[bodyIndex];
			materials[1] = this.TopMaterials[random.Next(this.TopMaterials.Length)];
			materials[2] = this.LeftMaterials[random.Next(this.LeftMaterials.Length)];
			materials[3] = this.RightMaterials[random.Next(this.RightMaterials.Length)];
			this.Renderer.sharedMaterials = materials;

			this.MinimapSprite = this.MinimapSprites[bodyIndex];
		}

		int merchantSpriteIndex = random.Next(this.MerchantSprites.Length);
		while (merchantSpriteIndex == 0 && CassAlreadyAppeared)
		{
			merchantSpriteIndex = random.Next(this.MerchantSprites.Length);
		}

		this.Merchant.sprite = this.MerchantSprites[merchantSpriteIndex];

		if (merchantSpriteIndex == 0)
		{
			CassAlreadyAppeared = true;
		}
		else
		{
			//this.Merchant.color = this.MerchantColors[random.Next(this.MerchantColors.Length)];
		}

		if (south)
		{
			this.transform.rotation = Quaternion.Euler(0, 180, 0);
			this.InteractionDirection = PlayerController.Direction.YPositive;
		}
		else if (north)
		{
			this.transform.rotation = Quaternion.Euler(0, 0, 0);
			this.InteractionDirection = PlayerController.Direction.YNegative;
		}
		else if (west)
		{
			this.transform.rotation = Quaternion.Euler(0, -90, 0);
			this.InteractionDirection = PlayerController.Direction.XPositive;
		}
		else if (east)
		{
			this.transform.rotation = Quaternion.Euler(0, 90, 0);
			this.InteractionDirection = PlayerController.Direction.XNegative;
		}
	}

	public void BindPlushie(PlushieData plushie)
	{
		this.PlushieData = plushie;

		this.PlushieRenderer.sprite = plushie.Sprite;
		this.PriceTag.text = $"{plushie.BasePrice}$";
	}

	public void BuyPlushie()
	{
		this.SoldoutGroup.SetActive(true);
		this.AvailableGroup.SetActive(false);

		this.MinimapBooth.PlushImage.enabled = false;
	}

	private void OnDestroy()
	{
		// OnDestroy should be called only when quitting the level, it should work.
		CassAlreadyAppeared = false;
	}
}