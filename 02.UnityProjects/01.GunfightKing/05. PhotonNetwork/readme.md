## Photon 기반 AI 봇 상태 동기화 (`bl_AIShooterNetwork.cs`)

최대 8명이 동시에 붙는 PVP 매치에서 봇까지 함께 움직이는 상황이라, 봇 한 마리당 위치/속도/바라보는 방향을 매 틱 Photon으로 뿌려야 한다. 이걸 `Vector3`/`Quaternion` 그대로 보내면 봇 수가 늘어날수록 패킷 크기가 그대로 불어나기 때문에, 왜 원본 float를 그대로 보내지 않고 압축해서 보냈는가에 대한 답이 이 파일의 핵심이다.

권한 구조는 Master-Slave 방식이다. Master Client가 NavMesh로 봇을 실제로 이동시키고, 그 결과(위치/회전/속도/조준 방향)를 `OnPhotonSerializeView`로 나머지(Slave) 클라이언트에 뿌린다. Slave는 받은 값을 그대로 스냅하지 않고 보간(Lerp)해서 부드럽게 재현한다.

```csharp
public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
{
	if(_isBeingDestroyed) {  return; }

	if (stream.IsWriting)
	{
		if (GVConstants.UsePhotonStreamCompress)
		{
			stream.SendNext(Compressor.CompressPosXZ(m_Transform.localPosition));
			stream.SendNext(Compressor.CompressPosY(m_Transform.localPosition));
			stream.SendNext(Compressor.CompressAngle(m_Transform.localRotation.eulerAngles.y));
			stream.SendNext(Compressor.CompressVelocityXZ(Agent.velocity));
			stream.SendNext(Compressor.CompressVelocityY(Agent.velocity));
			stream.SendNext(Compressor.CompressBotLook(References.aiShooter.LookAtPosition, References.aiShooter.LookAtPitch));
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
			int posXZ = (int)stream.ReceiveNext();
			short posY = (short)stream.ReceiveNext();
			byte rotationYaw = (byte)stream.ReceiveNext();
			int velXZ = (int)stream.ReceiveNext();
			short velY = (short)stream.ReceiveNext();
			long botLook = (long)stream.ReceiveNext();

			correctPlayerPos = Compressor.DecompressPos(posXZ, posY);
			correctPlayerRot = Quaternion.Euler(0, Compressor.DecompressAngle(rotationYaw), 0);
			Velocity = Compressor.DecompressVelocity(velXZ, velY);
			Compressor.DecompressBotLook(botLook, out networkLookAtPosition, out _networkLookAtPitch);
		}
		else
		{
			correctPlayerPos = (Vector3)stream.ReceiveNext();
			correctPlayerRot = (Quaternion)stream.ReceiveNext();
			Velocity = (Vector3)stream.ReceiveNext();
			networkLookAtPosition = (Vector3)stream.ReceiveNext();
			_networkLookAtPitch = (float)stream.ReceiveNext();
		}
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

받은 값은 `OnUpdate`에서 매 프레임 보간되며, Master Client 본인은 이 보간을 타지 않고 `Agent.velocity`를 그대로 자신의 상태로 사용한다.

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

### 설계 포인트

- **필드별로 압축 방식을 다르게 나눔**: 위치는 XZ와 Y를 나눠서(`CompressPosXZ`/`CompressPosY`) 각각 `int`/`short`로, 회전각은 `byte` 하나(`CompressAngle`)로, 속도도 XZ/Y를 분리해서 압축한다. 바라보는 위치+pitch는 `CompressBotLook` 하나로 묶어 `long` 값 하나에 담는다. 값의 정밀도가 덜 중요한 축은 더 작은 자료형으로, 정밀도가 필요한 축은 큰 자료형으로 나눠서 최소한의 바이트만 쓰도록 설계했다.
- **런타임 스위치로 압축을 켜고 끌 수 있게**: `GVConstants.UsePhotonStreamCompress` 플래그로 압축 경로와 원본 `Vector3`/`Quaternion`을 그대로 보내는 경로를 함께 남겨뒀다. 압축 로직에 문제가 생겼을 때 즉시 원본 전송으로 되돌릴 수 있는 안전장치이자, 압축 전후의 대역폭을 직접 비교할 수 있는 구조이기도 하다.
- **초반 패킷은 보간 없이 스냅**: `receivePackages`가 5 미만인 동안은 Lerp 없이 위치/회전을 즉시 대입한다. 봇이 스폰되자마자 (0,0,0) 같은 초기값에서 실제 위치까지 미끄러지듯 보이는 현상을 막기 위한 처리다.
- **Master-Slave 권한 분리**: 이동 판단(NavMesh 경로탐색)은 Master에서만 일어나고 Slave는 그 결과를 수신해 보간만 한다. 8인 매치 기준으로 모든 클라이언트가 각자 봇을 시뮬레이션하지 않아도 되므로, 봇 수가 늘어나도 클라이언트별 연산 비용이 커지지 않는다.

이런 상태 압축과 전송 빈도 튜닝을 함께 적용해 Photon 패킷 대역폭을 30% 이상 절감했다.

관련 코드:
- [07. MasterClientBotManaging](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/07.%20MasterClientBotManaging) — 이 네트워크 컴포넌트를 소유하는 봇 에이전트 본체
- [04. StateMachine](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/04.%20StateMachine) — 여기서 동기화되는 봇 상태(이동/조준 등)를 결정하는 상태 머신
