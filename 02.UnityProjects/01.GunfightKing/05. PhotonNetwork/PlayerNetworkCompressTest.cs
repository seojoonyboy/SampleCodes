using Game.View;
using NUnit.Framework;
using UnityEngine;
using Compressor = Game.View.BattleSystem.NetworkDataCompressor;

namespace GameViewTests
{
	public class PlayerNetworkCompressTest
	{
		const float ByteAngleError = 360f / 255f * 2;
		const float PositionError = 0.01f * 2;
		const float BotVelError = 0.1f * 2;

		[Test]
		public void PlayerNetworkCompressTest1()
		{
			Debug.Log("Angle:" + Vector3.Angle(new Vector3(0, 1, 0), new Vector3(0, 1, 0)));
			Debug.Log("Angle:" + Vector3.Angle(new Vector3(0, 1, 0), new Vector3(0.3f, 1, 0)));
			Debug.Log("Angle:" + Vector3.Angle(new Vector3(0, 1, 0), new Vector3(0.3f, -1, 0)));
			Debug.Log("Angle:" + Vector3.Angle(new Vector3(0, 1, 0), new Vector3(0, -1, 0)));
			Debug.Log("Byte angle error value: " + ByteAngleError);

			Debug.Log("Player Position 압축 테스트");
			TestPositionXZ(0, 0);
			TestPositionXZ(100f, 100f);
			TestPositionXZ(-100f, -100f);
			TestPositionXZ(short.MaxValue / 100, short.MinValue / 100); // 327

			Debug.Log("Angle 압축 테스트");
			TestEulerAngle(-500);
			TestEulerAngle(-370);
			TestEulerAngle(-190);
			TestEulerAngle(-180);
			TestEulerAngle(-0);
			TestEulerAngle(90);
			TestEulerAngle(180);
			TestEulerAngle(190);
			TestEulerAngle(370);
			TestEulerAngle(500);
			
			Debug.Log("Player States 압축 테스트");
			byte states = Compressor.CompressStates(PlayerState.Stealth, PlayerFPState.FireAiming, true);
			PlayerState state;
			PlayerFPState fpState;
			bool isGround;
			Compressor.DecompressStates(states, out state, out fpState, out isGround);
			Assert.IsTrue(state == PlayerState.Stealth && fpState == PlayerFPState.FireAiming && isGround == true);

			Debug.Log("Bot Look 압축 테스트");

			TestBotLook(0, 0, 0, 0); // 원점, 0 pitch
			TestBotLook(1.23f, 4.56f, 7.89f, 10.11f); // 일반적인 양수 위치
			TestBotLook(-12.34f, 56.78f, -90.12f, -15.16f); // 음수 포함
			TestBotLook(123.45f, 0, 0, 0);  // 한 축만 큰 값
			TestBotLook(0, 123.45f, 0, 45.67f); // pitch만 약간 큰 값
			TestBotLook(0, 0, 123.45f, 89.99f); // pitch 최대 근처
			TestBotLook(-327.67f, 0, 0, 0); // 최소 경계값 근처
			TestBotLook(327.67f, 0, 0, 0); // 최대 경계값 근처
			TestBotLook(0, 0, 0, -89.99f); // pitch 음수 경계
			TestBotLook(0.01f, -0.01f, 0.01f, -0.01f); // 최소 단위 정밀도 확인

			Debug.Log("Inaccuracity 테스트");
			// roll, pitch는 0~360으로 보장되어 있음.
			TestInaccuracity(0, 0, 0);
			TestInaccuracity(100, 10, 90);
			TestInaccuracity(100, 20, 180);
			TestInaccuracity(1000, 20, 360);

			Debug.Log("Vector3+Short 테스트");
			TestPosAndShort();

			Debug.Log("BotStatus 테스트");
			// vel은 -10 ~ 10 범위
			// heading은 0 ~ 360
			// pitch는 -90 ~ 90
			TestBotStatus(0, 0, -10, 10, 0, -90);
			
			TestBotStatus(0, 0, 0, 0, 0, 0);
			TestBotStatus(100, -100, -10, 10, 359, 90);
			TestBotStatus(0, 0, 0, 0, 0, -90);
			TestBotStatus(0, 0, 1, 1, 0, 0);
			TestBotStatus(0, 0, -1, -1, 0, 0);

			TestBotStatus(0, 0, 0, 0, 90, 0);
			TestBotStatus(0, 0, 0, 0, 170, 0);
			TestBotStatus(0, 0, 0, 0, 190, 0);
			TestBotStatus(0, 0, 0, 0, 260, 0);
			TestBotStatus(0, 0, 0, 0, 280, 0);
			TestBotStatus(0, 0, 0, 0, 400, 0);

			TestBotStatus(0, 0, 0, 0, -90, 0);
			TestBotStatus(0, 0, 0, 0, -170, 0);
			TestBotStatus(0, 0, 0, 0, -190, 0);
			TestBotStatus(0, 0, 0, 0, -260, 0);
			TestBotStatus(0, 0, 0, 0, -280, 0);
			TestBotStatus(0, 0, 0, 0, -400, 0);
		}

		void TestPosAndShort()
		{
			// short값만 간단히 확인
			Vector3 pos;
			short val;
			long packed = Compressor.CompressPosAndShort(Vector3.zero, short.MinValue);
			Compressor.DecompressPosAndShort(packed, out pos, out val);
			Assert.IsTrue(val == short.MinValue);

			packed = Compressor.CompressPosAndShort(Vector3.zero, short.MaxValue);
			Compressor.DecompressPosAndShort(packed, out pos, out val);
			Assert.IsTrue(val == short.MaxValue);
		}

		void TestInaccuracity(float speed, float pitch, float roll)
		{
			int packed = Compressor.CompressInaccuracity(speed, pitch, roll);
			float speed2, pitch2, roll2;
			Compressor.DecompressInaccuracity(packed, out speed2, out pitch2, out roll2);

			Assert.IsTrue(IsDiffLessThanError(speed, speed2, PositionError));
			Assert.IsTrue(IsDiffLessThanError(pitch, pitch2, ByteAngleError));
			Assert.IsTrue(IsDiffLessThanError(roll, roll2, ByteAngleError));
		}

		void TestBotLook(float x, float y, float z, float p)
		{
			long value = Compressor.CompressBotLook(new Vector3(x, y, z), p);
			Vector3 finalPos;
			float finalPitch;
			Compressor.DecompressBotLook(value, out finalPos, out finalPitch);

			Assert.IsTrue(IsDiffLessThanError(x, finalPos.x, PositionError));
			Assert.IsTrue(IsDiffLessThanError(y, finalPos.y, PositionError));
			Assert.IsTrue(IsDiffLessThanError(z, finalPos.z, PositionError));
			Assert.IsTrue(IsDiffLessThanError(p, finalPitch, PositionError));
		}

		void TestPositionXZ(float x, float z)
		{
			int xz = Compressor.CompressPosXZ(new Vector3(x, 0, z));
			Vector3 final = Compressor.DecompressPos(xz, 0);
			bool ok;
			ok = IsDiffLessThanError(x, final.x, PositionError);
			Assert.IsTrue(ok);
			ok = IsDiffLessThanError(z, final.z, PositionError);
			Assert.IsTrue(ok);
		}

		void TestEulerAngle(float origAngle)
		{
			byte packedAngle = Compressor.CompressAngle(origAngle);
			float finalAngle = Compressor.DecompressAngle(packedAngle);
			bool ok = IsAngleDiffLessThanError(origAngle, finalAngle, ByteAngleError);
			Assert.IsTrue(ok);
		}

		void TestBotStatus(float x, float z, float xVel, float zVel, float heading, float pitch)
		{
			long val = Compressor.CompressBotStatus(new Vector3(x, 0, z), new Vector3(xVel, 0, zVel), heading, pitch);
			Vector3 pos, vel;
			float heading_, pitch_;
			Compressor.DecompressBotStatus(val, out pos, out vel, out heading_, out pitch_);
			Assert.IsTrue(IsDiffLessThanError(x, pos.x, PositionError));
			Assert.IsTrue(IsDiffLessThanError(z, pos.z, PositionError));
			Assert.IsTrue(IsDiffLessThanError(xVel, vel.x, BotVelError));
			Assert.IsTrue(IsDiffLessThanError(zVel, vel.z, BotVelError));
			Assert.IsTrue(IsAngleDiffLessThanError(heading, heading_, ByteAngleError));
			Assert.IsTrue(IsDiffLessThanError(pitch, pitch_, 2f));

		}

		bool IsDiffLessThanError(float orig, float final, float error)
		{
			Debug.Log($"orig:{orig} final:{final} ");
			return Mathf.Abs(orig - final) < error;
		}
		bool IsAngleDiffLessThanError(float orig, float final, float error)
		{
			float angleDiff = Mathf.DeltaAngle(orig, final);
			Debug.Log($"Angle orig:{orig} final:{final} diff:{angleDiff}");
			//return Mathf.Abs(expected - final) < error 
			return angleDiff < error;
			
		}

	}
}
