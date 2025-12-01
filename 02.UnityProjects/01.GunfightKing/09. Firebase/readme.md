> Firebase Crashlytics 기능을 활용하여, Client App에서 발생하는 Exception 이나 의도하지 않은 예외 상황을 Firebase에 전달하여 로그를 백업하여
> Live 서비스 중인 App의 Crash 상황 전후를 파악하기 쉽게 하여, 안정성 향샹을 도모 하였음.

<img width="1419" height="599" alt="image" src="https://github.com/user-attachments/assets/e445aa40-c578-4788-886a-c25889c57a70" />   
* 기존 비정상 종료가 발생하지 않은 유저가 기존에 하루 평균 최저 88%의 안정화를 보였으나 이후, 관련 작업을 통한 모니터링과 효율적인 수정 대응으로 최고 99% 안정화를 도출함

<img width="1288" height="462" alt="image" src="https://github.com/user-attachments/assets/06d4e492-6e64-46f9-b621-78311c6d99f8" />   
* 실제 Firebase에게 Crashlytics가 발생하는 시점에 커스텀 로그를 추가하는 작업에 대한 예시   

------------

> 폭탄 모드에서 폭탄 설치가 일어났을 때, Crashlytics에 CustomLog를 쌓는 예시. [DemolitionBombManager.cs]
> CrashlyticsUtil.AddActionLog 에 의해서 Custom Log를 전달한다. 
<pre>
  <code>
    public void PlantBomb()
		{
			CrashlyticsUtil.AddActionLog($"[Battle] PlantBomb");
			_plantOrDefuseCR = StartCoroutine(nameof(DoPlant));
			DemolitionModeUi.Instance.UpdateProgress(0);
			DemolitionModeUi.Instance.ProgressUi.SetActive(true);

			//here you can replace the BlockAllWeapons() with your custom code in order to show a bomb activation hand animation instead of just hide the weapons.	  
			bl_MFPS.LocalPlayerReferences.gunManager.BlockAllWeapons();
			bl_MFPS.LocalPlayerReferences.firstPersonController.isControlable = false;
		}
  </code>
</pre>
   
   
> CrashlyticsUtil.AddActionLog 에서 실제 Firebase SDK를 통해 Custom Log를 전달한다.
<pre>
  <code>
    public static void AddActionLog(string msg)
		{
			if(!FirebaseUtil.IsFirebaseInit) { return; }
#if USE_FIREBASE
			Crashlytics.Log(msg);
#endif
		}
  </code>
</pre>   

> 실제 Crashlytics에 Crash 상황에 현황과 직전 Custom Log가 쌓인 것을 볼 수 있음
<img width="1269" height="358" alt="Crashlytics_스택추적" src="https://github.com/user-attachments/assets/e0f36522-b38e-409f-bc13-5337feb13602" />
<img width="1249" height="755" alt="Crashlytics_커스텀_로그" src="https://github.com/user-attachments/assets/b9be5e97-bbda-48df-89cb-b2d5531a0d29" />
<img width="1272" height="620" alt="Crashlytics_키" src="https://github.com/user-attachments/assets/e3f9a45e-90f9-462c-aaca-16d8bc37ed1b" />


> 발생한 Crashlytics 는 Jira에서 이슈를 생성하여 관리
<img width="659" height="784" alt="Crashlytics_관련_Jira_01" src="https://github.com/user-attachments/assets/aea3ae27-c121-40ce-843a-d344b8eb951b" />
<img width="606" height="761" alt="Crashlytics_관련_Jira_02" src="https://github.com/user-attachments/assets/917d7382-4f67-43d7-aac5-e346493dd0b2" />

> 관련한 예외처리를 한 이후, 해당 Jira의 이슈 번호를 Commit 메시지에 포함시켜, 이후 Commit history를 파악하기 쉽게 함

<img width="1716" height="576" alt="Crashlytics_관련_커밋" src="https://github.com/user-attachments/assets/e05d79aa-9081-4246-9391-ea3142a75620" />
