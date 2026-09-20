using System;
using Snowballs.Util;
using UnityEngine;

namespace Snowballs.Network.Dto
{
	/// <summary>
	/// InvokeDto
	/// </summary>
	[Serializable]
	public class InvokeDto
	{
		/// <summary>갱신 종류         (유저 정보, 스테이지 정보, 구독, 카운팅, 미확인,        카드, 사용 아이템, 활성화 아이템, 프로필,        일일 이벤트, 수집 이벤트, 패스, 팝업 스토어)</summary>
		public InvokeKind kind;
		/// <summary>리소스</summary>
		public String resource;
		/// <summary>참조 값</summary>
		public Int32 value;
		/// <summary>
		/// InvokeDto
		/// </summary>
		/// <param name="kind">갱신 종류         (유저 정보, 스테이지 정보, 구독, 카운팅, 미확인,        카드, 사용 아이템, 활성화 아이템, 프로필,        일일 이벤트, 수집 이벤트, 패스, 팝업 스토어)</param>
		/// <param name="resource">리소스</param>
		/// <param name="value">참조 값</param>
		public InvokeDto(InvokeKind kind, String resource, Int32 value)
		{
			this.kind = kind;
			this.resource = resource;
			this.value = value;
		}
	}
}
