주요 역할

1. Master Client인 경우, Bot이 Nav Mesh를 통해 이동하게 된다. 이동한 위치와 회전값, 수직 상하 각도(수직으로 어느 방향을 향하고 있는가) 등을 Slave에게 공유한다.
<pre>
  <code>
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

				Vector3 pos;
				Vector3 vel;
				float heading;
				float pitch;
				Compressor.DecompressBotStatus(status, out pos, out vel, out heading, out pitch);
				
				correctPlayerPos.x = pos.x;
				correctPlayerPos.z = pos.z;
				correctPlayerPos.y = posY / 100f;
				correctPlayerRot = Quaternion.Euler(0, heading, 0);
				Velocity = vel;
				_networkLookAtPitch = pitch;
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
  </code>
</pre>

2. Master의 Bot이 NavMesh의 Jump 구간을 만나 Jump 하는 경우, RPC 함수를 통해 Jump를 Slave 클라이언트에서 동기화 한다.
<pre>
  <code>
    //Slave에게 Jump 애니메이션 재생을 요청한다.
	public void SendRPCJump(string nickName)
	{
		photonView.RPC(nameof(JumpSync), RpcTarget.Others, nickName);
	}
	
	[PunRPC]
	void JumpSync(string nickName)
	{
		PlayerSeat targetBot = BattleManager.Instance.FindMFPSPlayerByNickname(nickName);
		
		if(targetBot == null) return;
		if(!targetBot.IsAlive) return;

		var shooterAgent = targetBot.Actor.GetComponent<bl_AIShooterAgent>();
		if (shooterAgent == null) return;
		
		AIAnimation aiAnimation = shooterAgent.References.aiAnimation as AIAnimation;
		if (aiAnimation == null) return;
		
		aiAnimation.ReplicaJump();
	}
  </code>
</pre>
