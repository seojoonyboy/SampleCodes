## 무기별 탄퍼짐(Bullet Spread) 설계

왜 무기마다, 그리고 같은 무기라도 상황(서기/앉기/걷기/뛰기)마다 탄퍼짐을 다르게 줘야 하는가 하는 문제와, 그 결과값을 어떻게 최소한의 데이터로 네트워크에 실어 보내는가 하는 문제를 함께 다룬다.

기획 데이터는 `Weapon.xlsx`에서 무기별로 관리한다. 기본 탄퍼짐 각도(`DefaultSpreadAngle`)와 상태별 배수(`SpreadSitMultiply`, `SpreadWalkMultiply`, `SpreadRunMultiply`)를 무기마다 다르게 세팅할 수 있게 해서, 약 20종에 달하는 무기 카테고리의 반동/탄퍼짐 밸런스를 코드 수정 없이 시트에서 조정할 수 있다.

<img width="332" height="209" alt="Weapon테이블_01" src="https://github.com/user-attachments/assets/e802842b-876e-4f05-8926-f3ada6caad43" />
<img width="538" height="171" alt="Weapon테이블_02" src="https://github.com/user-attachments/assets/8c7aa5c6-1d32-46bc-ae2e-5a19bdd67003" />

### 최종 탄퍼짐 각도 산출 (`bl_Gun.cs`)

서 있는 상태의 기본 각도에서 시작해서, 앉기/걷기/뛰기 상태에 따라 배수를 곱해 누적시키는 방식이다. 예를 들어 뛰면서 사격하면 `DefaultSpreadAngle * SpreadRunMultiply`가 되고, 앉은 채로 걷는 것도 가능한 구조라 `SpreadSitMultiply`가 먼저 곱해진 뒤 이동 상태 배수가 한 번 더 곱해질 수 있다.

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

조준(Zoom) 여부에 따라 기준값 자체를 `DefaultSpreadAngle` 대신 `DefaultSpreadAngleForZoom`으로 바꿔치기하는 것도 이 시점에서 처리한다.

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

`NetworkDataCompressor.CompressInaccuracity`는 함께 작업한 동료의 코드라 이 폴더에는 포함되어 있지 않지만, bulletSpeed/pitch/roll 세 개의 float를 int 하나에 담는 역할을 한다. `bl_Gun.cs`의 `BuildBulletData()`에서 이 함수가 호출된다.

```csharp
BulletSettings.SetInaccuracity(SpreadAngle, bulletSpeed);
```

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

<img width="2132" height="881" alt="탄퍼짐" src="https://github.com/user-attachments/assets/7626583e-aa6b-43e2-b5ae-fec7cc371029" />

### 설계 포인트

- **곱셈 누적 방식**: 상태별 배수를 더하는 대신 곱하는 방식을 택해서, 무기 하나에 대해 상태값 몇 개만 시트에 채우면 자연스럽게 조합(앉아서 걷기 등)이 성립한다. 새로운 이동 상태가 추가돼도 코드 로직을 건드리지 않고 배수 컬럼만 늘리면 대응 가능하다.
- **원본 float 대신 압축된 정수 하나로 전달**: pitch/roll/bulletSpeed를 각각의 float로 들고 다니는 대신 `PackedInaccuracity` 정수 하나로 합쳐서 `BulletData`에 담아 넘긴다. 이 값은 `bl_Gun.cs`의 `PlayerNetwork.ReplicateFire(OriginalWeaponType, instanceData.ProjectedHitPoint, BulletSettings.PackedInaccuracity)` 호출을 통해 그대로 네트워크로 전파되므로, 발사자 로컬에서 결정한 탄퍼짐 방향을 다른 클라이언트에서도 동일하게 재현할 수 있다.
- **압축/해제를 분리된 정적 함수로**: `SetInaccuracity`(압축)와 `ToInaccuracityVector`(해제)를 데이터 클래스 쪽에 정적으로 둬서, 발사 시점(로컬)과 실제 탄 생성 시점(로컬/네트워크 공통)이 분리되어 있어도 동일한 알고리즘으로 왕복 변환이 가능하게 했다.

관련 코드: [05. PhotonNetwork](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/05.%20PhotonNetwork) — `PackedInaccuracity`가 발사 이벤트와 함께 네트워크로 복제되는 지점
