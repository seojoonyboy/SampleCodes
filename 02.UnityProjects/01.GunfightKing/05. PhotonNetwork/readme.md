## Photon 기반 AI 봇 상태 동기화 (`bl_AIShooterNetwork.cs`)

최대 8명이 붙는 PVP 매치에 봇까지 섞여 있어서, 봇 한 마리의 위치·속도·방향을 `OnPhotonSerializeView`로 계속 뿌려야 한다. `Vector3`/`Quaternion`을 그대로 보내면 봇이 늘어날수록 패킷이 그대로 커진다. 이 문서는 봇 상태를 어떤 정밀도로 어떤 비트에 담아 보냈는지, 그 설계가 한 번 더 개정된 과정과 검증 방법, 그리고 지금 구조가 가진 한계를 정리한다.

권한 구조는 Master-Slave 방식이다. Master Client가 NavMesh로 봇을 실제로 움직이고(시뮬레이션은 [07. MasterClientBotManaging](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/07.%20MasterClientBotManaging)에서 Master에서만 돈다), 그 결과를 나머지 클라이언트에 전송한다. Slave는 받은 값을 스냅하지 않고 보간해서 재현한다.

### 1. 전송 데이터: 필드별 압축(1차) → 하나의 `long`(2차)

1차 구현은 필드마다 압축 함수를 따로 두고 `SendNext`를 6번 호출했다. 2차 구현(현재 샘플)은 위치 XZ, 속도 XZ, 방향, 피치를 `long` 하나에 담고 Y 위치만 `short`로 따로 보낸다.

| 구분 | 전송 항목 | 값 크기 합계 |
|---|---|---|
| 압축 끔 (`UsePhotonStreamCompress == false`) | pos `Vector3` 12 B + rot `Quaternion` 16 B + vel `Vector3` 12 B + lookAt `Vector3` 12 B + pitch `float` 4 B (5개 항목) | 56 B |
| 1차 압축 | posXZ `int` 4 + posY `short` 2 + yaw `byte` 1 + velXZ `int` 4 + velY `short` 2 + botLook `long` 8 (6개 항목) | 21 B |
| 2차 압축 (현재) | botStatus `long` 8 + posY `short` 2 (2개 항목) | 10 B |

값 자체의 크기만 합산한 것이고 Photon 직렬화가 항목마다 붙이는 타입 정보는 포함하지 않았다. 항목 수도 함께 줄었으므로 실제 차이는 표보다 다소 클 것으로 예상하지만, 실측한 값은 아니다.

2차 압축의 `long` 하나의 비트 배치는 아래와 같다(`NetworkDataCompressor.CompressBotStatus`, 파일은 [06. BulletSpread](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/06.%20BulletSpread)에 있다).

| 비트 | 내용 | 표현 |
|---|---|---|
| 0–15 | 위치 X | `int16`, 1 cm 해상도 (±327.67 m) |
| 16–31 | 위치 Z | `int16`, 1 cm 해상도 |
| 32–39 | 속도 X | `sbyte`, 0.1 m/s 해상도, ±12.7 m/s로 클램프 |
| 40–47 | 속도 Z | `sbyte`, 0.1 m/s 해상도 |
| 48–55 | 방향(yaw) | `byte`, 360°를 255단계로 |
| 56–63 | 조준 피치 | `sbyte`, 1° 해상도 (−90~90) |

```csharp
public static long CompressBotStatus(Vector3 pos, Vector3 vel, float rot, float lookPitch)
{
	ulong packed = 0;
	// 위치는 0.01m 해상도
	packed |= (ulong)(ushort)(pos.x * 100);
	packed |= (ulong)(ushort)(pos.z * 100) << 16;

	// 속도는 0.1 (m/s) 해상도로 1바이트로 압축
	sbyte velX = (sbyte)Math.Clamp(Mathf.RoundToInt(vel.x * 10), -127, 127);
	sbyte velZ = (sbyte)Math.Clamp(Mathf.RoundToInt(vel.z * 10), -127, 127);
	packed |= (ulong)(byte)velX << 32;
	packed |= (ulong)(byte)velZ << 40;

	// 방향
	var angle = CompressAngle(rot);
	packed |= (ulong)angle << 48;

	// lookPitch(-90 ~ 90)는 1바이트로 압축
	var pitch = (ulong)(byte)(sbyte)Mathf.RoundToInt(lookPitch);
	packed |= pitch << 56;
	return unchecked((long)packed);
}
```

Y 위치를 `long`에서 뺀 이유는 코드 주석이 밝히고 있다. 평지에서는 Y가 거의 변하지 않아 XZ와 같은 비트 예산을 줄 필요가 없다는 판단이다. 또한 Y 속도와 `LookAtPosition`은 2차에서 아예 전송하지 않는다. 조준 방향은 피치만 보내고, 좌우 방향은 몸통 yaw로 대신하는 구조다.

### 2. 송신과 수신 (`bl_AIShooterNetwork.cs`)

```csharp
public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
{
	if(_isBeingDestroyed) {  return; }

	if (stream.IsWriting)
	{
		if (GVConstants.UsePhotonStreamCompress)
		{
			stream.SendNext(Compressor.CompressBotStatus(m_Transform.localPosition, Agent.velocity, m_Transform.localRotation.eulerAngles.y, References.aiShooter.LookAtPitch));
			stream.SendNext(Compressor.CompressPosY(m_Transform.localPosition));
		}
		else
		{
			stream.SendNext(m_Transform.localPosition);
			stream.SendNext(m_Transform.localRotation);
			stream.SendNext(Agent.velocity);
			stream.SendNext(References.aiShooter.LookAtPosition);
			stream.SendNext(References.aiShooter.LookAtPitch);
		}
	}
	else
	{
		if (GVConstants.UsePhotonStreamCompress)
		{
			long status = (long)stream.ReceiveNext();
			short posY = (short)stream.ReceiveNext();

			Vector3 pos; Vector3 vel; float heading; float pitch;
			Compressor.DecompressBotStatus(status, out pos, out vel, out heading, out pitch);

			correctPlayerPos.x = pos.x;
			correctPlayerPos.z = pos.z;
			correctPlayerPos.y = posY / 100f;
			correctPlayerRot = Quaternion.Euler(0, heading, 0);
			Velocity = vel;
			_networkLookAtPitch = pitch;
		}
		else { /* 원본 값을 그대로 수신 */ }

		//Fix the translation effect on remote clients
		if (receivePackages < 5)
		{
			m_Transform.localPosition = correctPlayerPos;
			m_Transform.localRotation = correctPlayerRot;
			receivePackages++;
		}
	}
}
```

받은 값은 `OnUpdate`에서 프레임마다 보간한다. Master 본인은 보간을 타지 않고 `Agent.velocity`를 자기 상태로 쓴다.

```csharp
public override void OnUpdate()
{
	if (!PhotonNetworkEx.IsMasterClient)//if not master client, then get position from server
	{
		m_Transform.localPosition = Vector3.Lerp(m_Transform.localPosition, correctPlayerPos, Time.deltaTime * 7);
		m_Transform.localRotation = Quaternion.Lerp(m_Transform.localRotation, correctPlayerRot, Time.deltaTime * 7);
		References.aiShooter.LookAtPosition = Vector3.Lerp(References.aiShooter.LookAtPosition, networkLookAtPosition, Time.deltaTime * 5);
		References.aiShooter.LookAtPitch = Mathf.Lerp(References.aiShooter.LookAtPitch, _networkLookAtPitch, Time.deltaTime * 5);
	}
	else
	{
		Velocity = Agent.velocity;
		if (BattleManager.Instance.IsTimeUp())
		{
			if(Agent.enabled) Agent.isStopped = true;
		}
	}
}
```

<img width="1909" height="1027" alt="image" src="https://github.com/user-attachments/assets/f84321e4-54c7-4bce-b0d6-e786c29498c3" /><br/>
Master의 NavMesh 이동 결과가 Slave 클라이언트에서 재현되는 모습.

점프처럼 상태값으로 표현하기 어려운 이벤트는 스트림에 싣지 않고 RPC로 따로 보낸다. `SendRPCJump`가 `RpcTarget.Others`로 닉네임을 보내면 수신 측 `JumpSync`가 해당 봇을 찾아 `ReplicaJump()`로 점프 애니메이션만 재생한다.

### 3. 압축 오류를 잡고 검증한 과정

비트 패킹은 한 필드의 실수가 옆 필드를 망가뜨린다. 2차 압축도 이 문제를 겪었고, 2025-11-24에 `[LR-656] 봇 이동 패킷 압축 2차 - 오류 수정` 커밋(Revert 후 Reapply 커밋 한 쌍)으로 고쳤다. 이 시점의 diff에서 확인되는 수정은 다음과 같다.

- **부호 확장(sign extension)**: 이전 코드는 `(ulong)(sbyte)값`으로 캐스팅했다. 음수 `sbyte`는 `ulong`으로 바뀔 때 상위 비트가 모두 1로 채워진다. 그래서 속도 X가 음수이면 그 위 비트에 놓인 속도 Z·방향·피치가 모두 오염된다. 현재 코드는 `(ulong)(byte)`를 거쳐 8비트만 남긴다.
- **속도 클램프**: 정수로 반올림한 뒤 `[-127, 127]`로 자른다. 이전에는 `float`를 ±100으로 자른 뒤 `sbyte`로 캐스팅했다.
- **각도 정규화 코드 정리**: `CompressAngle`에서 `% 360`으로 계산한 값을 바로 다음 줄에서 덮어쓰고 있어 결과에 영향이 없는 코드였다. 이 부분을 제거했다.

같은 커밋에서 EditMode 단위 테스트 [`PlayerNetworkCompressTest.cs`](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/05.%20PhotonNetwork/PlayerNetworkCompressTest.cs)도 함께 고쳤다. 각도 테스트를 "입력에 대한 기대값을 손으로 적는 방식"에서 "압축 후 복원한 값이 원래 각도와 허용 오차 안에 드는지 보는 왕복(round-trip) 방식"으로 바꿨고, 음수 속도와 −90° 피치를 한 번에 넣는 케이스와 여러 방향(90°~400°, −90°~−400°) 케이스를 추가했다.

```csharp
const float ByteAngleError = 360f / 255f * 2;
const float PositionError = 0.01f * 2;
const float BotVelError = 0.1f * 2;

// vel은 -10 ~ 10 범위, heading은 0 ~ 360, pitch는 -90 ~ 90
TestBotStatus(0, 0, -10, 10, 0, -90);
TestBotStatus(100, -100, -10, 10, 359, 90);
TestBotStatus(0, 0, -1, -1, 0, 0);
TestBotStatus(0, 0, 0, 0, -400, 0);   // 범위를 벗어난 각도
```

위치는 ±100 m와 `short` 경계(약 ±327 m), 속도는 음수 조합, 방향은 −500°~500°, 피치는 ±90°를 각각 검사한다. 부호 확장 같은 오류는 "음수 값을 넣었을 때 옆 필드가 깨지는지"로만 드러나기 때문에, 음수와 경계값을 넣는 테스트가 회귀를 막는 가장 싼 방법이다.

### 설계 포인트

- **정밀도를 필드별로 달리 배정**: 위치는 1 cm(`int16`), 속도는 0.1 m/s(`sbyte`), 방향은 약 1.4°(`byte`), 피치는 1°(`sbyte`)이다. 봇이 화면에서 티 나게 어긋나지 않는 수준의 정밀도만 남기고, 나머지를 비트 예산에서 덜어냈다.
- **런타임 스위치**: `GVConstants.UsePhotonStreamCompress`로 압축 경로와 원본 전송 경로를 함께 남겼다. 압축에 문제가 생기면 바로 원본 전송으로 되돌릴 수 있고, 두 경로의 대역폭을 나란히 비교할 수도 있다.
- **초반 패킷은 스냅**: `receivePackages`가 5 미만인 동안은 Lerp 없이 위치·회전을 대입한다. 스폰 직후 (0,0,0)에서 실제 위치까지 미끄러지는 현상을 막는다.
- **Master 시뮬레이션 + Slave 재현**: 경로 탐색과 판단은 Master에서만 일어나고 Slave는 수신·보간만 한다. 봇 수가 늘어도 클라이언트별 연산 비용이 함께 늘지 않는다.
- **전송 빈도는 별도 축**: `TitleLogic`에서 `PhotonNetwork.SerializationRate = FrontMeta.PSR`로 초당 직렬화 횟수를 설정 값에 맞춘다. 패킷 크기는 코드로 줄이고, 빈도는 설정 값으로 조정하는 구조다.

> 근거:
> - 위 표의 바이트 합계는 `bl_AIShooterNetwork.cs`와 `NetworkDataCompressor.cs`의 필드 타입을 합산한 값이다. 실제 Photon 프레임의 총 크기나 대역폭을 측정한 결과가 아니다.
> - 2차 압축을 고친 커밋은 `[LR-656]` "봇 이동 패킷 압축 2차 - 오류 수정"이며 2025-11-24의 Revert/Reapply 한 쌍으로 확인된다. 위 3절의 수정 내용과 테스트 변경은 이 커밋의 diff에서 옮겼다. 2차 압축의 최초 구현 커밋은 확인 가능한 이력에 없어, 최초 구현의 작성자는 이 문서에서 주장하지 않는다.
> - 점프 동기화 RPC는 `[LR-637]`(2025-11) 작업이다. 전송 빈도 값(`PSR`)은 코드에서 `FrontMeta.PSR`을 읽어 적용하는 것까지만 확인했다. 값이 어디서 정해지고 누가 넣었는지는 확인하지 않았다.
> - "Photon 패킷 대역폭 30% 이상 절감"은 포트폴리오에 적은 성과 수치이며, 이 저장소의 코드나 커밋만으로는 재현되지 않는다.


이런 상태 압축과 전송 빈도 튜닝을 함께 적용해 Photon 패킷 대역폭을 30% 이상 절감했다.

관련 코드:
- [07. MasterClientBotManaging](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/07.%20MasterClientBotManaging) — 이 네트워크 컴포넌트를 소유하는 봇 에이전트 본체
- [04. StateMachine](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/04.%20StateMachine) — 여기서 동기화되는 봇 상태(이동/조준 등)를 결정하는 상태 머신
- [06. BulletSpread](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/06.%20BulletSpread) — 압축 함수 본체(`NetworkDataCompressor.cs`)와 탄퍼짐 패킹
