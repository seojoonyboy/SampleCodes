using System;
using Snowballs.Util;
using UnityEngine;

namespace Snowballs.Network.Dto
{
	/// <summary>
	/// <para>갱신 종류</para>
	/// <para>(유저 정보, 스테이지 정보, 구독, 카운팅, 미확인,</para>
	/// <para>카드, 사용 아이템, 활성화 아이템, 프로필,</para>
	/// <para>일일 이벤트, 수집 이벤트, 패스, 팝업 스토어)</para>
	/// </summary>
	[Serializable]
	public enum InvokeKind	{
		/// <summary>PLAYER: 0</summary>
		PLAYER = 0,
		/// <summary>STAGE: 1</summary>
		STAGE = 1,
		/// <summary>SUBSCRIPTION: 2</summary>
		SUBSCRIPTION = 2,
		/// <summary>COUNTING: 3</summary>
		COUNTING = 3,
		/// <summary>UNCONFIRMED: 4</summary>
		UNCONFIRMED = 4,
		/// <summary>MAILBOX: 5</summary>
		MAILBOX = 5,
		/// <summary>CARD: 10</summary>
		CARD = 10,
		/// <summary>USABLE_ITEM: 11</summary>
		USABLE_ITEM = 11,
		/// <summary>ACTIVE_ITEM: 12</summary>
		ACTIVE_ITEM = 12,
		/// <summary>PROFILE: 13</summary>
		PROFILE = 13,
		/// <summary>DAILY_EVENT: 20</summary>
		DAILY_EVENT = 20,
		/// <summary>MISSION: 21</summary>
		MISSION = 21,
		/// <summary>PASS: 22</summary>
		PASS = 22,
		/// <summary>POPUP_STORE: 23</summary>
		POPUP_STORE = 23,
		/// <summary>CP: 999</summary>
		CP = 999,
	}
}
