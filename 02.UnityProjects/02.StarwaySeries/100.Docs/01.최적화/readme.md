### 코드 최적화

3-매치 퍼즐은 한 턴만 진행돼도 여러 블록이 동시에 터지고, 빈 자리를 채우기 위해 새 블록이 다시 생성되는 구조다. 즉 `Instantiate`/`Destroy`가 턴마다 반복적으로 발생하는 장르라서, 그대로 두면 GC 할당과 프레임 드랍의 전형적인 원인이 된다. 여기서는 이 문제를 두 방향에서 다뤘다: (1) 블록/이펙트를 매번 생성·파괴하지 않도록 오브젝트 풀링을 도입했고, (2) 매치-중력 처리처럼 한 스테이지 안에서 여러 번 반복 실행되는 절차형 로직을 Coroutine에서 UniTask(async/await)로 전환했다.

#### Object Pooling — `IngameBlockPoolController`

일반 블록과 특수 블록 생성/파괴 이펙트를 각각 별도 풀로 관리한다. 일반 블록은 `List<GameObject>` 하나로, 특수 블록(운석/로켓/토성/블랙홀/스타레이 등 5종 × 생성·파괴 프리팹)은 키(블록 종류 문자열)별 `List<GameObject>`를 담은 `Dictionary<string, List<GameObject>>`로 관리한다.

```csharp
private static Dictionary<string, List<GameObject>> specialBlockPool;
private static List<GameObject> normalBlockPool;

public static GameObject SpawnBlock()
{
    if (normalBlockPool == null)
        normalBlockPool = new List<GameObject>();

    var targetItem = normalBlockPool.Find(x => !x.activeSelf);
    if (targetItem != null)
    {
        targetItem.SetActive(true);
        return targetItem;
    }

    GameObject targetPrefab = Instance.blockPrefab;
    GameObject newObj = Instantiate(targetPrefab, Instance.transform);
    normalBlockPool.Add(newObj);

    return newObj;
}
```

동작 방식은 "비활성 오브젝트를 먼저 찾고, 없을 때만 `Instantiate`로 풀을 늘린다"는 전형적인 풀링 패턴이다. `InitPool(poolSize, specialBlockPoolSize)`가 스테이지 진입 시 일반 블록 풀·특수 블록 생성 풀·특수 블록 파괴 이펙트 풀을 미리 워밍업해두고, 회수 시에는 `ReleaseNormalBlock` / `ReleaseExplodeSpecialBlock`이 하위 오브젝트를 정리한 뒤 `Destroy` 대신 부모를 풀 컨테이너로 재설정하고 `SetActive(false)`로 되돌려놓는다. `BlockController.Explode()`의 finally 블록에서도 `Destroy(gameObject)`가 아니라 `IngameBlockPoolController.ReleaseNormalBlock(this.gameObject)`을 호출해 이 풀로 반납하는 것을 볼 수 있다.

이 작업은 2023년 7월 18일 하루 오후에 이뤄진 작업이었고, 실제 커밋 이력을 보면 설계 → 실사용 검증 → 버그 수정 → 일부 설계 재조정까지 한 번에 이어진 흐름이 남아있다.

- **16:06 "인게임 최적화 1차 작업 완료"** — `IngameBlockPoolController.cs`를 새로 작성(+409줄)하면서, 이펙트마다 따로 있던 기존 프리팹 로더 클래스를 통째로 삭제하고 더 이상 쓰지 않는 대형 이펙트 프리팹도 함께 정리했다. 풀링을 새로 들여오면서 죽은 코드/리소스를 걷어낸, 순증감이 오히려 마이너스에 가까운 리팩터링이었다.
- **17:14 (1차 작업으로부터 1시간 8분 후) "PaperPlane Move 이펙트가 정상적인 부모 하위로 반납되지 않는 문제 수정"** — 같은 날 실제 플레이 테스트 중 발견된 반납(Release) 로직의 버그를 바로 잡았다.
- **18:23 (1차 작업으로부터 2시간 17분 후) "특수블록 Pool 이용하는 방식 취소함"** — 특정 특수 블록 한 종류에 대해서는 풀링 방식이 맞지 않다고 판단해 그 부분만 설계를 되돌렸다.

세 커밋 모두 같은 날, 같은 파일에서 두 시간 남짓 사이에 벌어진 일이다. 처음부터 완벽한 설계였다기보다는 붙여보고 → 검증하고 → 고치고 → 안 맞는 부분은 되돌린 기록에 가깝다.

#### Coroutine → UniTask 전환

`StageController`의 매치/중력 처리(`MatchAndGravity`)를 비롯한 절차형 로직은 `async UniTask`로 작성되어 있다.

```csharp
private UniTask? coMatchAndGravity = null;
...
public async UniTask MatchAndGravity()
{
    ...
    await UniTask.WaitUntil(() => 0 == BlockController.gravityCount);
    ...
}
```

`LoadStage`에서도 `StartCoroutine` 없이 `this.coMatchAndGravity = this.MatchAndGravity();`로 바로 실행을 시작한다. 같은 파일 안에 `#if UNDO_MANAGER` 블록으로 남아있는 `StartCoroutine(this.MatchAndGravity())` 같은 옛 호출부는, 이 로직이 원래 코루틴 기반이었다가 UniTask로 옮겨온 흔적이기도 하다. 이 전환으로 메모리 사용량이 대략 10~30% 줄었고, CS(고객센터)로 접수되던 "프레임 멈춤" 관련 문의도 월 5건 안팎에서 월 1건 미만으로 줄어드는 효과가 있었다.

[관련 코드 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/07.BlockControl/StageController.cs) (Coroutine → UniTask)   
[관련 코드 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/07.BlockControl/IngameBlockPoolController.cs) (Object Pooling)   


### 그래픽 최적화
그래픽팀과 논의한 그래픽 리소스 최적화 메뉴얼 내용 일부

Max Particles 수 최대한 적게
•	60 ~ 100 적정 수준 [최대 1000]   
 ![image](https://github.com/user-attachments/assets/4b041825-a77f-4865-ae73-072858f064d2)

Cast Shadows off 처리   
![image](https://github.com/user-attachments/assets/8e367ead-5a8f-4b0f-898e-b9b393543fe2)

![image](https://github.com/user-attachments/assets/d51be65c-1920-46fe-bc08-4e556cec1f50)

Sprite Packer를 통한 Texture 그룹별 압축   
![image](https://github.com/user-attachments/assets/690d931e-c971-4e85-946f-f212b3cb3862)

압축이 불가능한 Texture의 경우 별도로 ASTC 압축   
![image](https://github.com/user-attachments/assets/0f8e2f97-6e0e-4fcc-8407-7f3e324f7f76)
![image](https://github.com/user-attachments/assets/c4c346aa-a750-44ff-963c-c65a08f61f8f)


Memory Profiler와 

관련 코드: [BlockMatchLogic.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/BlockMatchLogic.md) · [07.BlockControl 폴더](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/07.BlockControl)
