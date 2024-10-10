Unreal 5 기반 스크린 골프 '더 스윙 제트'
==========================
> 팩트코퍼레이션,오로라메타 스크린 골프 클라이언트 개발   
> 개발 기간 : 2023.11 ~ 2024.09 [약 1년]   
> 출시 여부 : 매장 3곳 정식 런칭 [하남 미사 직영점, 청주 오창점, 제주 아라점]   

개발 환경
==========================
엔진 : Unreal 5.3.2   
플랫폼 : Windows x64   
버전 관리 : SVN   


프로젝트 소개
==========================
*더스윙 제트 특징*   
실제 스크린 골프 오프라인 매장에서 운영되는 스크린 골프 게임 개발.
유저가 공을 쳤을 때, 공을 추적하는 HW 센서   
게임 내 지형 기울기에 맞춰 바닥(플레이트) 기울기가 변하는 HW   
등 여러 HW와 API를 통해 게임과 연동되어 있다.   

*유저가 샷을 한 상황에 따라 다양한 카메라 연출*   
삼각함수와, 벡터, Bezier Curve, 낙구 예측 지점 산출 등 다양한 수학적 연산을 통해   
카메라 움직임 구현 [약 10여종 이상]

*Shader Graph를 활용한 런타임 Occlusion처리 및 Shader 최적화 작업*   
공이 사물에 가려졌을 때, 실시간으로 Shader를 활용하여 공을 가리는 물체에 대한 반투명 처리   
Material Parameter Collections에게 런타임중에 Actor의 정보를 전달하여 처리   

*Decal Component와 Shader를 활용한 지형 기울기에 따른 퍼팅 그리드 구현*
Decal을 활용하여 지형 기울기 정보를 알려주는 그리드 구현


프로젝트 관리
===========================

SVN을 통해 버전 관리   
![SVN_Commit](https://github.com/user-attachments/assets/99d17ef0-23a2-4fb9-9b88-78b9c81a685e)   

***
카메라 관련 테이블에서 조건에 해당하는 카메라 Record를 찾아 해당 Record의 카메라 Actor를 활성화 한다.   

카메라 테이블 일부   
![ShotCamera_table](https://github.com/user-attachments/assets/d1295b86-8ab1-493b-8f1a-4ec29667846c)   

<pre>
  <code>
    UFirstCameraRecord* ASGCamera::GetTargetFirstCameraPriority(int32 Priority, ASGPlayerCharacter* Player)
    {
    #ifdef ENABLE_DEBUG_CAMERA
    	if(IsValid(PHYSICAL_MANAGER))
    	{
    		int DebugFirstCameraIndex = PHYSICAL_MANAGER->GetCameraOneIndex();
    		bool IsDebugIndexExist = DebugFirstCameraIndex != 0;
    		if(IsDebugIndexExist)
    		{
    			// SG_LOG(Log, "SJW [DebugCamera] First Camera %i", DebugFirstCameraIndex);
    		
    			return GetFirstCameraRecordByIndex(DebugFirstCameraIndex);
    		}
    	}
    #endif
    	
    	auto TargetRecords = GetFirstCameraRecordsByPriority(Priority);
    	if(TargetRecords.Num() > 0)
    	{
    		TArray<UFirstCameraRecord*> ValidRecords;	//조건을 최종적으로 만족하는 Record 그룹
    
    		for (auto TargetRecord : TargetRecords)
    		{
    			float CurrentHoleCupDist = FVector::Dist2D(Player->GetActorLocation(), holecupLocation);
    
    			{
    				int32 TeeShot = TargetRecord->Tee_Shot;
    				if(TeeShot != -1)
    				{
    					bool TeeShotCond = TeeShot == 1;
    					if(IsTeeShot != TeeShotCond)
    					{
    						// SG_LOG(Log, "SJW 333 TeeShot Condition true");
    						continue;
    					}
    					
    					// SG_LOG(Log, "SJW 333 TeeShot Condition false");
    				}
    			}
    			
    			//1. 홀컵 관련 조건 검사
    			{
    				float HoleDistanceMin = TargetRecord->Hole_Start_Distance_Min;
    				float HoleDistanceMax = TargetRecord->Hole_Start_Distance_Max;
    			
    				if(HoleDistanceMin != -1)
    				{
    					if(CurrentHoleCupDist < HoleDistanceMin) { continue; }
    				}
    
    				if(HoleDistanceMax != -1)
    				{
    					if(CurrentHoleCupDist > HoleDistanceMax) { continue; } 
    				}
    			}
    
    			//2. 볼 파워 조건 검사
    			{
    				float BallPowerMin = TargetRecord->Ball_Power_Min;
    				float BallPowerMax = TargetRecord->Ball_Power_Max;
    
    				if(BallPowerMin != -1)
    				{
    					if(ballPower < BallPowerMin) { continue; }
    				}
    
    				if(BallPowerMax != -1)
    				{
    					if(ballPower > BallPowerMax) { continue; }
    				}
    			}
    
    			//3. 발사각 조건 검사
    			{
    				float LaunchDegreeMin = TargetRecord->Launch_Degree_Min;
    				float LaunchDegreeMax = TargetRecord->Launch_Degree_Max;
    
    				if(LaunchDegreeMin != -1)
    				{
    					if(launchAngle < LaunchDegreeMin) { continue; }
    				}
    
    				if(LaunchDegreeMax != -1)
    				{
    					if(launchAngle > LaunchDegreeMax) { continue; }
    				}
    			}
    
    			//4. 확률 0인 것 제외
    			{
    				float TargetActiveRate = TargetRecord->Camera_Active_Rate;
    				if(fabs(TargetActiveRate) <= EPSILON) {	continue; }
    			}
    
    			//조건을 모두 만족하는 경우
    			ValidRecords.Add(TargetRecord);
    		}
    
    		//조건을 만족하는 그룹이 없는 경우 다음 우선순위 진행
    		if(ValidRecords.Num() == 0)
    		{
    			// SG_LOG(Log, "GetTargetFirstCameraPriority NextPriority Case a...");
    			
    			Priority += 1;
    			return GetTargetFirstCameraPriority(Priority, Player);
    		}
    		
    		//확률 테이블 세팅
    		TArray<PercentageData> PercentageDatas;
    		PercentageData* mPercentageData = new PercentageData(1, ValidRecords[0]->Camera_Active_Rate);
    		PercentageDatas.Add(*mPercentageData);
    
    		//해당되는 Record가 2개 이상인 경우
    		if(ValidRecords.Num() > 1)
    		{
    			for(int i = 1; i < ValidRecords.Num(); i++)
    			{
    				mPercentageData = new PercentageData(PercentageDatas[i - 1].Max + 1, PercentageDatas[i - 1].Max + ValidRecords[i]->Camera_Active_Rate);
    				PercentageDatas.Add(*mPercentageData);
    			}
    		}
    		//~확률 테이블 세팅
    		
    		UFirstCameraRecord* TargetRecord = nullptr;
    
    		//4. 조건을 만족하는 그룹에서 확률을 기반으로 무작위 Record를 추출한다.
    		int RndResult = FMath::RandRange(1, 100);
    		for(int i = 0; i < PercentageDatas.Num(); i++)
    		{
    			if((RndResult >= PercentageDatas[i].Min) && (RndResult < PercentageDatas[i].Max))
    			{
    				TargetRecord = TargetRecords[i];
    			}
    		}
    
    		if(TargetRecord == nullptr)
    		{
    			// SG_LOG(Log, "GetTargetFirstCameraPriority NextPriority Case b...");
    			
    			Priority += 1;
    			return GetTargetFirstCameraPriority(Priority, Player);
    		}
    
    		return TargetRecord;
    	}
    	
    	// SG_LOG(Log, "GetTargetFirstCameraPriority Exception Case...");
    	return nullptr;
    }
  </code>
</pre>
