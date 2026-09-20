카드 스킬 — 스킬 블록 배치 컨트롤러
====================================
> 카드마다 고유한 스킬이 있고, 스킬을 쓰면 판 위의 일반 블록 하나가 **카드 스킬 블록 / 로켓 / 폭탄 / 미러볼 / 종이비행기** 같은 특수 블록으로 바뀐다.
> `CardSkillController` 는 그 "배치" 단계를 맡는다. 발동 트리거(카드 클릭, 스코어 모드 자동 발동, 이어하기 보상, 부스터 아이템)가 달라도 **셀 선택 → 기존 블록 폭파 → 특수 블록 생성** 의 한 골격을 공유한다.

샘플 코드: [`CardSkillController.cs`](./CardSkillController.cs)

| 메서드 | 트리거 | 생성하는 것 |
|--------|--------|-------------|
| `OnClickCardSkill(...)` (전체 인자) | 사용자가 카드를 탭 | 카드 스킬 블록 (카드 발사 이펙트, 글로우, 등급 정보 포함) |
| `OnClickCardSkill(int)` / `OnClickCardSkillForAutoplay(int)` | 코드 경로(호출부는 이 샘플에 없음) | 카드 스킬 블록 (이펙트 정보 없음, 피버 게이지 초기화는 후자만) |
| `OnClickBoosterItem(index)` | 상점 부스터 아이템 사용 | 로켓 / 폭탄 / 미러볼 중 하나 |
| `OnClickContinue(retryCount)` | 실패 후 이어하기 | 재시도 횟수별 턴 추가 + 특수 블록 (1회차 턴만, 2회차 로켓, 3회차 종이비행기·폭탄·미러볼) |

* 상세 설명, 코드 발췌, 발견한 문제점(이어하기 셀 배정 버그, 호출부 없는 `TailEffectTask`, 부분적으로만 채워진 `args`)은 **[CardSkillBlockLogic.md](../CardSkillBlockLogic.md)** 에 있다.
* 배치된 스킬 블록이 실제로 터지는 곳은 `StageController` 의 `case BlockType.CardSkill` 분기다(같은 문서의 "② 배치된 스킬 블록이 터지는 곳" 참고).
* 블록 폭파·풀 반납 흐름은 [BlockMatchLogic.md](../BlockMatchLogic.md), 판 초기화는 [PuzzleInit.md](../PuzzleInit.md).
