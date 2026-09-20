## 무기별 탄퍼짐(Bullet Spread) 설계

왜 무기마다, 그리고 같은 무기라도 상황(서기/앉기/걷기/뛰기)마다 탄퍼짐을 다르게 줘야 하는가 하는 문제와, 그 결과값을 어떻게 최소한의 데이터로 네트워크에 실어 보내는가 하는 문제를 함께 다룬다.

기획 데이터는 `Weapon.xlsx`에서 무기별로 관리한다. 기본 탄퍼짐 각도(`DefaultSpreadAngle`)와 상태별 배수(`SpreadSitMultiply`, `SpreadWalkMultiply`, `SpreadRunMultiply`)를 무기마다 다르게 세팅할 수 있게 해서, 40종 무기(8개 유형)의 탄퍼짐 밸런스를 코드 수정 없이 시트에서 조정할 수 있다. 시트의 컬럼 키는 `SpreadAngle`, `SpreadAngleZoom`, `SpreadSitMultiply`, `SpreadWalkMultiply`, `SpreadRunMultiply`이고 코드에서는 `WeaponDef`를 통해 위 프로퍼티 이름으로 읽는다. 반동(`RecoilAmount`)은 시트가 아니라 `bl_Gun`의 인스펙터 필드로 남아 있어 이 문서의 범위가 아니다.

<img width="332" height="209" alt="Weapon테이블_01" src="https://github.com/user-attachments/assets/e802842b-876e-4f05-8926-f3ada6caad43" />
<img width="538" height="171" alt="Weapon테이블_02" src="https://github.com/user-attachments/assets/8c7aa5c6-1d32-46bc-ae2e-5a19bdd67003" />

### 최종 탄퍼짐 각도 산출 (`bl_Gun.cs`)

서 있는 상태의 기본 각도에서 시작해서, 앉기/걷기/뛰기 상태에 따라 배수를 곱하는 방식이다. 예를 들어 뛰면서 사격하면 `DefaultSpreadAngle * SpreadRunMultiply`가 된다. 코드상 곱하는 순서는 `SpreadSitMultiply`가 먼저, 이동 상태 배수가 그 다음이지만, 두 함수가 모두 `firstPersonController.State`라는 단일 값을 `==`/`IsOneOf`로 비교하므로 `Crouching`인 프레임에는 걷기/뛰기 분기가 실행되지 않는다(`PlayerState` 정의 자체는 이 저장소 사본에 없어 비교 방식과 `CompressStates`의 4비트 패킹으로 판단했다). 즉 현재 코드에서 앉기 배수와 이동 배수가 함께 곱해지는 경우는 없고, 점프/낙하(`Jumping`, `Dropping`)는 뛰기 배수를 공유한다.

```csharp
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
```

조준(Zoom) 여부에 따라 기준값 자체를 `DefaultSpreadAngle` 대신 `DefaultSpreadAngleForZoom`으로 바꿔치기하는 것도 이 시점에서 처리한다. 시트 값으로 보면 대부분의 총기는 줌 기준값이 `1`이고 저격소총은 `0.1` 안팎이라, 조준 시 각도 자체가 크게 줄어든다.

### 각도를 압축된 정수로 패킹 (`BulletData.cs`)

발사 시점에 `GetSpreadAngle`로 구한 최종 각도(`spreadAngle`)를 받아서, 그 범위 안에서 무작위 pitch/roll을 뽑고 이를 `PackedInaccuracity`라는 정수 하나로 압축해 저장한다.

```csharp
public BulletData SetInaccuracity(float spreadAngle, float bulletSpeed = 100.0f)
{
	float halfSpreadAngle = spreadAngle * 0.5f;
	float randomPitch = Random.Range(0, halfSpreadAngle);
	float randomRoll = Random.Range(0, 360);
	PackedInaccuracity = NetworkDataCompressor.CompressInaccuracity(bulletSpeed, randomPitch, randomRoll);
	return this;
}
```

`bl_Gun.cs`의 `BuildBulletData()`에서 `SetInaccuracity`가 호출된다.

```csharp
BulletSettings.SetInaccuracity(SpreadAngle, bulletSpeed);
```

`NetworkDataCompressor.CompressInaccuracity`는 이 폴더의 `NetworkDataCompressor.cs`에 있고, 플레이어/봇 이동 패킷 압축까지 함께 담은 팀 공용 코드다. bulletSpeed/pitch/roll 세 개의 float를 다음과 같은 비트 배치로 int 하나에 담는다.

| 비트 | 내용 | 변환 |
|---|---|---|
| 0~15 | bulletSpeed | `(ushort)bulletSpeed` (소수점 이하 버림) |
| 16~23 | pitch | `pitch * (20 * 255 / 360)`을 255에서 잘라 1바이트로 (0~18도, 약 0.07도 간격) |
| 24~31 | roll | `roll * (255 / 360)` 1바이트 (0~360도, 약 1.41도 간격) |

```csharp
const float DegreeToByte = 255f / 360f;
const float PitchDegreeToByte = 20 * 255f / 360f;
const float ByteToPitchDegree = 360f / 255f / 20f;

// ... 중략 ...
pitch8 = (byte)Math.Min(pitch * PitchDegreeToByte, 255);

byte roll8 = (byte)(roll * DegreeToByte);
packed |= ((uint)(ushort)bulletSpeed) & 0xFFFF;
packed |= (uint)(pitch8) << 16;
packed |= (uint)(roll8) << 24;
```

복원(`DecompressInaccuracity`)은 같은 위치에서 비트를 꺼내 `pitch8 * ByteToPitchDegree`, `roll8 * ByteToDegree`로 되돌린다. pitch는 "거의 0~15도 사이의 작은 값"이라는 주석대로 roll보다 20배 촘촘한 변환비를 쓴다.

이 파일을 건드린 본인 커밋은 git 이력(2025-01 이후)에서 2025-11-24의 `Revert "[LR-656] 봇 이동 패킷 압축 2차 - 오류 수정"`과 이를 되돌린 `Reapply` 2건이 확인된다. 다만 Reapply diff를 확인하면 고친 대상은 `CompressBotStatus`/`DecompressBotStatus`/`CompressAngle`(봇 이동 패킷)이고 `CompressInaccuracity`/`DecompressInaccuracity`는 바뀌지 않았다. 수정 내용은 두 가지다. 하나는 부호 확장 버그로, 음수 `sbyte`를 `(ulong)(sbyte)x`로 바꾸면 상위 비트가 모두 1이 되어 `|=` 시 위쪽 필드(속도 Z, 방향, pitch)가 오염되므로 `(ulong)(byte)(sbyte)x`로 고쳤다. 다른 하나는 각도 wrap 처리로, 이후 대입에서 덮어써져 결과에 영향이 없던 정규화 코드를 걷어내고 테스트를 `Mathf.DeltaAngle` 기준으로 바꿨다. 같은 날 Revert된 뒤 다시 적용된 이력이며, 이 수정을 처음 넣은 커밋은 export 범위에 없다. 탄퍼짐 패킹 자체의 설계자가 누구인지는 이 자료로는 판단할 수 없다.

### 압축된 값을 다시 방향 벡터로 (`BulletData.cs` → `Bullet.cs`)

총알이 실제로 생성되는 시점에는 압축된 정수를 다시 풀어서 회전값으로 바꾸고, 총구 방향 벡터에 pitch → roll 순서로 회전을 적용해 최종 이동 방향/속도 벡터를 만든다.

```csharp
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
```

```csharp
// 총구 방향 속도 벡터
_velocity = data.Speed * m_Transform.forward;
// 탄퍼짐 적용
_velocity = m_Transform.TransformDirection(BulletData.ToInaccuracityVector(data.PackedInaccuracity));
```

`ToInaccuracityVector`가 만드는 벡터는 로컬 좌표계 기준이므로, 총구의 `Transform`으로 `TransformDirection`을 거쳐야 실제 월드 공간에서의 탄퍼짐 방향이 나온다.

위 발췌에서 첫 줄의 `_velocity = data.Speed * m_Transform.forward;`는 바로 다음 줄에서 값이 통째로 덮어써지고 그 사이에 `_velocity`를 읽는 코드가 없어서 실질적으로 죽은 코드다. 실제 탄 속도는 `data.Speed`가 아니라 패킹된 값에서 복원한 `bulletSpeed`(`ToInaccuracityVector` 안에서 `Vector3.forward * bulletSpeed`)가 결정한다. 이 흐름은 `Bullet.cs` 70~72행에 그대로 있다. 로컬 발사자의 탄도 같은 경로(`BulletSettings` → `InitProjectile` → `Bullet`)로 패킹/복원을 거치므로, 발사자와 원격 클라이언트가 같은 양자화된 값을 쓰게 된다.

<img width="2132" height="881" alt="탄퍼짐" src="https://github.com/user-attachments/assets/7626583e-aa6b-43e2-b5ae-fec7cc371029" />

### 설계 포인트

- **곱셈 방식의 배수 체계**: 상태별 배수를 더하는 대신 곱하는 방식을 택해서, 무기별로 기준각과 배수 몇 개만 시트에 채우면 밸런싱이 끝난다. 다만 새 이동 상태를 추가하려면 시트 컬럼과 `WeaponDef` 프로퍼티, `MultiplyVelocitySpreadOffset`의 분기를 함께 늘려야 하므로 "코드 수정 없이"가 성립하는 범위는 기존 상태의 수치 조정까지다.
- **원본 float 대신 압축된 정수 하나로 전달**: pitch/roll/bulletSpeed를 각각의 float로 들고 다니는 대신 `PackedInaccuracity` 정수 하나로 합쳐서 `BulletData`에 담아 넘긴다. 이 값은 `bl_Gun.cs`의 `PlayerNetwork.ReplicateFire(OriginalWeaponType, instanceData.ProjectedHitPoint, BulletSettings.PackedInaccuracity)` 호출에 실려 나가므로, 수신 측이 같은 정수를 `ToInaccuracityVector`에 넣으면 발사자와 같은 방향이 나오는 구조다. 수신 측(`PlayerNetwork`) 코드는 이 저장소 사본에 없어서 실제 재생 경로까지는 확인하지 못했다.
- **압축/해제를 데이터 클래스 쪽에 둠**: `SetInaccuracity`(압축, 인스턴스 메서드)와 `ToInaccuracityVector`(해제, `static`)를 `BulletData`에 두어, 발사 시점과 실제 탄 생성 시점이 분리되어 있어도 같은 `NetworkDataCompressor` 알고리즘으로 왕복 변환한다.

> 근거:
> - `Weapon.xlsx`(`Database` 시트)에는 40종 무기가 있고 유형별로 중화기 7, 권총 6, 소총 6, SMG 5, 저격소총 5, 근접 5, 투척 3, 전술 3이다(8개 유형). 탄퍼짐 5개 컬럼은 40행 모두 채워져 있다. 기존 문서의 "약 20종"은 실제 시트와 맞지 않아 정정했다.
> - git 이력(2025-01 이후)에서 확인되는 본인 커밋 기준으로, `[LR-197] 1인칭 탄 퍼짐(정확도) 로직 수정`은 7건(2025-02-05 ~ 2025-03-07, 이 중 6건이 02-05 ~ 02-11)이다. 2025-02-11에 "탄 퍼짐 관련 프로퍼티 시트로 이전"과 "Weapon.xlsx Desc에 설명 텍스트 추가" 커밋이 있다.
> - 봇: `[LR-639]` "[Bot] 플레이어와 동일한 로직으로 변경 (총기 탄퍼짐 + 현재 동작 상태에 따른 팩터 적용)" 5건(2025-11-11 3건, 11-12 2건)과 이후 `LR-637`(11-18, 2건), `LR-656`(11-20, 1건)의 봇 탄퍼짐 수정 커밋이 확인된다. 다만 현재 코드(`bl_AIShooterAgent.cs`의 `GetSpreadAngle`)에서 봇은 같은 시트 값(기준각, 앉기/뛰기 배수)을 쓰되, 이동 속도에 따라 `Lerp(1, SpreadRunMultiply, NormalSpeed)`로 배수를 연속 보간하고, 목표 지점을 `DecideTargetPosition`에서 yaw 방향으로만 무작위 편차를 주는 방식이라 `bl_Gun.GetSpreadAngle`/`SetInaccuracity` 경로와는 별도로 구현되어 있다. "동일한 로직"은 데이터와 배수 체계를 공유한다는 의미로 읽는 것이 정확하다.
> - 위 커밋 이력은 export된 2025-01-06 ~ 2025-11-26 범위의 본인 커밋만 반영한 것이며, 그 이전 이력과 다른 팀원의 작업은 포함되지 않는다.


관련 코드: [05. PhotonNetwork](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/05.%20PhotonNetwork) — `PackedInaccuracity`가 발사 이벤트와 함께 네트워크로 복제되는 지점
