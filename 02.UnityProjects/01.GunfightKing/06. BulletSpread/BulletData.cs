using Framework;
using Game.Data;
using System.Collections.Generic;
using UnityEngine;

namespace Game.View.BattleSystem
{
	/// <summary>
	/// Contains all the information about an instanced bullet
	/// </summary>
	public class BulletData
	{
		/// <summary>
		/// The name of the weapon from which this bullet was fired.
		/// </summary>
		public string WeaponName;

		/// <summary>
		/// The base damage that this bullet will cause
		/// </summary>
		public float Damage;

		/// <summary>
		/// The Position from where this bullet was fired.
		/// </summary>
		public Vector3 Position;

		/// <summary>
		/// The amount of force to applied to the hit object in case this have a RigidBody
		/// </summary>
		public float ImpactForce;

		/// <summary>
		/// 패킹된 탄퍼짐 값
		/// </summary>
		public int PackedInaccuracity;

		/// <summary>
		/// Bullet Speed
		/// </summary>
		public float Speed;

		/// <summary>
		/// The max distance that this bullet can travel without hit anything.
		/// </summary>
		public float Range;

		/// <summary>
		/// The amount of bullet drop
		/// </summary>
		public float DropFactor;

		/// <summary>
		/// GunID of the weapon from which this bullet was fire
		/// </summary>
		public WeaponCode WeaponCode;

		/// <summary>
		/// Was this bullet created by a remote player?
		/// </summary>
		public bool isNetwork;

		/// <summary>
		/// Was this bullet created by the actual local player
		/// The difference between this and <see cref="isNetwork"/>
		/// Is that IsNetwork can be False (meaning is not a remote bullet) when a Bot created the bullet
		/// But this ensure that the bullet was created by the real local player.
		/// </summary>
		public bool IsLocalPlayer;

		/// <summary>
		/// The Cached Network View
		/// </summary>
		public int ActorViewID
		{
			get;
			set;
		}

		/// <summary>
		/// The MFPS Actor who create this bullet
		/// </summary>
		public PlayerSeat MFPSActor
		{
			get;
			set;
		}

		/// <summary>
		/// Create the bullet data and fetch info from the <see cref="DamageData"/>
		/// </summary>
		/// <param name="data"></param>
		public BulletData(DamageData data)
		{
			MFPSActor = data.MFPSActor;
			Damage = data.Damage;
			ActorViewID = data.ActorViewID;
			WeaponCode = data.WeaponCode;
			Position = data.Position;
		}

		/// <summary>
		/// Calculate and assign the projectile inaccuracy vector
		/// </summary>
		/// <param name="spreadBase"></param>
		/// <param name="maxSpread"></param>
		/// <returns></returns>
		public BulletData SetInaccuracity(float spreadAngle, float bulletSpeed = 100.0f)
		{
			float halfSpreadAngle = spreadAngle * 0.5f;
			float randomPitch = Random.Range(0, halfSpreadAngle);
			float randomRoll = Random.Range(0, 360);
			PackedInaccuracity = NetworkDataCompressor.CompressInaccuracity(bulletSpeed, randomPitch, randomRoll);
			return this;
		}

		/// <summary>
		/// 정수로 압축된 탄퍼짐 값을 벡터로 변환
		/// </summary>
		/// <param name="packedInaccuracity"></param>
		public static Vector3 ToInaccuracityVector(int packedInaccuracity)
		{
			float bulletSpeed;
			float pitch;
			float roll;

			NetworkDataCompressor.DecompressInaccuracity(packedInaccuracity, out bulletSpeed, out pitch, out roll);

			Quaternion pitchRotation = Quaternion.Euler(pitch, 0, 0);
			Quaternion rollRotation = Quaternion.Euler(0, 0, roll);
			
			// pitch -> roll 순으로 적용
			return rollRotation * pitchRotation * (Vector3.forward * bulletSpeed);

		}
		/// <summary>
		/// 
		/// </summary>
		public BulletData()
		{
		}
	}
}