> 무기별로 탄퍼지는 효과를 다르게 주기 위한 설계에 대한 설명

걷고 있을 때, 뛰고 있을 때, 앉아 있을 때 기본 탄퍼짐에 배수배를 누적시키는 방식으로 함
예를 들어 SMG-5R 무기로 뛰면서 사격하는 경우 해당 무기 SpreadAngle (기존 탄퍼짐 정도) * SpreadWalkMultiply (걷고 있을 때 탄퍼짐 배수) 가 된다.

<img width="332" height="209" alt="Weapon테이블_01" src="https://github.com/user-attachments/assets/e802842b-876e-4f05-8926-f3ada6caad43" />
<img width="538" height="171" alt="Weapon테이블_02" src="https://github.com/user-attachments/assets/8c7aa5c6-1d32-46bc-ae2e-5a19bdd67003" />

그 결과값을 기준 방향 벡터를 상하좌우로 몇도 정도 각도까지 회전할 수 있는 범위를 제한하는 값으로 사용한다.

<pre>
  <code>
    public BulletData SetInaccuracity(float spreadAngle, float bulletSpeed = 100.0f)
		{
			float halfSpreadAngle = spreadAngle * 0.5f;
			float randomPitch = Random.Range(0, halfSpreadAngle);
			float randomRoll = Random.Range(0, 360);
			PackedInaccuracity = NetworkDataCompressor.CompressInaccuracity(bulletSpeed, randomPitch, randomRoll);
			return this;
		}
  </code>
</pre>

그리고 이 randomPitch 와 randomRoll 을 기존 float에서 압축한 형태로 변환하여 PackedInaccuracity 에 저장한다. -> NetworkDataCompressor.CompressInaccuracity [공동 작업]

실제 발사 조건이 되면 탄을 생성하면서 해당 PackedInaccuracity 방향으로 속도 벡터를 구한다. (나아가는 방향)

<pre>
  <code>
    _velocity = m_Transform.TransformDirection(BulletData.ToInaccuracityVector(data.PackedInaccuracity));
  </code>
</pre>

<img width="2132" height="881" alt="탄퍼짐" src="https://github.com/user-attachments/assets/7626583e-aa6b-43e2-b5ae-fec7cc371029" />
