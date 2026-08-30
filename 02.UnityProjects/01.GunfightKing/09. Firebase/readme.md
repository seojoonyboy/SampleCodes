## Firebase Crashlytics 연동 및 커스텀 로그 (Firebase)

Firebase Crashlytics 기능을 활용하여, Client App에서 발생하는 Exception이나 의도하지 않은 예외 상황을 Firebase에 전달해 로그를 백업하고, Live 서비스 중인 App의 Crash 상황 전후를 파악하기 쉽게 하여 안정성 향상을 도모하였다.

<img width="1419" height="599" alt="image" src="https://github.com/user-attachments/assets/e445aa40-c578-4788-886a-c25889c57a70" />

* 기존 비정상 종료가 발생하지 않은 유저가 기존에 하루 평균 최저 88%의 안정화를 보였으나 이후, 관련 작업을 통한 모니터링과 효율적인 수정 대응으로 최고 99% 안정화를 도출함

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

같은 패턴이 폭탄 해체(`DefuseBomb`), 설치/해체 취소, `bl_AIShooterHealth`의 봇 스폰·사망 등 배틀 상태가 바뀌는 지점마다 반복해서 쓰인다.

### 실제 로그 전달 (`CrashlyticsUtil.AddActionLog`)

`CrashlyticsUtil.AddActionLog`가 실제 Firebase SDK를 통해 커스텀 로그를 전달한다.

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

관련한 예외처리를 한 이후에는 해당 Jira 이슈 번호를 커밋 메시지에 포함시켜, 이후 커밋 히스토리를 파악하기 쉽게 했다.

<img width="1716" height="576" alt="Crashlytics_관련_커밋" src="https://github.com/user-attachments/assets/e05d79aa-9081-4246-9391-ea3142a75620" />

### 설계 포인트

- **왜 하필 폭탄 설치/해체 같은 지점에 로그를 남기나**: `AddActionLog`의 주석에는 "Crashlytics log의 전체 크기는 64KB로 제한된다"고 명시되어 있다. 로그 예산이 빠듯하기 때문에 모든 호출을 남기는 대신, 폭탄 설치·해체·취소나 봇 스폰/사망처럼 실제로 크래시가 몰리는 배틀 상태 전환 지점에만 선택적으로 breadcrumb를 남겨, 크래시 직전 몇 줄의 로그만으로도 어떤 순서로 문제가 발생했는지 재구성할 수 있게 했다.
- **Firebase 초기화 여부를 매번 확인**: `AddActionLog`, `SetCustomKey` 모두 `FirebaseUtil.IsFirebaseInit`를 먼저 확인하고 빠져나간다. Firebase가 초기화되지 않은 환경(오프라인/일부 테스트 빌드 등)에서 호출돼도 예외 없이 조용히 무시되므로, 게임 플레이 로직 곳곳에 로그 호출을 부담 없이 심을 수 있다.

관련 코드: [08. Obfuscator](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/08.%20Obfuscator)
