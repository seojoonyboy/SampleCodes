튜토리얼 — 시트 기반 튜토리얼 엔진
==================================
> 튜토리얼 한 스텝을 데이터 시트의 한 행(`TutorialCommon`)으로 정의하고, 실행 엔진(`TutorialManager`)은 같은 `TutorialType` 의 행들을 `Code` 순으로 소비하며 화면 하이라이트, 대사 타이핑, 클릭/시간 대기를 처리한다.
> 스텝 추가·재배치가 코드 수정이 아니라 시트 편집으로 끝나도록 "정의(시트)"와 "진행(엔진)"을 분리한 구조다.

샘플 코드: [`TutorialManager.cs`](./TutorialManager.cs)

* 시트 구조, 메인 루프(`Run`), UniTask/LINQ 사용 방식, 취소 처리, 알려진 한계: **[TutorialPattern.md](../TutorialPattern.md)**
