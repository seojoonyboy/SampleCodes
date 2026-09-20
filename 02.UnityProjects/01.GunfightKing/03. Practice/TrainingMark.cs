using DG.Tweening;
using Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.View.BattleSystem
{
	public class TrainingMark : MonoBehaviour
	{
		[SerializeField] bl_TrainingMarkHitHandler _hitHandler;
		
		[SerializeField] bool _canMoveVertical = false;
		[SerializeField] bool _canMoveHorizontal = false;

		[SerializeField] Transform _body;
		[SerializeField] Animator _animator;

		public bool IsFinished = false;		//맞거나 시간초과가 되어 끝났는가?
		
		float _moveSpeed;
		
		int _moveForward;
		int _moveBackward;
		int _moveRight;
		int _moveLeft;

		float _activeTime;
		float _deactiveTime;
		
		Coroutine _coroutine;
		Coroutine _autoActiveCoroutine;
		
		bool _isFreeMode = false;
		
		List<TrainingMark> _prevGroupMarks;

		Action<bool> _onHitCallback;

		Vector3 _originPos;
		
		public void InitContent(TradingMarkParams param, bool isFreeMode = false)
		{
			IsFinished = false;
			
			_originPos = transform.position;
			_isFreeMode = isFreeMode;
			
			_canMoveHorizontal = (param.MoveLeft > 0) || (param.MoveRight > 0);
			_canMoveVertical = (param.MoveForward > 0) || (param.MoveBackward > 0);
			
			_moveBackward = param.MoveBackward;
			_moveForward = param.MoveForward;
			_moveLeft = param.MoveLeft;
			_moveRight = param.MoveRight;
			
			_moveSpeed = param.MoveSpeed;

			_activeTime = param.ActiveTime;
			_deactiveTime = param.DeactiveTime;

			_prevGroupMarks = param.PrevGroupMarks;
			
			this.SetActiveGo(true);
			transform.DOKill();
			
			if(_coroutine != null) StopCoroutine(_coroutine);
			_coroutine = StartCoroutine(InitTweenMove());

			_hitHandler.IsFreeMode = _isFreeMode;
			_hitHandler.AddOnHitCallback(OnHitCollider);
			
			_onHitCallback = param.OnHit;
		}

		void OnHitCollider()
		{
			PlayAnimation("Deactivate");
			_hitHandler.ToggleCollider(false);
			_body.tag = "Untagged";
			
			if (_isFreeMode)
			{
				if (_autoActiveCoroutine == null)
				{
					_autoActiveCoroutine = StartCoroutine(AutoActiveAfterSec(3.0f));
				}
			}
			else
			{
				_onHitCallback?.Invoke(true);
				IsFinished = true;
			}
			
			OnDisable();
		}

		IEnumerator AutoActiveAfterSec(float afterSec)
		{
			yield return new WaitForSeconds(afterSec);
			
			PlayAnimation("Activate");
			_hitHandler.ToggleCollider(true);
			_body.tag = bl_MFPS.AI_TAG;

			_autoActiveCoroutine = null;
		}

		void PlayAnimation(string animName)
		{
			_animator.Play(animName);
		}

		Vector3 GetLeftEnd()
		{
			return _originPos - transform.right.normalized * _moveLeft / 2.0f;
		}

		Vector3 GetRightEnd()
		{
			return _originPos + transform.right.normalized * _moveRight / 2.0f;
		}

		Vector3 GetForwardEnd()
		{
			return _originPos - transform.forward.normalized * _moveForward / 2.0f;
		}

		Vector3 GetBackEnd()
		{
			return _originPos + transform.forward.normalized * _moveBackward / 2.0f;
		}
		
		IEnumerator InitTweenMove()
		{
			if (_prevGroupMarks != null && _prevGroupMarks.Count > 0)
			{
				yield return new WaitUntil(() => _prevGroupMarks.TrueForAll(mark => mark.IsFinished));
			}
			
			yield return new WaitForSeconds(_activeTime);
			PlayAnimation("Activate");
			
			_hitHandler.ToggleCollider(true);
			_body.tag = bl_MFPS.AI_TAG;
		
			//Note. 앞뒤 이동과 좌우 이동을 동시에 할 때 움직임이 애매하기 때문에 앞뒤 이동과 좌우 이동을 동시에 하지 않는다. 
			//만약, 앞뒤 이동이 활성화 되어 있으면 좌우 이동 여부는 무시한다.
			if (_canMoveVertical)
			{
				Vector3 targetPos = _moveForward > 0 ? GetForwardEnd() : GetBackEnd();
				transform
					.DOMove(targetPos, _moveSpeed)
					.SetLoops(-1, LoopType.Yoyo)
					.SetEase(Ease.Linear);
			}
			else if (_canMoveHorizontal)
			{
				Vector3 targetPos = _moveLeft > 0 ? GetLeftEnd() : GetRightEnd();
				transform
					.DOMove(targetPos, _moveSpeed)
					.SetLoops(-1, LoopType.Yoyo)
					.SetEase(Ease.Linear);
			}
			
			if (_deactiveTime > 0)
			{
				yield return new WaitForSeconds(_deactiveTime);
				
				PlayAnimation("Deactivate");
				
				IsFinished = true;
				_onHitCallback?.Invoke(false);
				
				OnDisable();
			}
		}

		private void OnDisable()
		{
			transform.DOKill();
			if(_coroutine != null) StopCoroutine(_coroutine);

			if (!_isFreeMode)
			{
				_hitHandler.RemoveOnHitCallback(OnHitCollider);
				_onHitCallback = null;
			}
		}

		public class TradingMarkParams
		{
			public int ID;
			
			public int MoveForward;
			public int MoveBackward;

			public int MoveRight;
			public int MoveLeft;

			public float MoveSpeed;

			public float ActiveTime;
			public float DeactiveTime;

			//이전 그룹 Mark들
			public List<TrainingMark> PrevGroupMarks = new List<TrainingMark>();

			public Action<bool> OnHit;	//과녁이 맞았을 때 Callback
		}
	}
}