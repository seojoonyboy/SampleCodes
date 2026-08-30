#define USE_INACCURACITY_PITCH_COMPRESSION_WITH_HIGH_QUALITY

using Framework;
using System;
using UnityEngine;

namespace Game.View.BattleSystem
{
	/// <summary>
	/// 플레이어 이동 동기화 데이터 압축
	/// </summary>
	public class NetworkDataCompressor
	{
		/// <summary>
		/// Player Position을 정밀도 1cm로 압축. 평지에서 Y값은 거의 안변하니 별도로 분리.
		/// </summary>
		public static int CompressPosXZ(Vector3 v)
		{
			uint packed = 0;
			packed |= (uint)(ushort)(v.x * 100);
			packed |= (uint)(ushort)(v.z * 100) << 16;
			return unchecked((int)packed);
		}

		/// <summary>
		/// Player Position을 정밀도 1cm로 압축. 평지에서 Y값은 거의 안변하니 별도로 분리.
		/// </summary>
		public static short CompressPosY(Vector3 v)
		{
			return (short)(v.y * 100);
		}

		/// <summary>
		/// Player Position 복원. 
		/// </summary>
		public static Vector3 DecompressPos(int xz, short y)
		{
			Vector3 pos = Vector3.zero;
			pos.x = (short)(xz & 0xffff) / 100f;
			pos.z = (short)(xz >> 16) / 100f;
			
			pos.y = y / 100f;
			return pos;
		}

		/// <summary>
		/// Player Velocity 압축. 평지에서 Y값은 거의 안변하니 별도로 분리.
		/// </summary>
		public static int CompressVelocityXZ(Vector3 v)
		{
			return CompressPosXZ(v);
		}
		/// <summary>
		/// Player Velocity 압축. 평지에서 Y값은 거의 안변하니 별도로 분리.
		/// </summary>
		public static short CompressVelocityY(Vector3 v)
		{
			return CompressPosY(v);
		}
		/// <summary>
		/// Player Velocity 복원.
		/// </summary>
		public static Vector3 DecompressVelocity(int xz, short y)
		{
			return DecompressPos(xz, y);
		}

		const float DegreeToByte = 255f / 360f;
		const float ByteToDegree = 360f / 255f;

		const float PitchDegreeToByte = 20 * 255f / 360f;
		const float ByteToPitchDegree = 360f / 255f / 20f;

		/// <summary>
		/// 각도(-180 ~ +180)를 0-255 범위의 byte로 압축.
		/// </summary>
		public static byte CompressAngle(float angle)
		{
			// 각도 범위를 0-360으로 정규화
			// -180 ~ 180 -> 0 ~ 360
			float normalizedAngle = (angle + 180.0f);

			// 0-360 범위의 float를 0-255 범위의 byte로 변환
			return (byte)(normalizedAngle * DegreeToByte);
		}

		/// <summary>
		/// 압축된 바이트 각도를 -180 ~ 180 범위로 복원
		/// </summary>
		public static float DecompressAngle(byte compressedAngle)
		{
			// 0-255 범위의 바이트를 0-360 범위의 float로 변환
			float normalizedAngle = (float)compressedAngle * ByteToDegree;
			// 0-360 범위의 각도를 -180 ~ 180 범위로 다시 변환
			return normalizedAngle - 180.0f;
		}
		
		/*--
		/// <summary>
		/// 플레이어 방향을 yaw만 short로 압축. 
		/// </summary>
		public static short CompressYaw(Quaternion rot)
		{
			float yaw = rot.eulerAngles.y;
			return (short)(yaw / 360f * 30000f);
		}

		public static Quaternion DecompressYaw(short data)
		{
			float yaw = data / 30000f * 360f;
			return Quaternion.Euler(0, yaw, 0);
		}
		--*/

		/// <summary>
		/// 플레이어 상태값 3종을 byte로 압축
		/// </summary>
		public static byte CompressStates(PlayerState playerState, PlayerFPState fpsState, bool isGrounded)
		{
			byte packed = 0;
			packed |= (byte)((byte)playerState & 0x0F); // bits 0–3
			packed |= (byte)(((byte)fpsState & 0x07) << 4); // bits 4–6
			packed |= (byte)((isGrounded ? 1 : 0) << 7); // bit 7
			return packed;
		}

		/// <summary>
		/// 플레이어 상태값 3종을 복원.
		/// </summary>
		public static void DecompressStates(byte packed, out PlayerState playerState, out PlayerFPState playerFPSState, out bool isGrounded)
		{
			playerState = (PlayerState)(packed & 0x0F); // bits 0–3
			playerFPSState = (PlayerFPState)((packed >> 4) & 0x07); // bits 4–6
			isGrounded = ((packed >> 7) & 0x01) != 0; // bit 7
		}

		public static long CompressBotStatus(Vector3 pos, Vector3 vel, float rot, float lookPitch)
		{
			ulong packed = 0;
			// 위치는 0.01m 해상도
			packed |= (ulong)(ushort)(pos.x * 100);
			packed |= (ulong)(ushort)(pos.z * 100) << 16;

			// 속도는 0.1 (m/s) 해상도로 1바이트로 압축

			var velX = (sbyte)Math.Clamp(Mathf.RoundToInt(vel.x * 10), -127, 127);
			var velZ = (sbyte)Math.Clamp(Mathf.RoundToInt(vel.z * 10), -127, 127);
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

		public static void DecompressBotStatus(long packedLong, out Vector3 pos, out Vector3 vel, out float rot, out float lookPitch)
		{
			ulong packed = unchecked((ulong)packedLong);

			Vector3 posXZ = Vector3.zero;
			posXZ.x = ((short)(packed & 0xffff)) / 100f;
			posXZ.z = ((short)((packed >> 16) & 0xffff)) / 100f;
			pos = posXZ;

			Vector3 velXZ = Vector3.zero;
			velXZ.x = ((sbyte)((packed >> 32) & 0xff)) / 10f;
			velXZ.z = ((sbyte)((packed >> 40) & 0xff)) / 10f;
			vel = velXZ;

			rot = DecompressAngle((byte)((packed >> 48) & 0xff));
			
			lookPitch = (sbyte)((packed >> 56) & 0xff);
		}

		//public static int CompressBotLook2(Vector3 pos)
		//{

		//}

		/// <summary>
		/// 봇 lookPos + lookPitch 압축
		/// </summary>
		public static long CompressBotLook(Vector3 pos, float pitch)
		{
			float sx = pos.x * 100f;
			float sy = pos.y * 100f;
			float sz = pos.z * 100f;
			float sp = pitch * 100f;
			short ix = (short)Mathf.RoundToInt(sx);
			short iy = (short)Mathf.RoundToInt(sy);
			short iz = (short)Mathf.RoundToInt(sz);
			short ip = (short)Mathf.RoundToInt(sp);
			ulong packed = 0;
			packed |= (ulong)(ushort)ix;
			packed |= (ulong)(ushort)iy << 16;
			packed |= (ulong)(ushort)iz << 32;
			packed |= (ulong)(ushort)ip << 48;
			return unchecked((long)packed);
		}

		/// <summary>
		/// 봇 LookPos + LookPitch 복원
		/// </summary>
		public static void DecompressBotLook(long packedLong, out Vector3 pos, out float pitch)
		{
			ulong packed = unchecked((ulong)packedLong);

			// 각각 16비트 추출
			short ix = (short)(packed & 0xFFFFu);
			short iy = (short)((packed >> 16) & 0xFFFFu);
			short iz = (short)((packed >> 32) & 0xFFFFu);
			short ip = (short)((packed >> 48) & 0xFFFFu);

			// 원래 스케일로 되돌림 (0.01 정밀도)
			pos = new Vector3(ix / 100f, iy / 100f, iz / 100f);
			pitch = ip / 100f;
		}

		/// <summary>
		/// 총탄 Inaccuracy 압축
		/// </summary>
		public static int CompressInaccuracity(float bulletSpeed, float pitch, float roll)
		{
			uint packed = 0;

			// pitch, roll은 0~360로 보장되어 있기 떄문에 정규화 필요없음.

			byte pitch8 = 0;

			// pitch는 거의 0~15도 사이의 작은 값이기 때문에 손실을 최소화하기 위해 더 큰 변환비를 사용한다.
#if UNITY_EDITOR
			if (pitch * PitchDegreeToByte > 255)
			{
				// Pitch값은 20보다 클 수 없음.
				DebugEx.Warning("CompressInaccuracity() Too big pitch: " + pitch);
			}
#endif
			pitch8 = (byte)Math.Min(pitch * PitchDegreeToByte, 255);
			
			byte roll8 = (byte)(roll * DegreeToByte);
			packed |= ((uint)(ushort)bulletSpeed) & 0xFFFF;
			packed |= (uint)(pitch8) << 16;
			packed |= (uint)(roll8) << 24;
			return unchecked((int)packed);
		}

		/// <summary>
		/// 총탄 Inaccuracy 복원.
		/// </summary>
		public static void DecompressInaccuracity(int packed, out float bulletSpeed, out float pitch, out float roll)
		{
			uint packedU = unchecked((uint)packed);

			bulletSpeed = (packedU & 0x7FFF);
			var pitch8 = (byte)((packedU >> 16) & 0xFF);
			var roll8 = (byte)(packedU >> 24);

			pitch = pitch8 * ByteToPitchDegree;
			roll = roll8 * ByteToDegree;
		}

		/// <summary>
		/// Position + short 압축
		/// </summary>
		public static long CompressPosAndShort(Vector3 pos, short val)
		{
			float ix = Mathf.RoundToInt(pos.x * 100f);
			float iy = Mathf.RoundToInt(pos.y * 100f);
			float iz = Mathf.RoundToInt(pos.z * 100f);
			ulong packed = 0;
			packed |= (ulong)(ushort)ix;
			packed |= (ulong)(ushort)iy << 16;
			packed |= (ulong)(ushort)iz << 32;
			packed |= (ulong)(ushort)val << 48;
			return unchecked((long)packed);
		}

		/// <summary>
		/// Position + short 복원
		/// </summary>
		public static void DecompressPosAndShort(long packedLong, out Vector3 pos, out short val)
		{
			ulong packed = unchecked((ulong)packedLong);

			// 각각 16비트 추출
			short ix = (short)(packed & 0xFFFFu);
			short iy = (short)((packed >> 16) & 0xFFFFu);
			short iz = (short)((packed >> 32) & 0xFFFFu);
			short ip = (short)((packed >> 48) & 0xFFFFu);

			// 원래 스케일로 되돌림 (0.01 정밀도)
			pos = new Vector3(ix / 100f, iy / 100f, iz / 100f);
			val = ip;
		}
	}
}