## 사격 연습장(Practice) 모드

PVP 대전 위주 콘텐츠를 보완하기 위해 마련한 싱글플레이 전용 콘텐츠 중 하나로, 연습장에서 레벨 단위의 훈련을 수행하는 경우 레벨별로 주어지는 탄알, 제한시간, 과녁의 움직이는 패턴 등을 PracticeMode 테이블과 연동하여 처리하였다.

아래는 PracticeMode 테이블 구조 일부이다.

<img width="1672" height="467" alt="image" src="https://github.com/user-attachments/assets/d40a9ae9-f985-4989-a205-8cbb630ac7cf" />

Mark1 : 고유 식별 번호
Mark1G : 과녁의 그룹 번호 [이 그룹이 모두 총알에 맞아야 다음 그룹으로 넘어간다.]
Mark1P : 과녁이 어떻게 움직일지 Pattern에 대한 참조키

### 과녁 그룹 순차 처리 (`TrainingMarkManager.cs`)

`TRAINING_MODE` 진입 시 실제 과녁 활성화는 `TrainingMarkManager`가 테이블의 `Mark1G`를 참조해 처리한다. 이전 그룹이 모두 처리되어야만 다음 그룹이 활성화되도록 런타임에 종속 관계를 구성한다.

```csharp
if (afterPrevGroup && (currentGroupId > 1))
{
	tradingMarkParams.PrevGroupMarks = GetTrainingMarksByGroupID(currentGroupId - 1);
}

trainingMark.InitContent(tradingMarkParams, false);
```

### 설계 포인트

과녁의 위치·그룹·움직임 패턴을 코드가 아닌 테이블에서 읽어오게 한 이유는, 레벨 밸런싱이 반복적으로 조정되는 콘텐츠였기 때문이다. `Mark1G` 값 하나로 순서 종속(순차 클리어형)과 독립(동시 클리어형) 스테이지를 모두 표현할 수 있어, 새 레벨을 추가할 때 코드 수정 없이 시트 값만 바꾸면 된다.

관련 코드: [03. Tutorial](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/03.%20Tutorial)
