using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Framework;

#if USE_FIREBASE
using Firebase.Crashlytics;
#endif

namespace Framework
{
	public static class CrashlyticsUtil
	{
		// CustomKey 분할 정보.
		static Dictionary<string, int> _customKeySplitInfos = new Dictionary<string, int>();
		
		static HashSet<int> _manualExceptionDupChecker = new();

		// 'CustomKey' 페이지에서 사용할 키 값의 Prefix문자열. (숫자는 정렬을 위함)
		public const string KeyPrefix_Version = "1.VERSION";
		public const string KeyPrefix_UserInfo = "2.USERINFO";
		public const string KeyPrefix_GameStatus = "3.GAMESTATUS";
		public const string KeyPrefix_LastRequest = "4.LASTREQ";
		public const string KeyPrefix_LastResponse = "5.LASTACK";

		/// <summary>
		/// Crashlytics에서는 UserId로 필터링하는 기능이 있다.
		/// </summary>
		/// <param name="userId"></param>
		public static void SetUserId(string userId)
		{
#if USE_FIREBASE
			Crashlytics.SetUserId(userId);
#endif
		}

		/// <summary>
		/// 예외발생시 도움이 될만한 액션로그(클릭, UI열림, 프로토콜 이름 등)를 남긴다.
		/// 참고: Crashlytics log의 전체 크기는 64KB로 제한된다.
		/// </summary>
		public static void AddActionLog(string msg)
		{
			if(!FirebaseUtil.IsFirebaseInit) { return; }
#if USE_FIREBASE
			Crashlytics.Log(msg);
#endif
		}
		
		/// <summary>
		/// Crashlytics 대시보드의 'KEY'탭에 출력할 정보들.
		/// 참고: 64개의 Key/Value 까지만 넣을 수 있다. 각 Key/Value는 1K제한.
		/// </summary>
		/// <param name="keyPrefix">대시보드에서 출력순서 조정용</param>
		public static void SetCustomKey(string key, string value, string keyPrefix = null)
		{
			if (!FirebaseUtil.IsFirebaseInit) { return; }
#if USE_FIREBASE
			// 예외가 발생했으면 CustomKey는 기록을 중단한다. (계속 기록하는 경우, 예외랑 따로 놀아서 y분석이 힘듦) 
			//if (ExceptionOccurred)
			//	return;

			if (keyPrefix != null)
			{
				key = $"[{keyPrefix}] {key}";
			}
			SetCustomKeyWithSplitting(key, value);
#endif
		}

		/// <summary>
		/// 코드에서 직접 호출하여 오류 남기기. Crashlytics엔 '심각하지 않은 오류'로 보고된다.
		/// 참고: 이 호출을 하면 Crashlytics가 자체 캐치한 예외와 동등하게 또 하나의 크래시 리포트가 생성되는 것 같다. 복수개의 리포트들은 마지막 8개만 유효하다.
		/// </summary>
		public static void LogException(System.Exception exception)
		{
			DebugEx.Log("LogException: " + exception.ToString());

			// 중복 제거
			int crc = exception.Message.GetCrcHash();
			if (_manualExceptionDupChecker.Contains(crc))
			{ return; }
			_manualExceptionDupChecker.Add(crc);
			
			AddActionLog("[ManualException] " + exception.ToString());
#if USE_FIREBASE
			if (!FirebaseUtil.IsFirebaseInit) { return; }
			Crashlytics.LogException(exception);
#endif
		}

#if USE_FIREBASE
		static void SetCustomKeyWithSplitting(string key, string value)
		{
			const int KEY_MAX_LENTH = 50;
			const int VAL_MAX_LENGTH = 1000 - KEY_MAX_LENTH;
			const int MAX_SPLITS = 40;

			RemovePrevStoredValues(key);

			if (value.Length <= VAL_MAX_LENGTH)
			{

				Crashlytics.SetCustomKey(key, value);
				return;
			}

			// 너무 긴 경우 1K씩 분할하여 넣는다.
			int pos = 0;
			int counter = 0;
			for (; (pos + VAL_MAX_LENGTH) < value.Length; pos += VAL_MAX_LENGTH)
			{
				Crashlytics.SetCustomKey(counter == 0 ? key : $"{key}({counter:00})", value.Substring(pos, VAL_MAX_LENGTH));
				counter++;
				if (counter == MAX_SPLITS - 1)
					break;
			}
			Crashlytics.SetCustomKey($"{key}({counter:00})", value.Substring(pos));
			counter++;
			_customKeySplitInfos[key] = counter;
		}

		static void RemovePrevStoredValues(string key)
		{
			// 기존에 분할 저장했던 내용은 덮어쓰는 방식으로 삭제.
			int splitCount;
			if (_customKeySplitInfos.TryGetValue(key, out splitCount))
			{
				for (int i = 0; i < splitCount; i++)
				{
					if (i > 0) Crashlytics.SetCustomKey($"{key}({i:00})", string.Empty);
				}
				_customKeySplitInfos[key] = 0;
			}
		}

#endif
	}

}