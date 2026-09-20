## Firebase Crashlytics 연동 및 커스텀 로그 (Firebase)

Firebase Crashlytics 기능을 활용하여, Client App에서 발생하는 Exception이나 의도하지 않은 예외 상황을 Firebase에 전달해 로그를 백업하고, Live 서비스 중인 App의 Crash 상황 전후를 파악하기 쉽게 하여 안정성 향상을 도모하였다.

<img width="1419" height="599" alt="image" src="https://github.com/user-attachments/assets/e445aa40-c578-4788-886a-c25889c57a70" />

* 기존 비정상 종료가 발생하지 않은 유저가 기존에 하루 평균 최저 88%의 안정화를 보였으나 이후, 관련 작업을 통한 모니터링과 효율적인 수정 대응으로 최고 99% 안정화를 도출함 (Crashlytics 대시보드 기준 포트폴리오 기재 수치이며 이 저장소로는 검증되지 않는다)

<img width="1288" height="462" alt="image" src="https://github.com/user-attachments/assets/06d4e492-6e64-46f9-b621-78311c6d99f8" />

* 실제 Firebase에게 Crashlytics가 발생하는 시점에 커스텀 로그를 추가하는 작업에 대한 예시

### 배틀 이벤트에 브레드크럼 남기기 (`DemolitionBombManager.cs`)

폭탄 모드에서 폭탄 설치가 일어났을 때, Crashlytics에 CustomLog를 쌓는 예시다. `CrashlyticsUtil.AddActionLog`로 커스텀 로그를 전달한다.

```csharp
public void PlantBomb()
{
	DebugEx.Log($"[DemolitionBombManager] PlantBomb()");
	CrashlyticsUtil.AddActionLog($"[Battle] PlantBomb");
	_plantOrDefuseCR = StartCoroutine(nameof(DoPlant));
	DemolitionModeUi.Instance.UpdateProgress(0);
	DemolitionModeUi.Instance.ProgressUi.SetActive(true);

	//here you can replace the BlockAllWeapons() with your custom code in order to show a bomb activation hand animation instead of just hide the weapons.	  
	bl_MFPS.LocalPlayerReferences.gunManager.BlockAllWeapons();
	bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = false;
}
```

같은 패턴이 폭탄 해체(`DefuseBomb`), 봇의 설치/해체 취소(`BotCancelPlantBomb`, `BotCancelDefuseBomb`), 해체 취소(`CancelDefuse`), `bl_AIShooterHealth`의 봇 `Awake`/`OnDestroy`/사망 등 배틀 상태가 바뀌는 지점마다 반복해서 쓰인다(이 폴더 기준 `AddActionLog` 호출 9곳).

### 실제 로그 전달 (`CrashlyticsUtil.AddActionLog`)

`CrashlyticsUtil.AddActionLog`가 실제 Firebase SDK를 통해 커스텀 로그를 전달한다. `USE_FIREBASE` 심볼이 정의되지 않은 빌드에서는 `Crashlytics.Log` 호출이 컴파일에서 제외되고, 정의된 빌드에서도 `FirebaseUtil.IsFirebaseInit`이 `false`이면 아무것도 하지 않는다(`FirebaseUtil`은 이 폴더에 없다).

```csharp
public static void AddActionLog(string msg)
{
	if(!FirebaseUtil.IsFirebaseInit) { return; }
#if USE_FIREBASE
	Crashlytics.Log(msg);
#endif
}
```

실제 Crashlytics에는 Crash 발생 시점의 현황과 그 직전까지 쌓인 Custom Log가 함께 남는 것을 볼 수 있다.

<img width="1269" height="358" alt="Crashlytics_스택추적" src="https://github.com/user-attachments/assets/e0f36522-b38e-409f-bc13-5337feb13602" />
<img width="1249" height="755" alt="Crashlytics_커스텀_로그" src="https://github.com/user-attachments/assets/b9be5e97-bbda-48df-89cb-b2d5531a0d29" />
<img width="1272" height="620" alt="Crashlytics_키" src="https://github.com/user-attachments/assets/e3f9a45e-90f9-462c-aaca-16d8bc37ed1b" />

### 이슈 관리 연동

발생한 Crashlytics는 Jira에서 이슈를 생성하여 관리한다.

<img width="659" height="784" alt="Crashlytics_관련_Jira_01" src="https://github.com/user-attachments/assets/aea3ae27-c121-40ce-843a-d344b8eb951b" />
<img width="606" height="761" alt="Crashlytics_관련_Jira_02" src="https://github.com/user-attachments/assets/917d7382-4f67-43d7-aac5-e346493dd0b2" />

관련한 예외처리를 한 이후에는 해당 Jira 이슈 번호를 커밋 메시지에 포함시켜, 이후 커밋 히스토리를 파악하기 쉽게 했다. 아래 "크래시 리포트 → 방어 코드 → 커밋 추적 사례"에서 실제 커밋 메시지로 확인한 내용을 정리한다.

<img width="1716" height="576" alt="Crashlytics_관련_커밋" src="https://github.com/user-attachments/assets/e05d79aa-9081-4246-9391-ea3142a75620" />

### 크래시 리포트 → 방어 코드 → 커밋 추적 사례

먼저 범위를 분명히 해 둔다. export된 본인 커밋(2025-01-06 ~ 2025-11-26) diff에서 `AddActionLog` 호출부(`DemolitionBombManager` 등)는 수정된 줄이 아니라 이미 있던 컨텍스트 줄로만 나오고, `CrashlyticsUtil.cs`를 건드린 커밋은 없다. 2024년 이력은 export에 없으므로 이 유틸과 breadcrumb 호출부를 누가 작성했는지는 이 자료로 판단할 수 없고, 이 문서도 어느 쪽으로든 주장하지 않는다. 다만 export에서 breadcrumb 문자열을 직접 고친 흔적은 있다(2025-10-17, `PhotonNetworkEx`의 `[Room] LeavelRoom(...)`을 `[Room] Leave Room(...)`으로 수정). 아래는 git 이력(2025-01 이후)에서 확인되는 본인 커밋 중, Crashlytics에 보고된 지점을 대응한 것들이다.

> 근거: 커밋 메시지에 `[LR-###][Crashlytics]` 접두를 쓴 커밋은 export에서 4건(2025-11-04 3건, 11-10 1건)이고, 나머지 대응 커밋은 `[LR-637][v1.0.3]`처럼 작업 티켓·버전 접두를 쓴다. 이 접두가 Jira 이슈와 커밋을 잇는 방식이다. 티켓 내용 자체는 저장소에 없다.

| 날짜 | 커밋 메시지 | diff에서 확인되는 변경 |
|---|---|---|
| 2025-11-04 | `[LR-629][Crashlytics] [LR-627][Crashlytics] bl_AIShooterAgent.UpdateLookAt index out of bounds Exception 방어코드 추가` | `UpdateLookAt`과 `SetDestination` 진입부에 `if (!Agent.isOnNavMesh) return;` 추가. 주석은 "NavMesh를 아직 불러오기 전이라면 (예외상황)". |
| 2025-11-04 | `[LR-633][Crashlytics] - Framework.GameException.Throw` | 예외가 던져지는 지점(`GameException.Verify`)이 아니라 그 원인인 `UserConfig`의 경로 getter를 수정. 온라인 모드인데 UUID가 비어 있으면 `"Offline"`으로 대체. |
| 2025-11-04 | `[LR-634][Crashlytics] Game.View.BattleSystem.TutorialManager.Update 에 대한 방어코드 추가` | `Update`의 ESC 처리 조건에 `BattleMainUi.Instance != null` 추가. |
| 2025-11-10 | `[LR-634][Crashlytics] Game.View.BattleSystem.KillCam.RefreshUi 에 대한 방어코드 추가` | `BattleManager.OnOtherPlayersChange`에서 `KillCam.Instance`가 null이 아닐 때만 `RefreshUi()` 호출. |
| 2025-11-07 | `[LR-637][v1.0.3] 기타 작업 - Crashlytics에 보고된 지점에 추가적으로 세부 ActionLog 추가` | `bl_AIManager.SpawnBot`의 단계 번호(`debugStep`)를 세분화하고, 실패 시 보고 메시지에 Photon 인스턴스화 오류 상태를 포함. |
| 2025-09-26 | `[LR-535] 난독화 이후 로그 확인을 위한 Exception 을 통한 Crashlytics Report를 임의로 발생시키는 테스트 버튼 추가` | 개발자 옵션에 "Crash Report 발생" 버튼 추가. `CrashlyticsUtil.LogException(new NullReferenceException("Test NullReferenceException"))` 호출. |

이 중 가장 눈에 띄는 두 건의 diff를 발췌한다. 먼저 LR-633은 리포트에 찍힌 프레임(`GameException.Throw`)이 아니라 그 값을 만든 쪽을 고친 사례다.

```diff
-				string userUUID = FrameworkApp.IsOnlineMode ? UserInfoRepository.Instance.UUID : "Offline";
+				string userUUID = FrameworkApp.IsOnlineMode && UserInfoRepository.Instance.UUID.HasContent() ? 
+					UserInfoRepository.Instance.UUID : 
+					"Offline";
+				
 				GameException.Verify(userUUID.HasContent(), "Logic Error. Please login first!");
```

LR-637은 방어 코드가 아니라 "다음 리포트에서 원인을 더 좁히기 위한 로그 보강"이다. `SpawnBot`의 `catch` 블록이 `debugStep == 4`(Photon 룸 오브젝트 인스턴스화 단계)일 때만 `PhotonNetwork.InstantiateRoomObjectErrorStatus`를 메시지에 넣도록 바뀌었다. 이 값은 같은 커밋에서 `PhotonNetwork.cs`에 추가된 정적 필드로, `InstantiateRoomObject`가 방에 들어가기 전에 호출되어 `null`을 돌려주면 400, 다른 `null` 반환 경로에서는 401이 들어간다.

```diff
-			CrashlyticsUtil.LogException(new GameException($"SpawnBot error(step:{debugStep}): " + e.Message));
+			//PhotonRoomInstantiate 오류
+			if (debugStep == 4)
+			{
+				CrashlyticsUtil.LogException(new GameException($"SpawnBot error(step:{debugStep}), InstantiateRoomObjectErrorStatus : {PhotonNetwork.InstantiateRoomObjectErrorStatus}" + e.Message));
+			}
+			else
+			{
+				CrashlyticsUtil.LogException(new GameException($"SpawnBot error(step:{debugStep}): " + e.Message));
+			}
```

폭탄 모드 쪽에서는 2025-11-03 커밋(`[LR-535] OnRoundFinish가 Bomb이 Null 이 된 상황에서 호출되는 경우가 있어 임시 방어코드와 로그 추가 [현재 확인중]`)이 이 폴더의 `DemolitionBombManager.StopAll()`에 `Bomb != null` 검사를 넣고, `DemolitionMode`의 라운드 종료 처리에 상태 로그를 추가했다. 이 폴더의 `DemolitionBombManager.cs` 279~282행에서 그 결과를 볼 수 있다. 이 커밋 메시지는 Crashlytics 접두가 아니라 `[LR-535]`이고 "현재 확인중"이라고 적혀 있어, 원인이 확정된 수정이라기보다 임시 조치와 관찰용 로그를 함께 넣은 것으로 읽는다.

### 설계 포인트

- **왜 하필 폭탄 설치/해체 같은 지점에 로그를 남기나**: `AddActionLog`의 주석에는 "Crashlytics log의 전체 크기는 64KB로 제한된다"고 명시되어 있다. 로그 예산이 빠듯하다는 점을 감안해 모든 호출을 남기지 않고 배틀 상태가 바뀌는 지점 위주로 breadcrumb를 둔 것으로 보이며, 크래시 직전 몇 줄만으로 발생 순서를 재구성하는 것이 목적이다. 다만 이 지점들이 실제로 크래시가 몰리는 곳이라는 근거는 코드에 없다. 폭탄 모드는 `Bomb is null`을 `LogException`으로 보고하는 코드(`DemolitionBombManager.cs` 43행)와 위 2025-11-03 대응 커밋이 있어 문제가 관찰된 영역이라는 정황만 있다.
- **Firebase 초기화 여부를 매번 확인**: `AddActionLog`, `SetCustomKey` 모두 `FirebaseUtil.IsFirebaseInit`를 먼저 확인하고 빠져나간다(`CrashlyticsUtil.cs` 43행, 56행). 초기화되지 않은 상태에서 호출돼도 예외 없이 무시되므로 게임 로직 곳곳에 로그 호출을 부담 없이 심을 수 있다. `IsFirebaseInit`이 어떤 조건에서 `false`인지는 이 폴더에 없어 확인하지 못했다.
- **커스텀 키는 접두사와 분할 저장을 지원한다**: `SetCustomKey`는 `1.VERSION`, `2.USERINFO`, `3.GAMESTATUS`, `4.LASTREQ`, `5.LASTACK` 접두사 상수로 대시보드 KEY 탭의 정렬 순서를 잡고, 값이 길면 1K 단위로 최대 40조각까지 나눠 저장한다. 이 키를 실제로 채우는 호출부는 이 저장소 사본에 없다.


관련 코드: [08. Obfuscator](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/08.%20Obfuscator)
