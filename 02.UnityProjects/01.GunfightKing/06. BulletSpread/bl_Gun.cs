//#define USE_PENDING_FIRST_SHOT

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MFPS.Core.Motion;
using MFPS.Internal.Structures;
using Game.View;
using Game.View.BattleSystem;
using Game.View.UI;
using Game.Data;
using Framework;
using System.Linq;
using UnityEngine.Serialization;
using Photon.Pun;
using JetBrains.Annotations;
#if ACTK_IS_HERE
using CodeStage.AntiCheat.ObscuredTypes;
#endif

namespace Game.View.BattleSystem
{
	public class bl_Gun : bl_GunBase
	{
		[SerializeField] WeaponSkinRenderGroup[] _skinRenderGroups;
		[FormerlySerializedAs("weaponRenders")]
		[SerializeField] Renderer[] _allRenderers;

		#region Public members
		public GunType GunTypeE; // 에디터에서 Gun Prefab 설정 편의만을 위해 사용됨.
		public bl_CustomGunBase customWeapon;
		public BulletInstanceMethod bulletInstanceMethod = BulletInstanceMethod.Pooled;
		public string BulletName = "bullet";
		public GameObject bulletPrefab;
		public GameObject grenade = null;      // the grenade style round... this can also be used for arrows or similar rounds
		public ParticleSystem muzzleFlash = null;    // the muzzle flash for this weapon
		public Transform muzzlePoint = null;    // the muzzle point of this weapon
		public bl_WeaponFXBase weaponFX = null;
		public ParticleSystem shell = null;       // the weapons empty shell particle
		public GameObject impactEffect = null;  // impact effect, used for raycast bullet types 
		public Vector3 AimPosition; //position of gun when is aimed
		public Vector3 aimRotation = Vector3.zero;
		public bool useSmooth = true;
		public float AimSmooth;
		public ShakerPresent shakerPresent;
		[Range(0, 179)]
		public float aimZoom = 50;
		public bool CanAuto = true;
		public bool CanSemi = true;
		public bool CanSingle = true;
		//Shotgun Specific Vars
		public int pelletsPerShot = 10;      // number of pellets per round fired for the shotgun
		public float delayForSecondFireSound = 0.45f;
		//Burst Specific Vars
		public int roundsPerBurst = 3;        // number of rounds per burst fire
		public float lagBetweenBurst = 0.5f;    // time between each shot in a burst
												//Launcher Specific Vars
		public List<GameObject> OnAmmoLauncher = new List<GameObject>();
		public bool ThrowByAnimation = false;
		public int impactForce = 50;            // how much force applied to a rigid body
		public float bulletSpeed = 200.0f;    // how fast are your bullets
		public float bulletDropFactor = 0.2f;
		public bool AutoReload = true;
		public bool m_AllowQuickFire = true;
		public ReloadPer reloadPer = ReloadPer.Bullet;
		public int bulletsPerClip = 50;      // number of bullets in each clip
		public int numberOfClips = 5;
		public int maxNumberOfClips = 10;      // maximum number of clips you can hold
		public float DelayFire = 0.85f;
		public float delayFireOnSprinting = 0.12f; // fire delay in the first shot when the player is sprinting.
												   //public Vector2 spreadMinMax = new Vector2(1, 3);

		public float spreadAimMultiplier = 0.5f;
		public float spreadPerSecond = 0.2f;    // if trigger held down, increase the spread of bullets
		public float SpreadAngle => GetSpreadAngle;
		public float decreaseSpreadPerSec = 0.5f;// amount of accuracy regained per frame when the gun isn't being fired 
		[HideInInspector] public bool isReloading = false;     // am I in the process of reloading
															   // Recoil
		public float RecoilAmount = 5.0f;
		public float RecoilSpeed = 2;
		public bool SoundReloadByAnim = false;
		public AudioClip TakeSound;
		public AudioClip FireSound;
		public AudioClip DryFireSound;
		public AudioClip ReloadSound;
		public AudioClip ReloadSound2 = null;
		public AudioClip ReloadSound3 = null;
		public AudioSource DelaySource = null;
		//cached player components
		public bl_PlayerSettings playerSettings;
		
		public WeaponSkinRenderGroup[] SkinRenderGroups => _skinRenderGroups;
		#endregion

		public Renderer[] AllRenderers
		{
			get 
			{
				if (!_allRenderers.HasContent())
				{
					DebugEx.Warning($"Prefab[{name}] AllRenders empty.");
					_allRenderers = GetComponentsInChildren<Renderer>();
				}
				return _allRenderers;
			}
			set => _allRenderers = value;
		}

		// 스코프 유무 (Aim 가능 유무)
		public bool HasScope { get; set; }
		
		#region Public properties
		public int DamageEx
		{
			get 
			{
				int dmg = WeaponDef.Damage + extraDamage;
#if DEBUG
				if(GameCheatValues.BattleCheat.HasFlag(CheatFlags.Damagex10)) { dmg *= 10; }
#endif
				return dmg;
			}
			set { extraDamage = value; }
		}

		public int ExtraDamage
		{
			get { return extraDamage; }
			set { extraDamage = value; }
		}

		//탄창 수 
		public int ExtraClips
		{
			get => _extraClips;
			set  
			{
				if (_ammoInitialized)
				{
					int delta = Mathf.Max(0, value - _extraClips);
					_extraClips = value;

					AddClip(delta);
				}
				else
				{
					_extraClips = value;
				}
			}
		}
		/// <summary>
		/// 탄창까지 모두 포함한 갯수
		/// </summary>
		public int BulletsLeftTotal => bulletsLeft + RemainingClipBullets * bulletsPerClip;

		bool _ammoInitialized;

		Coroutine _grenadeThrowCoroutine;
		
#if !ACTK_IS_HERE
		private int _remainingClipBullets;
		public int RemainingClipBullets
		{
			get => _remainingClipBullets;
			set => _remainingClipBullets = value;
		}
#else
	private ObscuredInt m_remainingClips = 5;
	public ObscuredInt RemainingClips
	{
		get => m_remainingClips;
		set => m_remainingClips = value;
	}
#endif

		public float Zoom
		{
			get { return aimZoom + extraZoom; }
			set { extraZoom = value; }//don't modify the base zoom, only the extra value
		}

		public int BulletsPerMagazine
		{
			get { return bulletsPerClip + extraBullets; }
			set { extraBullets = value; }//don't modify the base bullets, only the extra value
		}

		public float MaxSpread
		{
			get { return extraSpread; }
			set { extraSpread = value; }//don't modify the base bullets, only the extra value
		}
		/*--
		private float extraWeight = 0;
		public float WeaponWeight
		{
			get => Info.Weight + extraWeight;
			set => extraWeight = value;
		}
		--*/

		private float extraRecoil = 0;
		public float WeaponRecoil
		{
			get => RecoilAmount + extraRecoil;
			set => extraRecoil = value;
		}

		private float extraAimSpeed = 0;
		public float AimSpeed
		{
			get => AimSmooth + extraAimSpeed;
			set => extraAimSpeed = value;
		}

		private float extraReloadTime = 0;
		public float ReloadTimeEx
		{
			get => WeaponDef.ReloadTime + extraReloadTime;
		}

		public float ExtraReloadTime
		{
			get => extraReloadTime;
			set => extraReloadTime = Mathf.Max(-WeaponDef.ReloadTime, value);
		}

		private float extraBulletSpeed = 0;
		public float BulletSpeed
		{
			get => bulletSpeed + extraBulletSpeed;
			set => extraBulletSpeed = value;
		}

		private int extraRange = 0;
		public int Range
		{
			get => WeaponDef.Range + extraRange;
			set => extraRange = value;
		}

		/// <summary>
		/// 서 있는 경우의 탄퍼짐 각도. 탄퍼짐 로직의 기준값이다.
		/// </summary>
		public float DefaultSpreadAngle => WeaponDef.DefaultSpreadAngle;

		/// <summary>
		/// Zoom모드에서, 서 있는 경우의 탄퍼짐 각도. 탄퍼짐 로직의 기준값이다.
		/// </summary>
		public float DefaultSpreadAngleForZoom => WeaponDef.DefaultSpreadAngleForZoom;

		//앉아 있을 때 spreadAngle 배수
		public float SpreadSitMultiply => WeaponDef.SpreadSitMultiply;

		// 걷고 있을 때 spreadAngle 배수
		public float SpreadWalkMultiply => WeaponDef.SpreadWalkMultiply;

		// 뛰고 있을 때 spreadAngle 배수
		public float SpreadRunMultiply => WeaponDef.SpreadRunMultiply;

		

		public AudioClip FireAudioClip
		{
			get => FireSound;
			set
			{
				if (defaultFireSound == null) defaultFireSound = FireSound;
				FireSound = value;
				if (FireSound == null) FireSound = defaultFireSound;
			}
		}

		public float nextFireTime { get; set; }
		public Camera PlayerCamera { get; private set; }
		public bool BlockAimFoV { get; set; }
		public bool HaveInfinityAmmo { get; private set; }
		public System.Action<bool> onWeaponRendersActive;
		#endregion

		#region Private members
		private bool m_enable = true;
		public bool canBeTakenWhenIsEmpty = true;
		private bool alreadyKnife = false;
		private AudioSource Source;
		private Camera WeaponCamera;
		private bool inReloadMode = false;
		private AudioSource FireSource = null;
		private bool isInitialized = false;
		private Transform m_Transform;
		public BulletData BulletSettings = new BulletData();
		public PlayerFPState FPState = PlayerFPState.Idle;
		public int extraDamage, extraBullets = 0;
		private Transform defaultMuzzlePoint = null;
		private bool lastAimState = false;
		private float currentZoom, extraZoom, extraSpread = 0;
		private bool grenadeFired = false;
		GameObject instancedBullet = null;
		RaycastHit hit;
		private Vector3 DefaultPos;
		private Vector3 CurrentPos;
		private Quaternion currentRotation, defaultRotation;
		private bool isBursting = false;
		private static BulletInstanceData bulletInstanceData;
		private AudioClip defaultFireSound;
		private float lastShotTime = 0;
		private bool m_canFire = false;
		private bool m_canAim;
		private GunType currentGunType = GunType.None;
		int _extraClips;
		#endregion

		/// <summary>
		/// 
		/// </summary>
		protected override void Awake()
		{
			base.Awake();

			WeaponCode = CurrentLoadingWeaponCode;

			// 정상 실행이 아닌 실험적 상황인 경우의 방어코드.
			if (WeaponCode == WeaponCode.Invalid)
			{ WeaponCode = GetDummyWeaponCode(); }

			WeaponCodeDef = WeaponCodeCDB.Instance.GetDef((int)WeaponCode);
			WeaponDef = WeaponCodeDef.WeaponDef;

			// 총기류의 총탄 속도
			if (WeaponDef.GunType.IsOneOf(GunType.Sniper, GunType.Pistol, GunType.Burst, GunType.Machinegun, GunType.Shotgun))
			{
				bulletSpeed = GameViewSettings.Instance.GetBulletSpeed(WeaponDef.WeaponType);
			}

			WeaponUtil.ApplySkin(_skinRenderGroups, WeaponCodeDef);

			var battleMgr = BattleManager.Instance;

			// 수류탄 수량은 인벤토리에서 가져온다.
			if (WeaponDef.SlotType.IsConsumableWeaponSlot())
			{
				bulletsPerClip = 1;
				int maxOnDataSheet = WeaponDef.AmmoPerClip * WeaponDef.Clips;

				// Offline모드는 항상 2개씩 다시 채운다.
				if (PhotonNetwork.OfflineMode)
				{
					numberOfClips = maxOnDataSheet;
				}
				else
				{
					int itemCount = ItemCounter.GetConsumableWeaponCount(WeaponDef.No);

					// 이번 전투에서 소모한 수는 뺀다.
					itemCount = Mathf.Max(0, itemCount - battleMgr.GetConsumableItemUseCount(WeaponDef.No));

					// 라운드 별 최대 지급 수로 제한				
					itemCount = Mathf.Min(itemCount, maxOnDataSheet);
					numberOfClips = itemCount;
				}
			}
			else
			{
				bulletsPerClip = WeaponDef.AmmoPerClip;
				numberOfClips = WeaponDef.Clips;
			}

			maxNumberOfClips = WeaponDef.MaxClips;
			Initialize();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			if (_grenadeThrowCoroutine != null)
			{
				StopCoroutine(_grenadeThrowCoroutine);
			}
		}

		/// <summary>
		/// This is called from the Weapon Manager at start for all the equipped weapons.
		/// </summary>
		public void Initialize()
		{
			if (isInitialized)
			{
				FireSource.volume = Source.volume;
				return;
			}

			m_Transform = transform;
			playerSettings = PlayerReferences.playerSettings;

			if (FireSource == null) FireSource = PlayerReferences.gunManager.GetFireAudioSource();
			Source = GetComponent<AudioSource>();
			FireSource.volume = Source.volume;
			PlayerCamera = PlayerReferences.playerCamera;

			if (isMine && CrosshairCtl.Instance)
			{
				CrosshairCtl.Instance.Block = false;
			}
			Setup();
			customWeapon?.Initialitate(this);
			isInitialized = true;
		}

		/// <summary>
		/// 
		/// </summary>
		protected override void OnEnable()
		{
#if MFPSM
			ConnectToMobileControl();
#if UNITY_EDITOR
			bl_UtilityHelper.OnTestMobileInputOnEditorChanged += OnTestMobileInputOnPCChanged;
#endif

#endif

			if (!isInitialized) return;

			base.OnEnable();
			PlayClip(TakeSound);
			CanFire = false;
			UpdateUI();
			if (WeaponAnimation)
			{
				float t = WeaponAnimation.PlayTakeIn();

				if (WeaponType == GunType.Grenade)
				{
					DrawComplete();
				}
				else
				{
					Invoke(nameof(DrawComplete), t);
				}
			}
			else
			{
				DrawComplete();
			}
			bl_EventHandler.onAmmoPickUp += this.OnPickUpAmmo;
			bl_EventHandler.onRoundEnd += this.OnRoundEnd;
			if (CrosshairCtl.Instance)
			{
				CrosshairCtl.Instance.SetupCrosshairForWeapon(this);
			}
			bl_EquippedWeaponUIBase.Instance?.SetFireType(GetFireType());
			
			OnAmmoLauncher.ForEach(x => { x?.SetActive(true); });
#if MFPSTPV
		var tpScript = PlayerReferences.GetComponent<bl_PlayerCameraSwitcher>();
		if (tpScript != null)
		{
			SetWeaponRendersActive(!bl_CameraViewSettings.IsThirdPerson());
		}
#endif
		}

		/// <summary>
		/// 
		/// </summary>
		protected override void OnDisable()
		{
			base.OnDisable();
			bl_EventHandler.onAmmoPickUp -= this.OnPickUpAmmo;
			bl_EventHandler.onRoundEnd -= this.OnRoundEnd;
#if MFPSM
			DisconnectFromMobileControl();
#if UNITY_EDITOR
			bl_UtilityHelper.OnTestMobileInputOnEditorChanged -= OnTestMobileInputOnPCChanged;
#endif
#endif
			alreadyKnife = false;
			if (PlayerCamera == null) { PlayerCamera = PlayerReferences.playerCamera; }
			StopAllCoroutines();
			if (isReloading) { inReloadMode = true; isReloading = false; }
			if(isAiming)
			{
				isAiming = false;
				Aim(true);
			}
			isFiring = false;
			lastAimState = false;
			ResetDefaultMuzzlePoint();
		}

		/// <summary>
		/// Called by the weapon manager on all the equipped weapons
		/// even when they have not been enabled
		/// </summary>
		public void Setup(bool initial = false)
		{
			// 탄약 초기화
			InitAmmo();

			if (WeaponType != GunType.Shotgun && WeaponType != GunType.Sniper)
			{
				reloadPer = ReloadPer.Magazine;
			}

			DefaultPos = transform.localPosition;
			defaultRotation = transform.localRotation;
			if (PlayerCamera == null) { PlayerCamera = PlayerReferences.playerCamera; }
			WeaponCamera = PlayerReferences.weaponCamera;
			WeaponCamera.fieldOfView = bl_MFPS.Settings != null ? (float)bl_MFPS.Settings.GetSettingOf("Weapon FOV") : 55;
			CanAiming = true;

			// @TODO: Change this with more elaborated fire type system
			// Temporal fix to the initial fire type setup
			if (WeaponType == GunType.Machinegun && !CanAuto)
			{
				if (CanSemi) currentGunType = GunType.Burst;
				else if (CanSingle) currentGunType = GunType.Pistol;
			}
			else if (WeaponType == GunType.Pistol && !CanSingle)
			{
				if (CanSemi) currentGunType = GunType.Burst;
				else if (CanAuto) currentGunType = GunType.Machinegun;
			}
			else if (WeaponType == GunType.Burst && !CanSemi)
			{
				if (CanAuto) currentGunType = GunType.Machinegun;
				else if (CanSingle) currentGunType = GunType.Pistol;
			}
		}

		/// <summary>
		/// check what the player is doing every frame
		/// </summary>
		/// <returns></returns>
		public override void OnUpdate()
		{
			if (!m_enable)
				return;

			InputUpdate();
			Aim();
			DetermineUpperState();
			/*--
			if (isFiring) // if the gun is firing
			{
				spread += (PlayerReferences.firstPersonController.State == PlayerState.Crouching) ? spreadPerSecond * 0.5f : spreadPerSecond; // gun is less accurate with the trigger held down
			}
			else
			{
				spread -= decreaseSpreadPerSec; // gun regains accuracy when trigger is released
			}
			spread = Mathf.Clamp(spread, BaseSpread + extraSpread, SpreadMaxAngle + extraSpread);
			--*/
		}

		void InitAmmo()
		{
			bulletsLeft = numberOfClips > 0 ? BulletsPerMagazine : 0;

			// 남은 탄창의 탄알수.  탄창 1개분은 적용했으니 뺀다.
			_remainingClipBullets = BulletsPerMagazine * Mathf.Max(0, numberOfClips - 1);

			if (_extraClips > 0)
			{
				AddClip(_extraClips);
			}

			if (WeaponType == GunType.Knife)
			{
				bulletsLeft = 1;
			}

			_ammoInitialized = true;
		}

		/// <summary>
		/// All Input events 
		/// </summary>
		void InputUpdate()
		{
			if (GameSettings.Instance.isChating || !gunManager.isGameStarted || (!IsCursorLocked() && !bl_UtilityHelper.TestMobileInputOnEditor)) return;

			if (gunManager.IsGrenadeFiring())
			{
				return;
			}

			bool fireDown = bl_GameInput.Fire(GameInputType.Down);
			bool fireUp = bl_GameInput.Fire(GameInputType.Up);
			bool grenadeFireUp = bl_GameInput.WeaponSlot(4, GameInputType.Up);
			bool tacticalFireUp = bl_GameInput.WeaponSlot(5, GameInputType.Up);

			if (bl_UtilityHelper.CanAutoFire())
			{
				HandleAutoFire();
			}

			if (bl_UtilityHelper.UseMobileControl)
			{
				if (IsLoopFirableWeapon(WeaponType))
				{
					bool doFire = MobileControlsUi.Instance != null && MobileControlsUi.Instance.FireDown && CanFire;

					if (doFire)
					{
						LoopFire();

						AutoWeaponFire.OnManualFire();
					}
				}
			}
			else
			{
				if (CanFire)
				{
					// if press once
					if (WeaponDef.SlotType == WeaponSlotType.Grenade)
					{
						if (WeaponType == GunType.Grenade
							&& (grenadeFireUp || fireUp))
						{
							SingleFire();
						}
					}
					else if (WeaponDef.SlotType == WeaponSlotType.Tactical)
					{
						if (WeaponType == GunType.Grenade
							&& (tacticalFireUp || fireUp))
						{
							SingleFire();
						}
					}
					else
					{
						if (fireDown) SingleFire();
						// if keep pressing
						if (bl_GameInput.Fire()) LoopFire();
					}
				}
				else
				{
					if (fireDown && bulletsLeft <= 0 && !isReloading)//if try fire and don't have more bullets
					{
						PlayEmptyFireSound();
					}
				}
			}

			if (fireDown && isReloading)//if try fire while reloading 
			{
				if (WeaponType == GunType.Sniper || WeaponType == GunType.Shotgun)
				{
					if (bulletsLeft > 0)//and has at least one bullet
					{
						CancelReloading();
					}
				}
			}

			if (bl_UtilityHelper.UseMobileControl)
			{
				isAiming = MobileControlsUi.Instance.isAim && CanAiming;
			}
			else
			{
				isAiming = bl_GameInput.Aim() && CanAiming;
			}

			if (IsCursorLocked())
			{
				// ui가 없는 경우 고려
				if (CrosshairCtl.Instance)
				{
					CrosshairCtl.Instance.OnAim(isAiming);
				}
			}

			if (bl_GameInput.Reload() && CanReload)
			{
				Reload();
			}

			if (WeaponType == GunType.Machinegun || WeaponType == GunType.Burst || WeaponType == GunType.Pistol)
			{
				ChangeTypeFire();
			}

			//used to decrease weapon accuracy as long as the trigger remains down
			if (WeaponType != GunType.Grenade && WeaponType != GunType.Knife)
			{
				if (bl_UtilityHelper.UseMobileControl)
				{

					if (!bl_UtilityHelper.CanAutoFire() && MobileControlsUi.Instance != null)
					{
						isFiring = (MobileControlsUi.Instance.FireDown && CanFire);
					}
				}
				else
				{
					if (WeaponType == GunType.Machinegun)
					{
						isFiring = (bl_GameInput.Fire() && CanFire); // fire is down, gun is firing
					}
					else
					{
						if (fireDown && CanFire)
						{
							isFiring = true;
							CancelInvoke(nameof(CancelFiring));
							Invoke(nameof(CancelFiring), 0.12f);
						}
					}
				}
			}
		}

		/// <summary>
		/// change the type of gun gust
		/// </summary>
		void ChangeTypeFire()
		{
			bool inp = bl_GameInput.SwitchFireMode();
			if (inp)
			{
				switch (WeaponType)
				{
					case GunType.Machinegun:
						if (CanSemi) WeaponType = GunType.Burst;
						else if (CanSingle) WeaponType = GunType.Pistol;
						break;
					case GunType.Burst:
						if (CanSingle) WeaponType = GunType.Pistol;
						else if (CanAuto) WeaponType = GunType.Machinegun;
						break;
					case GunType.Pistol:
						if (CanAuto) WeaponType = GunType.Machinegun;
						else if (CanSemi) WeaponType = GunType.Burst;
						break;
				}
				bl_EquippedWeaponUIBase.Instance.SetFireType(GetFireType());
				gunManager.PlaySound(0);
			}
		}

		/// <summary>
		/// Called by mobile button event
		/// </summary>
		void OnFire()
		{
			if (!gunManager.isGameStarted) return;

			if (bulletsLeft <= 0 && !isReloading)
			{
				PlayEmptyFireSound();
			}

			if (isReloading)
			{
				if (WeaponType == GunType.Sniper || WeaponType == GunType.Shotgun)
				{
					if (bulletsLeft > 0)
					{
						CancelReloading();
					}
				}
			}

			if (!CanFire)
				return;

			SingleFire();
		}

		/// <summary>
		/// 
		/// </summary>
		void HandleAutoFire()
		{
			if (!CanFire) return;
			if (WeaponType == GunType.Grenade && isFiring) { return; }

			bool fireDown = AutoWeaponFire.Instance.TryFire(this);

			// 샷건은 탄퍼짐 각도가 일반적으로 4배가 더 크므로 3번 더 시도
			if (currentGunType == GunType.Shotgun)
			{
				for (int i = 0; !fireDown && i < 3; i++)
				{
					fireDown = AutoWeaponFire.Instance.TryFire(this);
				}
			}

			isFiring = fireDown;
			if (fireDown)
			{
				if (WeaponType == GunType.Machinegun)
					LoopFire();
				else
					SingleFire();
			}

		}

		/// <summary>
		/// Fire one time
		/// </summary>
		void SingleFire()
		{
			bool fireRatePassedOnRun = Time.time - lastShotTime > (WeaponDef.FireCycle * 2);
			if (WeaponType == GunType.Grenade)
			{
				fireRatePassedOnRun = FireRatePassed;
			}

			if (PlayerReferences.firstPersonController.State == PlayerState.Running && fireRatePassedOnRun)
			{
				isFiring = true;
				GunTypeFire(false);
			}
			else
			{
				GunTypeFire(false);
			}
			lastShotTime = Time.time;
		}

		// 연사 가능한 무기 타입인가
		bool IsLoopFirableWeapon(GunType gunType)
		{
			return WeaponType.IsOneOf(GunType.Machinegun, GunType.Shotgun, GunType.Sniper, GunType.Knife);
		}

		/// <summary>
		/// Fire continuously
		/// </summary>
		void LoopFire()
		{
			if (!IsLoopFirableWeapon(WeaponType))
			{
				return;
			}

			if (PlayerReferences.firstPersonController.State == PlayerState.Running && Time.time - lastShotTime > (WeaponDef.FireCycle * 5))
			{
				isFiring = true;

				this.InvokeAfter(delayFireOnSprinting, () =>
				{
					if (!bl_GameInput.Fire(GameInputType.Down)) return;

					GunTypeFire(true);
				});
			}
			else
			{
				GunTypeFire(true);
			}
			lastShotTime = Time.time;
		}

		/// <summary>
		/// Call the fire function depending on the weapon type
		/// </summary>
		/// <param name="auto">automatic fire?</param>
		void GunTypeFire(bool auto)
		{
			if (customWeapon != null) { customWeapon.OnFireDown(); }
			else
			{
				switch (WeaponType)
				{
					case GunType.Machinegun:
						if (auto) MachineGunFire();
						break;
					case GunType.Shotgun:
						ShotgunFire();
						break;
					case GunType.Burst:
						if (!isBursting) { StartCoroutine(BurstFire()); }
						break;
					case GunType.Grenade:
						if (!grenadeFired) { GrenadeFire(); }
						break;
					case GunType.HandItem:
						/// 진입되지 않는다. <see cref="MobileControlsUi.DoFire_HandItem()"/> 참고.
						break;
					case GunType.Pistol:
						MachineGunFire();
						break;
					case GunType.Sniper:
						SniperFire();
						break;
					case GunType.Knife:
						if (!alreadyKnife) { KnifeFire(); }
						break;
				}
			}
		}

		#region Fire Handlers
		/// <summary>
		/// fire the machine gun
		/// </summary>
		void MachineGunFire()
		{
			// If there is more than one bullet between the last and this frame
			float time = Time.time;

			if (nextFireTime > time) return;
			nextFireTime = time + (WeaponDef.FireCycle * 0.1f); // multiply by 0.1 just to add compatibility (keep the same fire rate) with older versions of MFPS.

			FireOneShot();

			//Play fire animation
			if (isAiming) WeaponAnimation?.PlayFire(bl_WeaponAnimationBase.AnimationFlags.Aiming);
			else WeaponAnimation?.PlayFire();

			OnFireCommons();
			PlayFX();

			//is Auto reload
			if (bulletsLeft <= 0 && _remainingClipBullets > 0 && AutoReload)
			{
				Reload();
			}
		}

		/// <summary>
		/// fire the sniper gun
		/// </summary>
		void SniperFire()
		{
			float time = Time.time;
			if (time - WeaponDef.FireCycle > nextFireTime)
				nextFireTime = time - Time.deltaTime;

			// Keep firing until we used up the fire time
			while (nextFireTime < time)
			{
				FireOneShot();
				WeaponAnimation?.PlayFire();
				StartCoroutine(DelayFireSound());
				OnFireCommons();
				PlayFX();
				//is Auto reload
				if (bulletsLeft <= 0 && _remainingClipBullets > 0 && AutoReload)
				{
					Reload(delayForSecondFireSound + 0.2f);
				}
				else if (bulletsLeft > 0 && isAiming)
				{
					//CHECKIT(ukyang) 테스트 중
					//SniperReloadBullet();
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		private float KnifeFire(bool quickFire = false)
		{
			// If there is more than one shot  between the last and this frame
			// Reset the nextFireTime
			if (Time.time - WeaponDef.FireCycle > nextFireTime)
				nextFireTime = Time.time - Time.deltaTime;

			float time = 0;
			// Keep firing until we used up the fire time
			while (nextFireTime < Time.time)
			{
				isFiring = true; // fire is down, gun is firing
				alreadyKnife = true;
				StartCoroutine(KnifeSendFire());

				Vector3 position = PlayerCamera.transform.position;
				Vector3 direction = PlayerCamera.transform.TransformDirection(Vector3.forward);

				RaycastHit hit;
				float range = PlayerReferences.cameraRay == null ? WeaponDef.Range : WeaponDef.Range + PlayerReferences.cameraRay.ExtraRayDistance;
				if (Physics.SphereCast(position, 0.2f, direction, out hit, range))
				{
					if (hit.transform.CompareTag(bl_MFPS.HITBOX_TAG))
					{
						var bp = hit.transform.GetComponent<IMFPSDamageable>();
						var damageData = new DamageData();
						damageData.Damage = DamageEx;
						damageData.Actor = LocalPlayer;
						damageData.MFPSActor = BattleManager.Instance.LocalPlayerSeat;
						damageData.Cause = DamageCause.Player;
						damageData.Position = transform.position;
						damageData.WeaponCode = WeaponCode;
						damageData.ActorViewID = BattleManager.LocalPlayerViewID;
						damageData.From = LocalPlayer.NickName;
						bp?.ReceiveDamage(damageData);

						bl_ObjectPoolingBase.Instance.Instantiate("blood", hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
					}
					else
					{
						var damageable = hit.transform.GetComponent<IMFPSDamageable>();
						if (damageable != null)
						{
							DamageData damageData = new DamageData()
							{
								Damage = DamageEx,
								Position = m_Transform.position,
								MFPSActor = BattleManager.Instance.LocalPlayerSeat,
								ActorViewID = BattleManager.LocalPlayerViewID,
								WeaponCode = WeaponCode,
								From = LocalPlayer.NickName,
							};
							damageData.Cause = DamageCause.Player;
							damageable.ReceiveDamage(damageData);
						}
					}
				}


				if (WeaponAnimation != null)
				{
					if (quickFire) time = WeaponAnimation.PlayFire(bl_WeaponAnimationBase.AnimationFlags.QuickFire);
					else time = WeaponAnimation.PlayFire();
				}

				PlayerNetwork.ReplicateFire(GunType.Knife, Vector3.zero, 0);
				OnFireCommons();
				// ui가 없는 경우 고려
				if (CrosshairCtl.Instance)
				{
					CrosshairCtl.Instance.OnFire();
				}
				isFiring = false;
				bl_EventHandler.DispatchLocalPlayerFire(WeaponCode);
			}
			return time;
		}

		/// <summary>
		/// burst shooting
		/// </summary>
		/// <returns></returns>
		IEnumerator BurstFire()
		{
			int shotCounter = 0;
			// If there is more than one bullet between the last and this frame
			// Reset the nextFireTime
			if (Time.time - lagBetweenBurst > nextFireTime)
				nextFireTime = Time.time - Time.deltaTime;

			int shots = Mathf.Min(roundsPerBurst, bulletsLeft);
			// Keep firing until we used up the fire time
			while (nextFireTime < Time.time)
			{
				while (shotCounter < shots)
				{
					isBursting = true;
					FireOneShot();
					shotCounter++;
					OnFireCommons();
					PlayFX();

					//Play fire animation
					if (isAiming) WeaponAnimation?.PlayFire(bl_WeaponAnimationBase.AnimationFlags.Aiming);
					else WeaponAnimation?.PlayFire();

					yield return new WaitForSeconds(WeaponDef.FireCycle);
					if (bulletsLeft <= 0) { break; }
				}

				nextFireTime += lagBetweenBurst;
				//is Auto reload
				if (bulletsLeft <= 0 && _remainingClipBullets > 0 && AutoReload)
				{
					Reload();
				}
			}
			isBursting = false;
		}

		/// <summary>
		/// fire the shotgun
		/// </summary>
		void ShotgunFire()
		{
			// If there is more than one bullet between the last and this frame
			// Reset the nextFireTime
			if (Time.time - WeaponDef.FireCycle > nextFireTime)
				nextFireTime = Time.time - Time.deltaTime;

			int pelletCounter = 0;  // counter used for pellets per round
									// Keep firing until we used up the fire time
			while (nextFireTime < Time.time)
			{
				do
				{
					FireOneShot();
					pelletCounter++; // add another pellet		 
				} while (pelletCounter < pelletsPerShot); // if number of pellets fired is less then pellets per round... fire more pellets

				if (!SoundReloadByAnim)
					StartCoroutine(DelayFireSound());
				else
					PlayFireAudio();

				WeaponAnimation?.PlayFire();
				OnFireCommons();
				PlayFX();
				//is Auto reload
				if (bulletsLeft <= 0 && _remainingClipBullets > 0 && AutoReload)
				{
					Reload(delayForSecondFireSound + 0.3f);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		void GrenadeFire(bool fastFire = false)
		{
			if (grenadeFired || !FireRatePassed)
				return;

			if (!fastFire && bulletsLeft == 0 && _remainingClipBullets > 0)
			{
				Reload(1); // if out of ammo, reload
				return;
			}
			isFiring = true;
			grenadeFired = true;

			// 던지는 애니메이션을 동기화하기 위해 추가.
			PlayerNetwork.ReplicateFire(GunType.Grenade, Vector3.zero, 0);

			if (ThrowByAnimation)
			{
				float coolTime = WeaponDef.FireCycle;
				if (bulletsLeft == 1)
				{
					coolTime = Mathf.Max(ReloadTimeEx, WeaponDef.FireCycle);
				}
				nextFireTime = Time.time + coolTime;
				if (fastFire) WeaponAnimation?.PlayFire(bl_WeaponAnimationBase.AnimationFlags.QuickFire);
				else WeaponAnimation?.PlayFire();
			}
			else
			{
				if (_grenadeThrowCoroutine != null)
				{
					StopCoroutine(_grenadeThrowCoroutine);
				}
				
				_grenadeThrowCoroutine = 
					StartCoroutine(ThrowGrenadeOrUseHandItem(fastFire, true));
			}

			if (GetGameMode.IsNotOneOf(BattleMode.Practice, BattleMode.Tutorial))
			{
				BattleManager.Instance.AddConsumableItemUseCount(WeaponDef.No);
			}
		}

		// UVA 사용.
		void HandItemFire(System.Action<bool> onEnd)
		{
			if (grenadeFired || !FireRatePassed)
			{
				onEnd(false);
				return;
			}
			if (bulletsLeft == 0 && _remainingClipBullets > 0)
			{
				Reload(1);
				onEnd(false);
				return;
			}
			isFiring = true;
			grenadeFired = true;

			// (만약 있다면) 조작 애니메이션을 동기화하기 위해 추가.
			PlayerNetwork.ReplicateFire(GunType.HandItem, Vector3.zero, 0);

			if (ThrowByAnimation)
			{
				nextFireTime = Time.time + WeaponDef.FireCycle;
				gunManager.SetConsumableCommonNextFireTime();

				WeaponAnimation?.PlayFire();
			}
			else { StartCoroutine(ThrowGrenadeOrUseHandItem(false, true, onEnd)); }

			if(GetGameMode.IsNotOneOf(BattleMode.Practice, BattleMode.Tutorial))
			{
				BattleManager.Instance.AddConsumableItemUseCount(WeaponDef.No);
			}
		}

		/// <summary>
		/// fire your launcher
		/// </summary>
		public IEnumerator ThrowGrenadeOrUseHandItem(bool fastFire = false, bool useDelay = true, System.Action<bool> onEnd = null)
		{
			DebugEx.Log($"ThrowGrenadeOrUseHandItem.............");
			
			bool isHandItem = WeaponType == GunType.HandItem;

			float t = 0;
			// multiple these values to add compatibility with the default inspector values in older MFPS versions
			float throwForce = BulletSpeed * 22;
			float upwardForce = bulletDropFactor * 33;

			if (useDelay)
			{
				float coolTime = WeaponDef.FireCycle;
				if (bulletsLeft == 1)
				{
					coolTime = Mathf.Max(ReloadTimeEx, WeaponDef.FireCycle);
				}
				nextFireTime = Time.time + coolTime;
				gunManager.SetConsumableCommonNextFireTime();

				if (fastFire) t = WeaponAnimation.PlayFire(bl_WeaponAnimationBase.AnimationFlags.QuickFire);
				else t = WeaponAnimation.PlayFire();

				float d = (fastFire) ? DelayFire + WeaponAnimation.GetAnimationDuration(bl_WeaponAnimationBase.WeaponAnimationType.TakeIn) : DelayFire;
				yield return new WaitForSeconds(d);
			}

			Vector3 direction = PlayerCamera.transform.forward * throwForce + m_Transform.parent.up * upwardForce;
			Vector3 origin = muzzlePoint.position;

			bool subtractBullet = true;

			if (HaveInfinityAmmo)
			{ subtractBullet = false; }

			if (GetGameMode == BattleMode.Practice 
				&& PracticeMode.Instance.CurrentState != PracticeMode.State.TRAINING_MODE)
			{ subtractBullet = false; }

			if(GetGameMode == BattleMode.Tutorial)
			{ subtractBullet = false; }

#if DEBUG
			if (GameCheatValues.BattleCheat.HasFlag(CheatFlags.InfiniteAmmo)) { subtractBullet = false; }
#endif
			if (subtractBullet) { bulletsLeft--; }

			if (!isHandItem)
			{
				FireOneProjectile(direction, origin);
			}
			
			grenadeFired = false;

			// UAV는 전용 로직이 있음
			if (WeaponDef.TacticalType == WeaponTacticalType.UAV)
			{
				BattleManager battleManager = BattleManager.Instance;
				string nickName = battleManager.LocalPlayer.NickName;
				Team team = battleManager.LocalPlayer.GetPlayerTeam();

				UAVActivateData uavData = new UAVActivateData(team, nickName, WeaponCode);
				battleManager.DispatchUAVActivated(uavData);
			}
			
			bl_EventHandler.DispatchLocalPlayerFire(WeaponCode);

			UpdateUI();
			Recoil();

			PlayerNetwork.IsFireGrenade(SpreadAngle, origin, transform.parent.rotation, direction);
			PlayFireAudio();
			isFiring = false;

			//hide the grenade mesh when doesn't have ammo
			if (bulletsLeft <= 0)
			{
				OnAmmoLauncher.ForEach(x =>
			   {
				   if (x != null)
					   x?.SetActive(false);
			   });
			}

			//is Auto reload
			if (!fastFire && AutoReload)
			{
				if (bulletsLeft <= 0 && _remainingClipBullets > 0)
				{
					Reload(0);
				}

				if (useDelay)
				{
					t = t - DelayFire;
					yield return new WaitForSeconds(t);
				}
			}
			DebugEx.Log("[Gun] ThrowGrenade - End");

			// 수류탄의 경우, 투척 후 항상 기존 무기로 돌아간다.
			if (!fastFire)
			{
				gunManager?.OnReturnWeapon();
			}

			onEnd?.Invoke(true);
		}

		public void ResetCoolTime()
		{
			float coolTime = WeaponDef.FireCycle;
			nextFireTime = Time.time + coolTime;
			gunManager.SetConsumableCommonNextFireTime();
		}

		/// <summary>
		/// 
		/// </summary>
		public IEnumerator FastGrenadeFire(System.Action callBack)
		{
			float tt = WeaponAnimation.GetAnimationDuration(bl_WeaponAnimationBase.WeaponAnimationType.AimFire)
				+ WeaponAnimation.GetAnimationDuration(bl_WeaponAnimationBase.WeaponAnimationType.TakeIn);
			
			GrenadeFire(true);
			yield return new WaitForSeconds(tt);
			
			if (_remainingClipBullets > 0)
			{
				bool subtractBullet = true;

				if (HaveInfinityAmmo) { subtractBullet = false; }
#if DEBUG
				if (GameCheatValues.BattleCheat.HasFlag(CheatFlags.InfiniteAmmo)) { subtractBullet = false; }
#endif
				if (subtractBullet) { bulletsLeft++; _remainingClipBullets--; }

				UpdateUI();
			}
			callBack();
			StopAllCoroutines();
			gameObject.SetActive(false);
		}

		/// <summary>
		/// UAV 사용.
		/// </summary>
		public void UseHandItem()
		{
			HandItemFire(success =>
			{
				if (_remainingClipBullets > 0)
				{
					bool subtractBullet = true;
					
					if (HaveInfinityAmmo) { subtractBullet = false; }
#if DEBUG
					if (GameCheatValues.BattleCheat.HasFlag(CheatFlags.InfiniteAmmo)) { subtractBullet = false; }
#endif

					if (subtractBullet) { bulletsLeft++; _remainingClipBullets--; }

					UpdateUI();
				}
			});
		}

		/// <summary>
		/// 
		/// </summary>
		public void QuickMelee(System.Action callBack)
		{
			StartCoroutine(QuickMeleeSequence(callBack));
		}

		/// <summary>
		/// 
		/// </summary>
		IEnumerator QuickMeleeSequence(System.Action callBack)
		{
			float tt = KnifeFire(true);
			yield return new WaitForSeconds(tt);
			callBack();
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Create and fire a bullet
		/// </summary>
		/// <returns></returns>
		void FireOneShot()
		{
			// set the gun's info into an array to send to the bullet
			BuildBulletData();
			var instanceData = GetBulletPosition();
			//bullet info is set up in start function
			instancedBullet = bl_ObjectPoolingBase.Instance.Instantiate(BulletName, instanceData.Position, instanceData.Rotation); // create a bullet
			instancedBullet.GetComponent<bl_ProjectileBase>().InitProjectile(BulletSettings, instanceData.IsCameraPos);// send the gun's info to the bullet
			if (CrosshairCtl.Instance)
			{
				CrosshairCtl.Instance.OnFire();
			}
			PlayerNetwork.ReplicateFire(OriginalWeaponType, instanceData.ProjectedHitPoint, BulletSettings.PackedInaccuracity);
			bl_EventHandler.DispatchLocalPlayerFire(WeaponCode);
			if (bulletsLeft == 0)
			{
				Reload();
			}
		}

		/// <summary>
		/// Create and Fire 1 launcher projectile
		/// </summary>
		/// <returns></returns>
		void FireOneProjectile(Vector3 direction, Vector3 origin)
		{
			BuildBulletData();

			//Instantiate grenade
			GameObject newNoobTube = Instantiate(grenade, origin, transform.parent.rotation) as GameObject;

			var proRigid = newNoobTube.GetComponent<Rigidbody>();
			if (proRigid != null)//if grenade have a rigidbody,then apply velocity
			{
				proRigid.AddForce(direction, ForceMode.Impulse);
			}

			newNoobTube.GetComponent<bl_ProjectileBase>().InitProjectile(BulletSettings);// send the gun's info to the grenade, bl_Projectile.cs 
		}

		/// <summary>
		/// 
		/// </summary>
		public void OnFireCommons()
		{
			PlayFireAudio();

			bool subtractBullet = true;
			if (WeaponType == GunType.Knife)
			{
				subtractBullet = false;
			}
#if DEBUG
			if (GameCheatValues.BattleCheat.HasFlag(CheatFlags.InfiniteAmmo)) { subtractBullet = false; }
#endif
			if (subtractBullet)
			{
				bulletsLeft--;
			}

			UpdateUI();
			nextFireTime += WeaponDef.FireCycle;
			EjectShell();

			// 반동
			Recoil();


			if (BulletSettings.IsLocalPlayer && bulletsLeft == 0 && BulletsLeftTotal == 0)
			{
				bl_EventHandler.DispatchOutOfAmmoEvent();
			}
			// Shake사용하지 않음.
			//Shake();
		}

		/// <summary>
		/// Play muzzleflash effect.
		/// </summary>
		public void PlayFX()
		{
			if (!isAiming)
			{
				if (weaponFX == null)
				{
					if (muzzleFlash) { muzzleFlash.Play(); }
				}
				else weaponFX.PlayFireFX();
			}
		}

		/// <summary>
		/// Get the bullet spawn position and rotation
		/// </summary>
		public BulletInstanceData GetBulletPosition()
		{
			bulletInstanceData.IsCameraPos = false;
			bulletInstanceData.Rotation = m_Transform.parent.rotation;
			Vector3 cp = PlayerCamera.transform.position;
			Vector3 mp = muzzlePoint.position;
			
			bulletInstanceData.ProjectedHitPoint = cp + (PlayerCamera.transform.forward * 100);

			// if there's a collider between the player camera and the weapon fire point
			// that means the fire point is going through the collider
			if (Physics.Linecast(cp, mp, GameSettings.TagsAndLayerSettings.LocalPlayerHitableLayers))
			{
				// since we can't shoot from the fire point (since the bullets will go through the collider)
				// the bullet will be instanced from the center of the player camera
				bulletInstanceData.Position = cp;
				bulletInstanceData.IsCameraPos = true;
			}
			else
			{
				// we can shoot from the fire point
				bulletInstanceData.Position = mp;
			}

			// Now there is a situation where the bullet wont get to hit the desired destination 
			// this happens when the bullet trajectory is hampered by a collider that is near to the bullet instantiation point.
			// in these case the player may not be aiming to that collider but the bullet will hit that collider instead of where the player is aiming to
			// so to fix this we do the following:

			// detect colliders between the fire point and the direction (5 meters long) where the player is aiming to
			if (Physics.Raycast(bulletInstanceData.Position, PlayerCamera.transform.forward, out hit, 3, GameSettings.TagsAndLayerSettings.LocalPlayerHitableLayers, QueryTriggerInteraction.Ignore))
			{
				// if indeed there is a collider hampering the trajectory
				// instance the bullet from the camera to make the bullet avoid the obstacle and hit the desired destination.

				bulletInstanceData.ProjectedHitPoint = hit.point;
				bulletInstanceData.Position = cp;
				bulletInstanceData.IsCameraPos = true;
			}
			else // All clear, not obstacles detected
			{
				if (isAiming)
				{
					if (WeaponType != GunType.Sniper)
					{
						// this result in a more realist bullet shoot effect when aiming but add a sightly inaccuracy on close targets
						bulletInstanceData.Rotation = Quaternion.LookRotation(bulletInstanceData.ProjectedHitPoint - mp);
						if (Physics.Raycast(cp, PlayerCamera.transform.forward, out hit, 500, GameSettings.TagsAndLayerSettings.LocalPlayerHitableLayers, QueryTriggerInteraction.Ignore))
						{
							bulletInstanceData.ProjectedHitPoint = hit.point;
							bulletInstanceData.Rotation = Quaternion.LookRotation(bulletInstanceData.ProjectedHitPoint - mp);

#if MFPSTPV
						if (bl_CameraViewSettings.IsThirdPerson())
						{
							// In third person view, we have to make sure the projected hit point is not behind the weapon fire point
							if (bl_MathUtility.Distance(hit.point, cp) <= bl_MathUtility.Distance(mp, cp))
							{
								// if the projected hit point is behind the fire point, then the bullet raycast has to be fired from the 
								// actual fire point
								bulletInstanceData.ProjectedHitPoint = cp + (PlayerCamera.transform.forward * 100);
								bulletInstanceData.Rotation = m_Transform.parent.rotation;
							}
						}
#endif
						}

						// this in the other hand make the bullet travel exactly where the player is aiming but the bullets are instanced from the center of the screen
						// which if you are using some sort of custom effects in the bullets will be noticed (like shooting bullets from the eyes)
						// but if this is what you need and you are not using custom bullet trails/effects, comment the above lines and uncomment the following one:

						// bulletInstanceData.Position = cp;
					}
					else
					{
						bulletInstanceData.Position = cp;
						bulletInstanceData.IsCameraPos = true;
					}
				}
				else
				{
#if MFPSTPV
				// in third person we have to always get the direction from the camera
				if (bl_CameraViewSettings.IsThirdPerson())
				{
					if (Physics.Raycast(cp, PlayerCamera.transform.forward, out hit, 500, bl_GameData.TagsAndLayerSettings.LocalPlayerHitableLayers, QueryTriggerInteraction.Ignore))
					{

						// In third person view, we have to make sure the projected hit point is not behind the weapon fire point
						if (bl_MathUtility.Distance(hit.point, cp) > bl_MathUtility.Distance(mp, cp))
						{
							bulletInstanceData.ProjectedHitPoint = hit.point;
							bulletInstanceData.Rotation = Quaternion.LookRotation(bulletInstanceData.ProjectedHitPoint - mp);
						}
						else
						{
							// if the projected hit point is behind the fire point, then the bullet raycast has to be fired from the 
							// actual fire point
						}
					}
				}
#endif
				}
			}

			return bulletInstanceData;
		}
		#endregion

		/// <summary>
		/// Fetch the information that will be attached to the next fired bullet
		/// containing all required information from the weapon from which the bullet was shoot
		/// and the information about the player who shoot it
		/// </summary>
		public void BuildBulletData()
		{
			BulletSettings.Damage = DamageEx;
			BulletSettings.ImpactForce = impactForce;

			BulletSettings.SetInaccuracity(SpreadAngle, bulletSpeed);
			BulletSettings.Speed = BulletSpeed;
			BulletSettings.WeaponName = WeaponDef.Name;
			BulletSettings.Position = PlayerReferences.transform.localPosition;
			BulletSettings.WeaponCode = WeaponCode;
			BulletSettings.isNetwork = false;
			BulletSettings.DropFactor = bulletDropFactor;
			BulletSettings.Range = Range;
			BulletSettings.ActorViewID = BattleManager.LocalPlayerViewID;
			BulletSettings.MFPSActor = BattleManager.Instance.LocalPlayerSeat;
			BulletSettings.IsLocalPlayer = true;
		}

		/// <summary>
		/// Aiming control
		/// </summary>
		void Aim(bool instantly = false)
		{
			if (isAiming && !isReloading)
			{
				CurrentPos = AimPosition; //Place in the center ADS
				currentRotation = Quaternion.Euler(aimRotation);
				currentZoom = Zoom; //create a zoom camera
				PlayerReferences.weaponSway.UseAimSettings();
			}
			else // if not aiming
			{
				CurrentPos = DefaultPos; //return to default gun position
				currentRotation = defaultRotation;
				currentZoom = PlayerReferences.DefaultCameraFOV; //return to default fog
				PlayerReferences.weaponSway.ResetSettings();
			}

			// 즉시 상태값들 변경
			if (instantly)
			{
				m_Transform.localPosition = CurrentPos;
				m_Transform.localRotation = currentRotation;
				if (PlayerCamera != null && !BlockAimFoV)
				{
					PlayerCamera.fieldOfView = currentZoom;
				}
			}
			else // 부드럽게 변경
			{
				float delta = Time.deltaTime;
				//apply position
				m_Transform.localPosition = useSmooth ? Vector3.Lerp(m_Transform.localPosition, CurrentPos, delta * AimSpeed) : //with smooth effect
				Vector3.MoveTowards(m_Transform.localPosition, CurrentPos, delta * AimSpeed); // with snap effect
				m_Transform.localRotation = Quaternion.Slerp(m_Transform.localRotation, currentRotation, delta * AimSpeed);

				if (PlayerCamera != null && !BlockAimFoV)
				{
					PlayerCamera.fieldOfView = Mathf.Lerp(PlayerCamera.fieldOfView, currentZoom + controller.GetSprintFov(), delta * AimSpeed);
				}
			}
			if (lastAimState != isAiming)
			{
				bl_EventHandler.DispatchLocalAimEvent(isAiming, currentZoom);
				lastAimState = isAiming;
			}
		}

		/// <summary>
		/// 총기 반동
		/// </summary>
		public void Recoil()
		{
			var recoilData = new bl_RecoilBase.RecoilData() { Amount = WeaponRecoil, Speed = RecoilSpeed };
			PlayerReferences.recoil.SetRecoil(recoilData);
		}

		/// <summary>
		/// 
		/// </summary>
		void Shake()
		{
			float influence = isAiming ? 0.5f : 1;
			bl_EventHandler.DoPlayerCameraShake(shakerPresent, "fpweapon", influence);
		}

		/// <summary>
		/// 
		/// </summary>
		void EjectShell()
		{
			if (shell != null)
				shell?.Play();
		}

		/// <summary>
		/// 
		/// </summary>
		public void CheckBullets(float delay = 0)
		{
			if (bulletsLeft <= 0 && _remainingClipBullets > 0 && AutoReload)
			{
				Reload(delay);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		void OnReload()
		{
			if (!CanReload) return;

			Reload();
		}

		/// <summary>
		/// 
		/// </summary>
		public void Reload(float delay = 0.2f)
		{
			DebugEx.Log("[Gun] Reload");

			if (isReloading || !gameObject.activeInHierarchy)
				return;

			StopCoroutine(nameof(DoReload));
			StartCoroutine(nameof(DoReload), delay);
		}

		/// <summary>
		/// start reload weapon
		/// deduct the remaining bullets in the cartridge of a new clip
		/// as this happens, we disable the options: fire, aim and run
		/// </summary>
		/// <returns></returns>
		IEnumerator DoReload(float waitTime = 0.2f)
		{
			isAiming = CanFire = false;

			if (isReloading)
				yield break; // if already reloading... exit and wait till reload is finished

			if (waitTime > 0)
			{
				// Delay before start reloading
				yield return new WaitForSeconds(waitTime);
			}

			if (_remainingClipBullets > 0 || inReloadMode)//if have at least one cartridge
			{
				isReloading = true; // we are now reloading

				if (WeaponAnimation != null)
				{
					if (reloadPer == ReloadPer.Bullet)//insert one bullet at a time
					{
						int t_repeat = BulletsPerMagazine - bulletsLeft; //get the number of spent bullets
						int add = (_remainingClipBullets >= t_repeat) ? t_repeat : (int)_remainingClipBullets;
						int maxLoop = WeaponType == GunType.Shotgun ? pelletsPerShot : BulletsPerMagazine;
						WeaponAnimation?.PlayReload(ReloadTimeEx, new int[2] { add, maxLoop }, bl_WeaponAnimationBase.AnimationFlags.SplitReload);

						// Since this reload method const of multiple animation parts, the reload time
						// will be determined by the animations, the reload will end once the last animation has been played.
						yield break;
					}
					else
					{
						WeaponAnimation?.PlayReload(ReloadTimeEx, null);
					}
				}

				if (!SoundReloadByAnim)
				{
					StartCoroutine(ReloadSoundIE());
				}

				if (WeaponType == GunType.Grenade) { OnAmmoLauncher.ForEach(x => { x?.SetActive(true); }); }

				if (WeaponType != GunType.Grenade)
				{
					yield return new WaitForSeconds(ReloadTimeEx); // wait for reload time
				}
				FillMagazine();
			}
			UpdateUI();
			isReloading = inReloadMode = false; // done reloading
			CanAiming = CanFire = true;

			DebugEx.Log("[Gun] Reload - Done");
		}

		/// <summary>
		/// 
		/// </summary>
		public void FillMagazine()
		{
			int need = BulletsPerMagazine - bulletsLeft;
			int add = (_remainingClipBullets >= need) ? need : (int)_remainingClipBullets;
			bulletsLeft += add;
			if (!HaveInfinityAmmo) _remainingClipBullets -= add;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="bullet"></param>
		public void LoadBullet(int bullet)
		{
			_remainingClipBullets -= bullet;
			bulletsLeft += bullet;
			UpdateUI();
		}

		public void AddClip(int count)
		{
			if(count == 0) { return; }

			_remainingClipBullets += BulletsPerMagazine * count;
			UpdateUI();
		}

		/// <summary>
		/// Set unlimited ammo to this weapon
		/// </summary>
		/// <param name="infinity"></param>
		public void SetInifinityAmmo(bool infinity)
		{
			HaveInfinityAmmo = infinity;
			UpdateUI();
		}

		public void ResetAmmo()
		{
			RemainingClipBullets = BulletsPerMagazine * (numberOfClips - 1);
			bulletsLeft = BulletsPerMagazine;
			
			UpdateUI();
		}

		/// 탄알수를 강제 변경한다.
		/// <param name="newBulletNumber">전체 탄알 수</param>
		public void UpdateBulletLeft(int newBulletNumber)
		{
			bulletsLeft = newBulletNumber <= BulletsPerMagazine ? 
				newBulletNumber : 
				BulletsPerMagazine;
			
			RemainingClipBullets = 	newBulletNumber - bulletsLeft;
			
			UpdateUI();
		}

		public void UpdateClip(int targetClip)
		{
			RemainingClipBullets = BulletsPerMagazine * targetClip;
			bulletsLeft = BulletsPerMagazine;
			
			UpdateUI();
		}

		/// <summary>
		/// Sync Weapon state for Upper animations
		/// </summary>
		void DetermineUpperState()
		{
			if (PlayerNetwork == null)
				return;

			if (isFiring && !isReloading)
			{
				FPState = (isAiming) ? PlayerFPState.FireAiming : PlayerFPState.Firing;
			}
			else if (isAiming && !isFiring && !isReloading)
			{
				FPState = PlayerFPState.Aiming;
			}
			else if (isReloading)
			{
				FPState = PlayerFPState.Reloading;
			}
			else if (controller.State == PlayerState.Running && !isReloading && !isFiring && !isAiming)
			{
				FPState = PlayerFPState.Running;
			}
			else
			{
				FPState = PlayerFPState.Idle;
			}
			PlayerNetwork.FPState = FPState;
		}

		/// <summary>
		/// Snap the weapon to the aim position.
		/// </summary>
		public void SetToAim()
		{
			m_Transform.localPosition = AimPosition;
			m_Transform.localRotation = Quaternion.Euler(aimRotation);
		}

		#region Audio
		/// <summary>
		/// 
		/// </summary>
		public void PlayFireAudio()
		{
			FireSource.clip = FireSound;
			FireSource.spread = Random.Range(1.0f, 1.5f);
			FireSource.pitch = Random.Range(1.0f, 1.075f);
			FireSource.Play();
		}

		/// <summary>
		/// most shotguns have the sound of shooting and then reloading
		/// </summary>
		/// <returns></returns>
		IEnumerator DelayFireSound()
		{
			PlayFireAudio();
			yield return new WaitForSeconds(delayForSecondFireSound);
			if (DelaySource != null)
			{
				DelaySource.clip = ReloadSound3;
				DelaySource.Play();
			}
			else
			{
				PlayClip(ReloadSound3);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void FinishReload()
		{
			DebugEx.Log("[Gun] FinishReload");
			isReloading = false; // done reloading
			CanAiming = true;
			CanFire = true;
			inReloadMode = false;
		}

		/// <summary>
		/// 
		/// </summary>
		private void PlayClip(AudioClip clip, float pitch = 1)
		{
			Source.clip = clip;
			Source.pitch = pitch;
			Source.Play();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="part"></param>
		public void PlayReloadAudio(int part)
		{
			if (SoundReloadByAnim) return;

			if (part == 0) PlayClip(ReloadSound);
			else if (part == 1) PlayClip(ReloadSound2);
			else if (part == 2) PlayClip(ReloadSound3);
		}

		/// <summary>
		/// 
		/// </summary>
		public void PlayEmptyFireSound()
		{
			if (WeaponType != GunType.Knife && DryFireSound != null)
			{
				PlayClip(DryFireSound);
			}
		}

		/// <summary>
		/// use this method to various sounds reload.
		/// if you have only 1 sound, them put only one in inspector
		/// and leave empty other box
		/// </summary>
		/// <returns></returns>
		IEnumerator ReloadSoundIE()
		{
			float t_time = ReloadTimeEx / 3;
			if (ReloadSound != null)
			{
				PlayClip(ReloadSound);
			}
			if (ReloadSound2 != null)
			{
				if (WeaponType == GunType.Shotgun)
				{
					int t_repeat = BulletsPerMagazine - bulletsLeft;
					for (int i = 0; i < t_repeat; i++)
					{
						yield return new WaitForSeconds(t_time / t_repeat + 0.025f);
						PlayClip(ReloadSound2);
					}
				}
				else
				{
					yield return new WaitForSeconds(t_time);
					PlayClip(ReloadSound2);
				}
			}
			if (ReloadSound3 != null)
			{
				yield return new WaitForSeconds(t_time);
				PlayClip(ReloadSound3);
			}
			yield return new WaitForSeconds(0.65f);
		}
		#endregion

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		IEnumerator KnifeSendFire()
		{
			yield return new WaitForSeconds(0.5f);
			isFiring = false;
			alreadyKnife = false;
		}

		/// <summary>
		/// When we disable the gun ship called the animation
		/// and disable the basic functions
		/// </summary>
		public float DisableWeapon(bool isFastKill = false)
		{
			CanAiming = false;
			if (isReloading) { inReloadMode = true; isReloading = false; }
			CanFire = false;
			if (PlayerCamera == null) { PlayerCamera = PlayerReferences.playerCamera; }
			if (!isFastKill) { StopAllCoroutines(); }

			if (WeaponAnimation != null)
				return WeaponAnimation.PlayTakeOut();
			else return 0;
		}

		/// <summary>
		/// 
		/// </summary>
		void DrawComplete()
		{
			if (GetGameMode == BattleMode.Tutorial) { CanFire = TutorialManager.Instance.CanFire(); }
			else { CanFire = !BattleManager.Instance.RoundFinish; }
			
			CanAiming = true;
			if (inReloadMode)
			{
				if (WeaponType == GunType.Grenade)
				{
					FillMagazine();
					inReloadMode = false;
					UpdateUI();
				}
				else
				{
					Reload(0.2f);
				}
			}
		}

		/// <summary>
		/// When round is end we can't fire
		/// </summary>
		void OnRoundEnd()
		{
			DebugEx.Log("Gun.OnRoundEnd()");
			m_enable = false;
		}

		/// <summary>
		/// 
		/// </summary>
		public bool OnPickUpAmmo(int bullets, int projectiles, int weaponNo)
		{
			//if this is not a global ammo but for a specific weapon
			if (weaponNo != Invalid.No)
			{
				//and this is not the weapon that is for
				if (weaponNo != WeaponCode.WeaponNo) return false;
			}

			if (WeaponType == GunType.Knife) return false;

			if (WeaponType == GunType.Grenade)
			{
				_remainingClipBullets += projectiles;
				new MFPSLocalNotification(string.Format("+{0} {1}", projectiles.ToString(), WeaponDef.Name));
			}
			else
			{
				if (_remainingClipBullets >= BulletsPerMagazine * maxNumberOfClips) return false;

				int oldCount = _remainingClipBullets;
				_remainingClipBullets += bullets;
				_remainingClipBullets = Mathf.Clamp(_remainingClipBullets, 0, BulletsPerMagazine * maxNumberOfClips);
				new MFPSLocalNotification(string.Format("+{0} {1} Bullets", _remainingClipBullets - oldCount, WeaponDef.Name));
			}

			UpdateUI();
			return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="newPoint"></param>
		public void OverrideMuzzlePoint(Transform newPoint)
		{
			defaultMuzzlePoint = muzzlePoint;
			muzzlePoint = newPoint;
		}

		/// <summary>
		/// Enable/Disable the weapon renders/meshes
		/// </summary>
		public void SetWeaponRendersActive(bool active)
		{
			foreach (var renderer in _allRenderers)
			{
				renderer.SetActiveGo(active);
			}

#if CUSTOMIZER
		var customizerWeapon = GetComponent<bl_CustomizerWeapon>();
		if (active && customizerWeapon != null && customizerWeapon.ApplyOnStart)
		{
			customizerWeapon.LoadAttachments();
			customizerWeapon.ApplyAttachments();
		}
#endif
			onWeaponRendersActive?.Invoke(active);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public float GetReloadTime()
		{
			if (reloadPer == ReloadPer.Bullet && WeaponAnimation != null)
			{
				int missingBullets = BulletsPerMagazine - bulletsLeft; //get the number of spent bullets
				int add = (_remainingClipBullets >= missingBullets) ? missingBullets : (int)_remainingClipBullets;
				float[] bullets = { add };
				return WeaponAnimation.GetAnimationDuration(bl_WeaponAnimationBase.WeaponAnimationType.Reload, bullets);
			}
			return ReloadTimeEx;
		}

		void CancelFiring() { isFiring = false; }
		void CancelReloading() { WeaponAnimation?.CancelAnimation(bl_WeaponAnimationBase.WeaponAnimationType.Reload); }
		public void ResetDefaultMuzzlePoint() { if (defaultMuzzlePoint != null) muzzlePoint = defaultMuzzlePoint; }
		public void UpdateUI() => WeaponSwitcherSlotManager.Instance.UpdateAmmoCount(this);
		public void SetDefaultWeaponCameraFOV(float fov) => WeaponCamera.fieldOfView = fov;
		public Vector3 GetDefaultPosition() => DefaultPos;

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override FireType GetFireType()
		{
			switch (WeaponType)
			{
				case GunType.Machinegun: return FireType.Auto;
				case GunType.Burst: return FireType.Semi;
				case GunType.Knife: return FireType.Undefined;
				default: return FireType.Single;
			}
		}

		#region Getters
		/// <summary>
		/// The current <see cref="GunTypeE"/>
		/// This can change if the weapon fire type changes in runtime.
		/// </summary>
		public GunType WeaponType
		{
			get
			{
				if (currentGunType == GunType.None) currentGunType = WeaponDef.GunType;
				return currentGunType;
			}
			set
			{
				currentGunType = value;
			}
		}

		/// <summary>
		/// The default <see cref="GunTypeE"/> defined in GameData
		/// </summary>
		public GunType OriginalWeaponType
		{
			get => WeaponDef.GunType;
		}

		float GetSpreadAngle
		{
			get
			{
				float finalSpreadAngle = isAiming ? DefaultSpreadAngleForZoom : DefaultSpreadAngle;

				if (PlayerReferences.firstPersonController.State == PlayerState.Crouching)
				{
					finalSpreadAngle *= this.SpreadSitMultiply;
				}

				MultiplyVelocitySpreadOffset(ref finalSpreadAngle);

				return finalSpreadAngle;
			}
		}

		private void MultiplyVelocitySpreadOffset(ref float inputAngle)
		{
			var playerState = PlayerReferences.firstPersonController.State;

			if (playerState.IsOneOf(PlayerState.Running, PlayerState.Jumping, PlayerState.Dropping))
			{
				inputAngle *= SpreadRunMultiply;
			}
			else if (playerState == PlayerState.Walking)
			{
				inputAngle *= SpreadWalkMultiply;
			}
		}

		public int GetCompactClips { get { return (_remainingClipBullets / BulletsPerMagazine); } }

		/// <summary>
		/// Determine if the player can shoot this weapon.
		/// </summary>
		public bool CanFire
		{
			get
			{
				if (GetGameMode.IsOneOf(BattleMode.Practice))
				{
					if(PracticeMode.Instance.IsCountdown) return false;
				}
				
				return (bulletsLeft > 0 && m_canFire && !isReloading && FireWhileRun);
			}
			set => m_canFire = value;
		}

		public bool FireRatePassed => IsConsumable
			? Time.time > nextFireTime && Time.time > gunManager.ConsumableCommonNextFireTime
			: Time.time > nextFireTime;

		public bool AllowQuickFire() => m_AllowQuickFire && bulletsLeft > 0 && FireRatePassed;

		/// <summary>
		///  Determine if the player can aiming with this weapon in the current state
		/// </summary>
		public bool CanAiming
		{
			get
			{
				if (WeaponType == GunType.Grenade || WeaponType == GunType.Knife) return false;
				if (!HasScope) { return false; }

				if (controller.GetRunToAimBehave() == PlayerRunToAimBehave.BlockAim && controller.State == PlayerState.Running) return false;

				return (m_canAim);
			}
			set
			{
				if (HasScope) { m_canAim = value; }
			}
		}

		/// <summary>
		/// Determine if the player can reload this weapon
		/// </summary>
		bool CanReload
		{
			get
			{
				bool can = false;

				// Run상태가 Reload를 막지 않게 함.
				const bool RESTRICED_BY_RUN = false;

				if (bulletsLeft < BulletsPerMagazine && _remainingClipBullets > 0
					&& (!RESTRICED_BY_RUN || controller.State != PlayerState.Running)
					&& !isReloading)
				{
					can = true;
				}
				if (WeaponType == GunType.Knife && nextFireTime < Time.time)
				{
					can = false;
				}
				return can;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		bool FireWhileRun
		{
			get
			{
				if (GameSettings.Instance.CanFireWhileRunning)
				{
					return true;
				}
				if (controller.State != PlayerState.Running)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
		}

		public bool CanBeTaken()
		{
			if (WeaponType != GunType.Grenade && canBeTakenWhenIsEmpty)
			{
				return true;
			}

			return (bulletsLeft > 0 || _remainingClipBullets > 0);
		}

		public bool IsOutOfAmmo() => bulletsLeft <= 0 && _remainingClipBullets <= 0;

		public bl_FirstPersonControllerBase controller => PlayerReferences.firstPersonController;
		public bl_PlayerNetwork PlayerNetwork => PlayerReferences.playerNetwork;
		private GunManager gunManager => PlayerReferences.gunManager;

		private bl_PlayerReferences _playerReferences;
		public bl_PlayerReferences PlayerReferences
		{
			get
			{
				if (_playerReferences == null) _playerReferences = transform.root.GetComponent<bl_PlayerReferences>();
				return _playerReferences;
			}
		}

		private bl_WeaponAnimationBase _anim;
		public bl_WeaponAnimationBase WeaponAnimation
		{
			get
			{
				if (_anim == null) { _anim = GetComponentInChildren<bl_WeaponAnimationBase>(); }
				return _anim;
			}
		}
		#endregion

		[System.Serializable]
		public enum BulletInstanceMethod
		{
			Pooled,
			Instanced,
		}

		[System.Serializable]
		public enum ReloadPer
		{
			Bullet,
			Magazine,
		}

		public struct BulletInstanceData
		{
			public Vector3 Position;
			// 애로사항이 있어 총구 포인트 대신 카메라 포지션을 사용하는가
			// (이때는 시각적 결함 때문에 Trail이펙트를 금지해야 한다)
			public bool IsCameraPos;
			public Quaternion Rotation;
			public Vector3 ProjectedHitPoint;
		}

#if UNITY_EDITOR
		public bool _aimRecord = false;
		public Vector3 _defaultPosition = new Vector3(-100, 0, 0);
		public Vector3 _defaultRotation = new Vector3(-100, 0, 0);
		private void OnDrawGizmos()
		{
			if (muzzlePoint != null)
			{
				Gizmos.color = new Color(0, 1, 0, 0.4f);
				Gizmos.DrawSphere(muzzlePoint.position, 0.022f);
				Gizmos.color = Color.white;
			}
		}
#endif

		static bool IsCursorLocked()
		{
			return RoomMenu.Instance.isCursorLocked;
		}

		void ConnectToMobileControl()
		{
			if (bl_UtilityHelper.UseMobileControl)
			{
				MobileControlsUi.OnFireClick += OnFire;
				MobileControlsUi.OnReload += OnReload;
			}
		}

		void DisconnectFromMobileControl()
		{
			MobileControlsUi.OnFireClick -= OnFire;
			MobileControlsUi.OnReload -= OnReload;
		}

		/// <summary>
		/// MFPS씬을 독립 실행할 때 어떻게 해서든 유효한 WeaponNo를 얻게하기 위한 메서드.
		/// </summary>
		protected override WeaponCode GetDummyWeaponCode()
		{
			int defaultWeaponNo = WeaponCDB.Instance.GetAllDefs().First(def => def.GunType == GunTypeE).No;
			return new WeaponCode(defaultWeaponNo);
		}

		void SniperReloadBullet()
		{
			if (isReloading || !gameObject.activeInHierarchy)
			{ return; }

			StopCoroutine(nameof(DoSniperReloadBullet));
			StartCoroutine(DoSniperReloadBullet());
		}

		IEnumerator DoSniperReloadBullet()
		{
			isReloading = true;

			CanFire = false;

			yield return new WaitForSeconds(0.3f);

			isAiming = false;

			//if (durationTime > 0)
			{
				yield return new WaitForSeconds(0.7f);
			}
			isReloading = false;

			isAiming = CanFire = true;
		}		

#if UNITY_EDITOR
		void OnTestMobileInputOnPCChanged()
		{
			DisconnectFromMobileControl();
			ConnectToMobileControl();
		}
#endif
	}
}