using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ATM : Interaction
{
	public ParticleSystem ParticleSystem;

	[System.NonSerialized]
	public PlayerController.Direction InteractionDirection;
}
