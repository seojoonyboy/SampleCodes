//#define BULLET_NEVER_POOL
using Framework;
using Game.Data;
using UnityEngine;

namespace Game.View.BattleSystem
{
	/// <summary>
	/// 총탄의 이동 및 트레일 효과. 기존 bl_Bullet.cs 대비 구현 변경:
	///  - TrailRenderer 대신 LineRenderer를 사용한다.
	/// - 트레일 효과를 위해 전용 Shader (BulletTrail.shader)를 사용한다.
	/// </summary>
	public class Bullet : bl_ProjectileBase
	{
		[LovattoToogle]
		[SerializeField] bool _checkDamageables;
		[SerializeField] LineRenderer _line;

		public int ActorViewID { get; set; }

		RaycastHit _hit;
		Transform m_Transform;
		float _totalTraveledDistance = 0;
		BulletData _bulletData;
		Vector3 _startPos = Vector3.zero;
		Vector3 _newPos = Vector3.zero;   // bullet's new position
		// 마지막 Raycast 호출한 포지션.
		Vector3 _oldPos = Vector3.zero;   // bullet's previous location
		bool _hasTravelDone = false;             // has the bullet hit something?
		Vector3 _velocity;            // direction bullet is travelling
		float _bulletRange;
		LayerMask _bulletMask;
		int localLayer = -2;
		float _decayElapsedTime;
		float _elapsedTime;
		GameViewSettings _viewSetting;
		float _shaderRandomA = 0;
		float _shaderRandomB = 0;
		bool _isPistol;
		const float TrailMinLength = 3; // BulletTrail 머터리얼의 'Cap'의 두배 값 이상이어야 한다.
		/// <summary>
		/// <see cref="bl_Gun"/>으로부터 생성 후 초기화 호출.
		/// </summary>
		public override void InitProjectile(BulletData data, bool isStartFromCameraPos = false)
		{
			_viewSetting = GameViewSettings.Instance;

			if (data.MFPSActor != null)
			{
				float detectDist = GameSettings.Instance.FiringDetectionDistance;
				PlayerDetectionManager.Instance.TryExposePlayer(data.MFPSActor, DetectionType.Fire, 0, detectDist);
			}

			// '총구'가 아닌 카메라 포지션으로부터의 출발이면 Trail 이펙트를 사용하지 않는다.
			//_line.enabled = !isStartFromCameraPos;

			// Since the bullet is pooled, make sure to reset all the variables to its initial values.
			ResetBullet();

			m_Transform = transform;
			_bulletData = data;
			_isPistol = (data.WeaponCode != 0 && WeaponCDB.Instance.GetDef(data.WeaponCode.WeaponNo).GunType == GunType.Pistol);

			ActorViewID = data.ActorViewID;

			_shaderRandomA = Random.value;
			_shaderRandomB = Random.value;

			// 총구 방향 속도 벡터
			_velocity = data.Speed * m_Transform.forward;
			// 탄퍼짐 적용
			_velocity = m_Transform.TransformDirection(BulletData.ToInaccuracityVector(data.PackedInaccuracity));

			_bulletRange = Mathf.Min(data.Range, _viewSetting.BulletRangeLimit);

			_startPos = m_Transform.position;
			_newPos = _startPos;
			_oldPos = _newPos;

			_bulletMask = GameSettings.TagsAndLayerSettings.BulletHittableLayers;

			if (data.IsLocalPlayer)
			{
				if (localLayer == -2) localLayer = GameSettings.TagsAndLayerSettings.GetLocalPlayerLayerIndex();
				_bulletMask = GameSettings.TagsAndLayerSettings.BulletHittableLayers & ~(1 << localLayer);
			}

			if (_line != null)
			{
				_line.positionCount = 2;
				_line.SetPosition(0, _startPos);
				_line.SetPosition(1, _newPos);
				
				if (!GameSettings.Instance.BulletTracer) { Destroy(_line); }
			}
		}

		void ResetBullet()
		{
			_bulletData = null;
			_hasTravelDone = false;
			ActorViewID = 0;
			_totalTraveledDistance = 0;
			_decayElapsedTime = 0;
			_elapsedTime = 0;
		}
		
		public override void OnUpdate()
		{
			if(_line == null) { Disable(); return; }

			if (!_hasTravelDone)
			{
				Travel();
				_line.SetPosition(1, _newPos);
			}

			if (_totalTraveledDistance > TrailMinLength)
			{
				var material = _line.material;

				// 시작 위치 및 총길이
				Vector4 param = _startPos;
				param.w = _totalTraveledDistance;
				material.SetVector("_OrigPos", param);

				Color color = Color.white;

				float evalTime = _elapsedTime * (_isPistol ? _viewSetting.PistolBulletTrailDecayTimeMul : 1);

				// 현재 시점의 투명도 및 왜곡량
				color.a = _viewSetting.BulletTrailAlphaCurve.Evaluate(evalTime);
				float distortion = _viewSetting.BulletTrailDistortionCurve.Evaluate(evalTime);

				// 내 총탄인 경우 카메라 방향에서 보게 되어 너무 강하게 보이니 알파를 좀 줄인다.
				if (ActorViewID == BattleManager.LocalPlayerViewID)
				{
					color.a *= _viewSetting.MyBulletTrailAlpha;
				}
				
				_line.material.SetVector("_TintColor", color);
				_line.material.SetVector("_Distortion", new Vector4(distortion, _decayElapsedTime, _shaderRandomA, _shaderRandomB));
			}

			if (_hasTravelDone)
			{
				float decayTime = _elapsedTime / _viewSetting.BulletTrailDecayTime;
				if (decayTime > 1)
				{
					Disable();
					return;
				}
				_decayElapsedTime += Time.deltaTime;
			}
			
			_elapsedTime += Time.deltaTime;

#if BULLET_NEVER_POOL
			if (!isActiveAndEnabled)
			{
				Destroy(gameObject);
			}
#endif
		}

		/// <summary>
		/// 총탄의 이동. 
		/// </summary>
		void Travel()
		{
			float timeStep = Time.deltaTime;

			_newPos += _velocity * timeStep;

			float advanceLength = (_newPos - _oldPos).magnitude;
			_totalTraveledDistance += advanceLength;

			// (이후 작업) 이 조건절은 카메라 시야에 들어오지 않으면 프레임 스킵 가능하게 개선 필요.
			if (true)
			{
				if (Physics.Raycast(_oldPos, _velocity, out _hit, advanceLength, _bulletMask, QueryTriggerInteraction.Ignore))
				{
					_newPos = _hit.point;

					_totalTraveledDistance = (_newPos - _startPos).magnitude;

					bool isLocalPlayerHit;
					if(OnHit(_hit, out isLocalPlayerHit))
					{
						if (isLocalPlayerHit)
						{
							_bulletMask = GameSettings.TagsAndLayerSettings.EnvironmentOnly;
						}
						else
						{
							_hasTravelDone = true;
						}
						
					}
				}
				_oldPos = _newPos;

				if (_totalTraveledDistance > _bulletRange)
				{
					_hasTravelDone = true;
				}
			}

			m_Transform.position = _newPos;
		}

		void Disable()
		{
			ResetBullet();
			gameObject.SetActive(false);
		}

		bool OnHit(RaycastHit hit, out bool isLocalPlayerHit)
		{
			isLocalPlayerHit = false;
			int debugStep = 0;
			
			try
			{

				Ray mRay = new Ray(m_Transform.position, m_Transform.forward);

				debugStep = 1;

				if (!_bulletData.isNetwork)
				{
					if (hit.rigidbody != null && !hit.rigidbody.isKinematic) // if we hit a rigi body... apply a force
					{
						float mAdjust = 1.0f / (Time.timeScale * (0.02f / Time.fixedDeltaTime));
						hit.rigidbody.AddForceAtPosition(((mRay.direction * _bulletData.ImpactForce) / Time.timeScale) / mAdjust, hit.point);
					}
				}

				debugStep = 2;

				switch (hit.transform.tag) // decide what the bullet collided with and what to do with it
				{
					case "IgnoreBullet":
						return false;
					case "Projectile":
						// do nothing if 2 bullets collide
						break;
					case bl_MFPS.HITBOX_TAG://Send Damage for other players
						SendPlayerDamage(hit, ref debugStep);
						break;
					case bl_MFPS.AI_TAG:
						//Bullet hit a bot collider, check if we have to apply damage.
						SendBotDamage(hit);
						break;
					case bl_MFPS.LOCAL_PLAYER_TAG:
						// 봇이 유저를 맞힘
						SendPlayerDamageFromBot(hit, out isLocalPlayerHit);
						break;
					case "Wood":
						InstanceHitParticle("decalw", hit);
						break;
					case "Concrete":
						InstanceHitParticle("decalc", hit);
						break;
					case "Metal":
						InstanceHitParticle("decalm", hit, true);
						break;
					case "Dirt":
						InstanceHitParticle("decals", hit);
						break;
					case "Water":
						InstanceHitParticle("decalwt", hit);
						break;
					default:
						InstanceHitParticle("decal", hit);
						break;
				}

				debugStep = 3;

				return true;
			}
			catch (System.Exception e)
			{
				CrashlyticsUtil.LogException(new System.Exception($"Bullet.OnHit() debugStep:{debugStep} tag:{hit.transform.tag} msg:{e.Message}"));
				return true;
			}
		}

		/// <summary>
		/// Apply damage to real players from a bot or another real player
		/// </summary>
		void SendPlayerDamage(RaycastHit hit, ref int debugStep)
		{
			if (GameSettings.Instance.ShowBlood)
			{
				GameObject go = bl_ObjectPoolingBase.Instance.Instantiate("blood", hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
				go.transform.parent = hit.transform;
			}
			debugStep = 20;

			if (_bulletData.isNetwork) return;

			IMFPSDamageable damageable = hit.transform.GetComponent<IMFPSDamageable>();
			if (damageable == null)
			{
				return;
			}
			debugStep = 21;
			DamageData damageData = BuildBaseDamageData();
			damageData.Direction = m_Transform.forward;
			debugStep = 22;
			//check if the bullet comes from a bot or a real player.
			if (_bulletData.MFPSActor != null)
			{
				damageData.Cause = _bulletData.MFPSActor.IsRealPlayer ? DamageCause.Player : DamageCause.Bot;
				debugStep = 23;
			}
			else
			{
				damageData.Cause = DamageCause.Player;
			}

			debugStep = 24;
			damageable.ReceiveDamage(damageData);

			debugStep = 25;

		}

		/// <summary>
		/// Apply damage to a bot from a Real Player or Another bot
		/// </summary>
		void SendBotDamage(RaycastHit hit)
		{
			//apply damage only if this is the Local Player, we could use only On Master Client to have a more "Authoritative" logic but since the
			//Ping of Master Clients can be volatile since it depend of the client connection, that could affect all the players in the room.
			if (!_bulletData.isNetwork)
			{
				if (_bulletData.MFPSActor == null)
				{
					Debug.LogWarning($"Bullet ownerID {_bulletData.ActorViewID} not found, ignore this if a player just enter in the match.");
					return;
				}
				if (hit.transform.root != null && _bulletData.MFPSActor.NickName == hit.transform.root.name) { return; }
				IMFPSDamageable damageable = hit.transform.GetComponent<IMFPSDamageable>();

				if (damageable != null)
				{
					//callback is in bl_HitBox.cs
					var damageData = BuildBaseDamageData();
					damageable.ReceiveDamage(damageData);
				}
			}

			GameObject go = bl_ObjectPoolingBase.Instance.Instantiate("blood", hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
			go.transform.parent = hit.transform;
		}

		/// <summary>
		/// 봇이 유저를 맞힘.
		/// </summary>
		void SendPlayerDamageFromBot(RaycastHit hit, out bool isLocalPlayerHit)
		{
			//Bots doesn't hit the players HitBoxes like other real players does, instead they hit the Character Controller Collider,
			//So instead of communicate with the hit box script we have to communicate with the player health script directly
			var pdm = hit.transform.GetComponent<bl_PlayerReferences>();

			if (pdm == null || _bulletData.MFPSActor == null)
			{
				isLocalPlayerHit = false;
				return;
			}
			
			isLocalPlayerHit = pdm.playerNetwork != null && pdm.playerNetwork.isMine;

			if (!isLocalPlayerHit)
			{
				GameObject go = bl_ObjectPoolingBase.Instance.Instantiate("blood", hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
				go.transform.parent = hit.transform;
			}

			if (_bulletData.MFPSActor.IsRealPlayer)
			{
				return;
			}
			
			if (_bulletData.isNetwork) return;

			
			if (!isOneTeamMode)
			{
				if (/*--!bl_RoomSettings.Instance.CurrentRoomInfo.friendlyFire &&--*/
					pdm.playerSettings.PlayerTeam == _bulletData.MFPSActor.Team)//if hit a team mate player
				{
					return;
				}
			}
			DamageData info = BuildBaseDamageData();
			info.Actor = null;
			info.Cause = DamageCause.Bot;
			pdm.playerHealthManager.DoDamage(info);
		}

		void InstanceHitParticle(string poolPrefab, RaycastHit hit, bool overrideDamageable = false)
		{
			// instantiate particle
			GameObject go = bl_ObjectPoolingBase.Instance.Instantiate(poolPrefab, hit.point, Quaternion.LookRotation(hit.normal));
			// instance decal
			bl_BulletDecalManagerBase.InstantiateDecal(hit);

			if (go != null)
				go.transform.parent = hit.transform;

			if (_checkDamageables || overrideDamageable)
			{
				var damageable = hit.transform.GetComponent<IMFPSDamageable>();
				if (damageable == null) return;
				DamageData damageData = BuildBaseDamageData();
				damageData.Cause = DamageCause.Player;
				damageable.ReceiveDamage(damageData);
			}
		}

		DamageData BuildBaseDamageData()
		{
			DamageData data = new DamageData()
			{
				Damage = (int)_bulletData.Damage,
				Position = _bulletData.Position,
				MFPSActor = _bulletData.MFPSActor,
				ActorViewID = _bulletData.ActorViewID,
				WeaponCode = _bulletData.WeaponCode,
			};
			if (data.MFPSActor != null) { data.From = data.MFPSActor.NickName; }
			return data;
		}
	}
}