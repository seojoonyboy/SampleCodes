#include "SGCamera.h"

#include "SGBowlerFreeView.h"
#include "SGGameInstance.h"
#include "SGCameraMode.h"
#include "SGCourseMode.h"
#include "SGDrivingMode.h"
#include "SGPlayerCharacter.h"
#include "SGGroundChecker.h"
#include "SGPixelEdgeFinder.h"
#include "SGPuttingGuide.h"
#include "SGShotCameraTable.h"
#include "SGShotDataManager.h"
#include "SGTableManager.h"
#include "Kismet/KismetMathLibrary.h"

#define ENABLE_DEBUG_CAMERA
#define EPSILON 0.00001

/**
*
*/
ASGCamera::ASGCamera()
{
	PrimaryActorTick.bCanEverTick = true;
}

void ASGCamera::BeginPlay()
{
	Super::BeginPlay();
	
	SpawnCamera();

	IsDecalRequestAvailable = false;
}

void ASGCamera::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	Super::EndPlay(EndPlayReason);

	if(IsValid(GetWorld()))
	{
		GetWorld()->GetTimerManager().ClearTimer(TreeHitHandle);
		GetWorld()->GetTimerManager().ClearTimer(PuttingTraceCameraWaitHandle);
	}
}

void ASGCamera::Tick(float DeltaTime)
{
	Super::Tick(DeltaTime);
	
	LandingCheck(DeltaTime);
	
	// 공을 치면 카메라 동작
	if (CanTickCameraWork)
	{
		TickCameraWork(DeltaTime);
	}
	// 공이 멈추면 카메라 위치 초기화
	else
	{
		if (IsValid(CurFollowCam) && !IsPenaltyArea)
		{
			TraceCamera->SetActorLocation(CurFollowCam->GetComponentLocation());
			TraceCamera->SetActorRotation(CurFollowCam->GetComponentRotation());
	
			StartFixedCamera->SetActorLocation(CurFollowCam->GetComponentLocation());
			StartFixedCamera->SetActorRotation(CurFollowCam->GetComponentRotation());
		}
	}
}

void ASGCamera::SpawnCamera()
{
	UClass* CourseCameraFindClass = Cast<UClass>(StaticLoadObject(UClass::StaticClass(), NULL, TEXT("Blueprint'/Game/Game/Camera/SGCourseCamera.SGCourseCamera_C'")));
	if (nullptr == CourseCameraFindClass) return;

	TraceCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if (nullptr != TraceCamera)
	{
#if WITH_EDITOR
		TraceCamera->SetActorLabel(TEXT("Z_TraceCamera"));
#endif
		TraceCamera->GetCamera()->SetRelativeRotation(FRotator(ShiftRotateAngle, 0, 0));

		TraceCamera->GetSpringArm()->bDoCollisionTest = false;
		TraceCamera->GetSpringArm()->TargetArmLength = TraceCameraTargetArmLength;
		TraceCamera->GetSpringArm()->CameraLagSpeed = 1;
		TraceCamera->GetSpringArm()->CameraRotationLagSpeed = 4;
	}

	ReverseCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if (nullptr != ReverseCamera)
	{
#if WITH_EDITOR
		ReverseCamera->SetActorLabel(TEXT("Z_ReverseCamera"));
#endif
	}

	StartFixedCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if (nullptr != StartFixedCamera)
	{
#if WITH_EDITOR
		StartFixedCamera->SetActorLabel(TEXT("Z_StartFixedCamera"));
#endif

		StartFixedCamera->GetSpringArm()->bDoCollisionTest = false;
		StartFixedCamera->GetSpringArm()->TargetArmLength = 300;
	}

	GreenCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if (nullptr != GreenCamera)
	{
#if WITH_EDITOR
		GreenCamera->SetActorLabel(TEXT("Z_GreenCamera"));
#endif
		GreenCamera->GetCamera()->SetRelativeLocation(FVector(0, 0, 900));

		GreenCamera->GetSpringArm()->bDoCollisionTest = false;
		GreenCamera->GetSpringArm()->TargetArmLength = 450;
	}
	
	SideCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if (nullptr != SideCamera)
	{
#if WITH_EDITOR
		SideCamera->SetActorLabel(TEXT("Z_SideCamera"));
#endif

		SideCamera->GetSpringArm()->bEnableCameraLag = false;
		SideCamera->GetSpringArm()->bEnableCameraRotationLag = false;
	}

	ReverseLandFixedCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if(nullptr != ReverseLandFixedCamera)
	{
#if WITH_EDITOR
		ReverseLandFixedCamera->SetActorLabel(TEXT("Z_ReverseLandFixedCamera"));
#endif

		ReverseLandFixedCamera->GetSpringArm()->bEnableCameraLag = false;
		ReverseLandFixedCamera->GetSpringArm()->TargetArmLength = 300;
	}

	BadPlaceHitFixedCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if(nullptr != BadPlaceHitFixedCamera)
	{
#if WITH_EDITOR
		BadPlaceHitFixedCamera->SetActorLabel(TEXT("Z_BadPlaceHitFixedCamera"));		
#endif
		BadPlaceHitFixedCamera->GetSpringArm()->bEnableCameraLag = false;
	}

	PuttingZoomCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if(nullptr != PuttingZoomCamera)
	{
#if WITH_EDITOR
		PuttingZoomCamera->SetActorLabel(TEXT("Z_PuttingZoomCamera"));
#endif
		PuttingZoomCamera->GetSpringArm()->bDoCollisionTest = false;
		PuttingZoomCamera->GetSpringArm()->SetActive(false);
		PuttingZoomCamera->GetSpringArm()->bEnableCameraLag = false;
		PuttingZoomCamera->GetSpringArm()->bEnableCameraRotationLag = false;
		PuttingZoomCamera->GetSpringArm()->TargetArmLength = 10;
	}

	PuttingTraceCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if(nullptr != PuttingTraceCamera)
	{
#if WITH_EDITOR
		PuttingTraceCamera->SetActorLabel(TEXT("Z_PuttingTraceCamera"));
#endif
		//퍼팅용 카메라는 SpringArm 사용하지 않음
		PuttingTraceCamera->GetSpringArm()->bDoCollisionTest = false;
		PuttingTraceCamera->GetSpringArm()->SetActive(false);
		PuttingTraceCamera->GetSpringArm()->bEnableCameraLag = false;
		PuttingTraceCamera->GetSpringArm()->bEnableCameraRotationLag = false;
		PuttingTraceCamera->GetSpringArm()->TargetArmLength = 10;
	}

	EndShotWaitCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if(nullptr != EndShotWaitCamera)
	{
#if WITH_EDITOR
		EndShotWaitCamera->SetActorLabel(TEXT("Z_EndShotWaitCamera"));
#endif
		EndShotWaitCamera->GetSpringArm()->Deactivate();
		EndShotWaitCamera->GetCamera()->FieldOfView = 90;
	}
}

void ASGCamera::ImpactCameraSetting(ERenderType::Type RenderType, bool mIsTeeShot)
{
	IsTeeShot = mIsTeeShot;
	
	NewImpactCameraSettings(RenderType);
}

void ASGCamera::NewImpactCameraSettings(ERenderType::Type RenderType)
{
	IsDecalRequestAvailable = true;
	
	IPFinder = Cast<ASGIPFinder>(UGameplayStatics::GetActorOfClass(GetWorld(), ASGIPFinder::StaticClass()));
	if(IPFinder != nullptr)	{ IPFinder->ResetAllDecalScale(); }
	
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if (nullptr == GameInst) return;

	APlayerController* Controller = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
	if (nullptr == Controller) return;

	ASGPlayerCharacter* Player = Cast<ASGPlayerCharacter>(Controller->GetCharacter());
	if (nullptr == Player) return;

	Player->ClearWaitCameraTimer();

	ballPower = GameInst->GetBallSpeed();
	launchAngle = GameInst->GetLaunchAngle();
	
	sideSpinRate = GameInst->GetSideSpinRate();

	//현재 화면 크기 정보를 가져온다. [에디터에서 스크린 사이즈를 중간에 변경할 수 있기 때문에 Impact에서 갱신한다.]
	{
		int32 ViewPortSizeX = 1920;
		int32 ViewPortSizeY = 1080;
		Controller->GetViewportSize(ViewPortSizeX, ViewPortSizeY);
		ScreenSize = FVector2d(ViewPortSizeX, ViewPortSizeY);
	}

	FVector NormalizeBallForwardVector = Player->GetActorForwardVector().GetSafeNormal();
	FVector NormalizeBeginForwardVector = BeginForwardDir.GetSafeNormal();
	float DotProduct = FVector::DotProduct(NormalizeBallForwardVector, NormalizeBeginForwardVector);

	BallForwardVectorAfterImpact = Player->GetActorForwardVector();
	FVector ShiftedCamToBallDirVector = BallForwardVectorAfterImpact.RotateAngleAxis(ShiftRotateAngle, FVector::ZAxisVector);
	
	float AngleInRadian = FMath::Acos(FMath::Clamp(DotProduct, -1.0f, 1.0f));
	launchSideAngleInDegree = FMath::RadiansToDegrees(AngleInRadian);
	
	{
		IsNotChange = false;
		CanTickCameraWork = true;
		IsLanding = false;
		IsPutting = false;
		flyingTime = 0;
		oldLocation = startLocation = Player->GetActorLocation() - FVector(0, 0, 30); // 처음 최고점 구할 때는 oldLocation이 현재 플레이어 높이보다 낮아야함

		CanTraceCamera = true;
		CanRotateTraceCamera = true;
	}

	ASGDrivingMode* const drivingMode = Cast<ASGDrivingMode>(GetWorld()->GetAuthGameMode());
	if (IsValid(drivingMode))
	{
		IsDrivingMode = true;
		if (IsValid(drivingMode->GetTargetActor()))
			holecupLocation = drivingMode->GetTargetActor()->GetActorLocation(); // 홀컵 위치 찾기
	}
	else
	{
		IsDrivingMode = false;
		holecupLocation = Player->CharacterComponent->GetPinPos(); // 홀컵 위치 찾기

		EndShotWaitCameraNeed = true;
	}

	if(!IsDrivingMode)
	{
		if(IsTeeShot) {	TraceCamera->GetCamera()->FieldOfView = 70;	}
		else { TraceCamera->GetCamera()->FieldOfView = 80; }
	}

 	if(drivingMode)
	{
#ifdef ENABLE_DEBUG_CAMERA
 		bool IsDebugCameraExist = PHYSICAL_MANAGER->GetCameraOneIndex() != 0;
 		if(IsDebugCameraExist)
 		{
 			SelectedFirstCameraRecord = GetTargetFirstCameraPriority(1, Player);
 			BeforeApexCamNum = SelectedFirstCameraRecord != nullptr ?
				GetBeforeApexCamNumFromStringValue(SelectedFirstCameraRecord->Camera_Result) : 2;
 		}
 		else { BeforeApexCamNum = 2; }
#endif
	}
	else
	{
		SelectedFirstCameraRecord = GetTargetFirstCameraPriority(1, Player);
		BeforeApexCamNum = SelectedFirstCameraRecord != nullptr ?
			GetBeforeApexCamNumFromStringValue(SelectedFirstCameraRecord->Camera_Result) : 2;
		
		SG_LOG(Log, "SJW BeforeApexCamNum : %i", BeforeApexCamNum);
		
		//test code
		//BeforeApexCamNum = 2;
		//end test code
	}

	IsPutting = Player->IsOnApronORGreen() && (PHYSICAL_MANAGER->GetDrivingModeType() == EDrivingModeType::Putter);
	
	if (IsDrivingMode)
	{
		TraceCamera->SetActorRotation(StartPlayerRot);
		StartFixedCamera->SetActorRotation(StartPlayerRot);
	}
	else
	{
		if (IsPutting)
		{
			SelectedPuttingCameraRecord = GetTargetPuttingCameraRecordByPriority(1, Player);
			InitPuttingTraceCameraWork(Controller, Player);

			//test code
			//SelectedPuttingCameraRecord = GetTargetPuttingCameraRecordByIndex(10);
			//end test code

			TraceCamera->GetSpringArm()->SocketOffset = FVector(0, 0, 0);
			TraceCamera->GetSpringArm()->TargetArmLength = Player->GetCameraBoom()->TargetArmLength;
			TraceCamera->SetActorLocation(Player->GetFollowCamera()->GetComponentLocation());
			
			//test code
			// ASGCourseMode* CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();
			// if(IsValid(CourseMode))
			// {
			// 	CourseMode->SpawnDebugBlueBall(predictPuttingPosition);
			// }
			//~test code
			
			if(IsValid(SelectedPuttingCameraRecord))
			{
				FString CameraResultStr = SelectedPuttingCameraRecord->Camera_Result;
				PuttingCamNum = GetPuttingCamNumFromStringValue(CameraResultStr);

				//test code
				//PuttingCamNum = 2;
				//end test code
				
				if(PuttingCamNum == 2)
				{
					FVector BallLocation = Player->GetActorLocation();
					FVector DirBallToHoleCup = holecupLocation - BallLocation;
					DirBallToHoleCup.Z = 0;
					DirBallToHoleCup = DirBallToHoleCup.RotateAngleAxis(-5.0f, FVector::ZAxisVector);
					DirBallToHoleCup.Normalize();

					float DistOffset = IsValid(SelectedPuttingCameraRecord) ?
						SelectedPuttingCameraRecord->Putt_Fixed_Distance * 100
						: 300;

					//test code
					//DistOffset = 300;
					//end test code

					FVector NewGreenCamLocation = holecupLocation + DirBallToHoleCup * DistOffset;
					NewGreenCamLocation.Z = BallLocation.Z + 50;
					
					float HeightOffset = IsValid(SelectedPuttingCameraRecord) ?
						SelectedPuttingCameraRecord->Putt_Fixed_Height * 100
						: 100;

					//test code
					//HeightOffset = 150;
					//end test code
					
					NewGreenCamLocation.Z += HeightOffset;
					// NewGreenCamLocation.Y += 100;
					
					GreenCamera->SetActorLocation(NewGreenCamLocation);
					GreenCamera->GetCamera()->SetWorldLocation(NewGreenCamLocation);
					// GroundCheck(GreenCamera->GetCamera()->GetComponentLocation(), NewGreenCamLocation);

					FRotator Rot = FRotationMatrix::MakeFromX(Player->GetActorLocation() - GreenCamera->GetCamera()->GetComponentLocation()).Rotator();
					GreenCamera->SetActorRotation(Rot);
					
					GetWorld()->GetTimerManager().SetTimer(PuttingTraceCameraWaitHandle, [this, Controller, Player]() 
					{
						Controller->SetViewTarget(GreenCamera);
					},	0.2f, false, 0);
				}
			}
		}
		else
		{
			FRotator Rot = FRotationMatrix::MakeFromX(ShiftedCamToBallDirVector).Rotator();
			Rot.Pitch = 0;
			TraceCamera->GetCamera()->SetWorldRotation(Rot);
		}
	}
}


void ASGCamera::EndShot()
{
	IsInitTraceCamera = false;
	
	IsUpperOutSafeAreaDetected = false;
	IsBottomOutSafeAreaDetected = false;
	
	PuttingPassTime = 0;
	IsPuttingBallForwardMove = true;
	
	FlagCameraToReverseCamera = true;
	CanTickCameraWork = false;
	IsPenaltyArea = false;
	IsBadPlaceHitCameraLocated = false;

	CameraStep = 0;
	IsBeforeApexCamNumDecided = false;
	IsAfterApexCamNumDecided = false;

	IsDecalRequestAvailable = false;

	CanTraceCamera = true;
	CanRotateTraceCamera = true;
	CanGreenCameraLookHoleCup = true;
	
	IsHitBadPlace = false;
	
	traceCameraBreakTime = 0;

	IsBallImpacted = false;

	IsBadImpacted = false;
	IsHoleIn = false;
	
	IsPuttingZoomCameraSetLocationRotation = false;

	IsPutting = false;
	
	if(!IsDrivingMode && IsValid(IPFinder))
	{ 
		IPFinder->ResetAllDecalScale();
	}
	
	InitCameraLag();
	InitCameraRelativeRotation();
	TraceCamera->GetSpringArm()->CameraLagSpeed = 1;
	TraceCamera->GetSpringArm()->SocketOffset = FVector(0, 0, 100);
	TraceCamera->GetCamera()->SetRelativeLocation(FVector(0, 0, 100));
	
	PuttingZoomCamera->GetCamera()->FieldOfView = 80;

	if(ShiftRotateAngle == 0)
	{
		TraceCamera->GetSpringArm()->SetRelativeRotation(FRotator::ZeroRotator);
	}
	else
	{
		int dir = ShiftRotateAngle > 0 ? 1 : -1;
		TraceCamera->GetSpringArm()->SetRelativeRotation(FRotator(0, -1 * ShiftRotateAngle, 0));	
	}
	
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if (nullptr != GameInst)
	{
		APlayerController* Controller = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
		if (nullptr != Controller)
		{
			ASGPlayerCharacter* Player = Cast<ASGPlayerCharacter>(Controller->GetPawn());
			if (nullptr != Player)
			{
				Controller->SetViewTarget(Player->GetFollowCamera()->GetOwner());

				FVector PlayerLocation = Player->GetActorLocation();
				StartFixedCamera->SetActorLocation(PlayerLocation);

				FVector BallToTraceCameraVector = Player->GetActorLocation() - TraceCamera->GetCamera()->GetComponentLocation();
				FVector LeftVector = BallToTraceCameraVector.RotateAngleAxis(-90, FVector::ZAxisVector).GetSafeNormal();
				LeftVector.Z = 0;
				LeftVector *= ShiftLeftAmount;
				
				TraceCamera->SetActorLocation(PlayerLocation);
				FRotator Rot = FRotationMatrix::MakeFromX(BallToTraceCameraVector + LeftVector).Rotator();
				TraceCamera->GetCamera()->SetWorldRotation(Rot);
				
				BadPlaceHitFixedCamera->SetActorLocation(PlayerLocation);

				BeginForwardDir = Player->GetActorForwardVector();

				Player->GetCameraBoom()->SocketOffset = FVector(0, 0, 100);
				Player->GetCameraBoom()->SetRelativeLocation(FVector::ZeroVector);
				Player->GetFollowCamera()->SetRelativeLocation(FVector::ZeroVector);
				
				Player->UpdateCameraFieldOfView();
				
				Player->GetFollowCamera()->SetRelativeRotation(FRotator(2, 0, 0));
			}
		}
	}
}

void ASGCamera::TickCameraWork(float DeltaTime)
{
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if (nullptr == GameInst) return;

	APlayerController* Controller = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
	if (nullptr == Controller) return;

	ASGPlayerCharacter* Player = Cast<ASGPlayerCharacter>(Controller->GetPawn());
	if (nullptr == Player) return;

	Player->TraceCameraLastLocation = TraceCamera->GetActorLocation();

	if(Player->GetIsHitBadPlace())
	{
		if(!IsHitBadPlace)
		{
			if(Player->GetLastHitSurface() == SurfaceType14)
			{
				FVector BeforeBallForwardVector = Player->GetActorForwardVector().GetSafeNormal();
				GetWorld()->GetTimerManager().SetTimer(TreeHitHandle, [this, Player, BeforeBallForwardVector]()
				{
					FVector CurrentBallForwardVector = Player->GetActorForwardVector().GetSafeNormal();
					float DotProduct = FVector::DotProduct(BeforeBallForwardVector, CurrentBallForwardVector);
					float AngleInRadian = FMath::Acos(FMath::Clamp(DotProduct, -1.0f, 1.0f));
					
					float Degree = FMath::RadiansToDegrees(AngleInRadian);
					SG_LOG(Log, "Tree hit After Degree %f", Degree);

					if((Degree > 15) && Player->GetFlyingCheck())
					{
						SG_LOG(Log, "SJW Hit BadPlace");
						IsHitBadPlace = true;
					}
					
				}, 30.0f, false, 0.5f);
			}
		}
	}
	
	if (Player->GetIsInHazard())
	{
		CanTickCameraWork = false;
		IsPenaltyArea = true;
		return;
	}

	// OB 검사
	if (!Player->GetBallMoving() && Player->GetShotArrivalCondition() == EShotJudgeArrival::OB)
	{
		CanTickCameraWork = false;
		IsPenaltyArea = true;
		return;
	}

	// 퍼팅 카메라 (그린 지역에서만)
	if (IsPutting)
	{
		// SG_LOG(Log, "PuttingCamNum %i", PuttingCamNum);
		UPuttCameraRecord* ResultRecord = SelectedPuttingCameraRecord;

		//test code
		// ResultRecord = GetTargetPuttingCameraRecordByIndex(9);
		// PuttingCamNum = 1;
		//end test code

		float BallToHoleCupDist = FVector::Dist2D(Player->GetActorLocation(), holecupLocation);
		
		//역방향 카메라
		if (PuttingCamNum == 2)
		{
			// float CamToBallDist = FVector::Dist(
			// 	Player->GetActorLocation(),
			// 	GreenCamera->GetCamera()->GetComponentLocation()
			// );

			if(BallToHoleCupDist > SG_HOLE_CUP_DIAMETER)
			{
				FVector LookTarget = Player->GetActorLocation() - GreenCamera->GetCamera()->GetComponentLocation();
				FRotator Rot = FRotationMatrix::MakeFromX(LookTarget).Rotator();
				GreenCamera->GetCamera()->SetWorldRotation(Rot);
			}
		}
		// 추적 카메라 (정방향)
		else
		{
			// SG_LOG(Log, "SJW 555 BallToHoleCupDist %f", BallToHoleCupDist);
			// SG_LOG(Log, "SJW 555 Player->GetVelocity().Z %f", Player->GetVelocity().Z);
			
			if(BallToHoleCupDist < 8)
			{
				// SG_LOG(Log, "SJW 555 GetLastShotArrivalCondition hole in");
				
				Player->GetCameraBoom()->CameraLagSpeed = 100;

				//Note. 공이 홀에 들어가는 경우 SpringArm에 의해 Follow Camera가 강제로 공에 붙어 홀 안으로 들어가는 경우를
				//막기 위해, SrpingArm 을 홀컵에 들어갈 것이 예측되는 경우 강제로 살짝 올려준다.
				Player->GetCameraBoom()->SetRelativeLocation(FVector(0, 0, 50));
				
				CanTraceCamera = false;
				IsHoleIn = true;
			}
			else
			{
				//속도가 어느정도 있고, 역방향으로 돌아오고 있고, 어느정도 홀컵에서 멀어진 경우
				if(IsHoleIn && (BallToHoleCupDist > 50))
				{
					SG_LOG(Log, "SJW 666 Move after Hole hit!!!");
					
					IsHoleIn = false;
				}
				
				CanTraceCamera = true;
			}
			
			if(IsValid(ResultRecord))
			{
				int32 ZoomCameraActiveCond = ResultRecord->Zoom_Camera_Active;
				if(ZoomCameraActiveCond == 1)
				{
					int32 ZoomCameraActiveDist = ResultRecord->Zoom_Camera_Active_Distance * 100;
					if(BallToHoleCupDist <= ZoomCameraActiveDist)
					{
						PuttingZoomCameraWork(Controller, Player);
						CanTraceCamera = false;
					}
				}
			}
			
			// SG_LOG(Log, "BallToHoleCupDist %f", BallToHoleCupDist);
			
			if(!CanTraceCamera || !Player->GetBallMoving())
			{
				float MinLagSpeed = 0.5f;
				if(IsPutting && IsValid(ResultRecord))
				{
					int32 ZoomCameraActiveCond = ResultRecord->Zoom_Camera_Active;
					if(ZoomCameraActiveCond == 1) {	MinLagSpeed = 0; }
				}
				if(TraceCamera->GetSpringArm()->CameraLagSpeed > MinLagSpeed)
				{
					TraceCamera->GetSpringArm()->CameraLagSpeed -= 0.5;
				}
				return;
			}
			
			if (!TraceCamera->GetSpringArm()->bEnableCameraLag)
				TraceCamera->GetSpringArm()->bEnableCameraLag = true;

			if(CanRotateTraceCamera)
			{
				if(!TraceCamera->GetSpringArm()->bEnableCameraRotationLag)
					TraceCamera->GetSpringArm()->bEnableCameraRotationLag = true;
			}
			else
			{
				if(TraceCamera->GetSpringArm()->bEnableCameraRotationLag)
					TraceCamera->GetSpringArm()->bEnableCameraRotationLag = false;
			}

			TraceCameraWork(Controller, Player, DeltaTime);
			PuttingZoomCamera->SetActorLocation(TraceCamera->GetActorLocation());
			
			if(CanRotateTraceCamera && IsPutting)
			{
				FRotator CurrentRotation = TraceCamera->GetCamera()->GetComponentRotation();
				FRotator NewRotation = FRotationMatrix::MakeFromX(Player->GetActorLocation() - TraceCamera->GetCamera()->GetComponentLocation()).Rotator(); 

				FRotator ResultRot = FMath::Lerp(CurrentRotation, NewRotation, 0.5f);
				TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
			}
		}
	}
	// 기본 카메라 (그린 지역이 아닌 곳 전부)
	else
	{
		flyingTime += DeltaTime;

		if(IsLanding && !Player->GetFlyingCheck() && !IsBallImpacted)
		{
			SG_LOG(Log, "SJW 333 Ball Impacted ground...");
			BallImpactedPosition = Player->GetActorLocation();
			TraceCameraPrevLocationAfterBallImpacted = TraceCamera->GetCamera()->GetComponentLocation();
			IsBallImpacted = true;
		}

		if(IsHitBadPlace)
		{
			AfterApexCamNum = 5;
			IsAfterApexCamNumDecided = true;
			InitBadPlaceHitCameraWork(Controller, Player);
			
			CameraStep = 1;
		}
		
		if(CameraStep == 0)
		{
			//APEX를 지났을 때, 2차 카메라로 전환한다.
			if(IsLanding)
			{
				IsAfterApexCamNumDecided = false;
				CameraStep = 1;
				return;
			}
			
			if(IsBeforeApexCamNumDecided)
			{
				switch (BeforeApexCamNum)
				{
				//기본 시작 고정 카메라
				case 1:
					FixedCameraWork(Player);
					break;

				//(정방향) 추적 카메라	
				case 2:
					TraceCameraWork(Controller, Player,	DeltaTime);
					break;
				}
			}
			else
			{
				switch (BeforeApexCamNum)
				{
				case 1:
					InitFixedCameraWork(Controller, Player);
					break;

				case 2:
					InitTraceCameraWork(Controller, Player);
					break;
				}

				IsBeforeApexCamNumDecided = true;
			}
		}
		else if(CameraStep == 1)
		{ 
			//이미 카메라 유형이 결정된 경우
			if(IsAfterApexCamNumDecided)
			{
				float SideCameraRemainHeightCM = 0;
				float SideBellowCameraRemainHeightCM = 0;
				
				switch (AfterApexCamNum)
				{
				//(역방향) 지면 고정 카메라
				case 1:
					ReverseFixedCameraWork(Controller, Player);
					break;
					
				//측면 카메라
				case 2:
					if(FlagCameraToReverseCamera)
					{
						sideCameraPassTime = flyingTime - sideCameraBeginTime;
					}
					
					{
						if(IsLanding && IsAvailableThirdCamera(Controller, Player) && !IsHitBadPlace)
						{
#ifdef ENABLE_DEBUG_CAMERA
							int DebugThirdCameraIndex = PHYSICAL_MANAGER->GetCameraThreeIndex();
							bool IsDebugCameraExist = DebugThirdCameraIndex != 0;
							if(IsDebugCameraExist)
							{
								SG_LOG(Log, "[DebugCamera] Third Camera");
								
								AfterApexCamNum = 1;

								InitReverseFixedCameraWork(Controller, Player->GetActorLocation());
								ReverseFixedCameraWork(Controller, Player);

								return;
							}
#endif
							
							if(IsValid(SelectedSecondCameraRecord))
							{
								if(SelectedSecondCameraRecord->Third_Camera_Result.ToLower() == "land_fixed")
								{
									FlagCameraToReverseCamera = false;
							
									if(!IsPredictWrongLandingZone())
									{
										SG_LOG(Log, "SJW 002 Reverse Fixed Camera!!!!");
								
										AfterApexCamNum = 1;

										InitReverseFixedCameraWork(Controller, Player->GetActorLocation());
										ReverseFixedCameraWork(Controller, Player);
									}
								}
								else if(SelectedSecondCameraRecord->Third_Camera_Result == "shot_follow")
								{
									AfterApexCamNum = 3;
									
									InitTraceCameraWork(Controller, Player);
									TraceCameraWork(Controller, Player, DeltaTime);
								}
							}
							
						}
					}
					
					SideCameraWork(Player);
					break;
					
				//(정방향) 추적 카메라
				case 3:
					TraceCameraWork(Controller, Player, DeltaTime);
					break;

				//측면 45도 카메라 (구. 사이드 카메라)
				case 4:
					{
						if(IsLanding && IsCloseToGround(Player, SideCameraRemainHeightCM) && !IsPredictWrongLandingZone())
						{
							SG_LOG(Log, "SJW 002 Reverse Fixed Camera!!!!");
							AfterApexCamNum = 1;
							
							InitReverseFixedCameraWork(Controller, Player->GetActorLocation());
							ReverseFixedCameraWork(Controller, Player);
							return;
						}
					}
				//잘못 부딪혔을 때 (예. 나무) 예외처리 카메라
				case 5:
					{
						// SG_LOG(Log, "SJW 002 BadPlaceHit Camera Active");
						BadPlaceHitCameraWork(Player);
					}
					break;
				}
			}
			else
			{
				if(!IsDrivingMode) DecideAfterCamNum();

#ifdef ENABLE_DEBUG_CAMERA
				else
				{
					bool IsDebugCameraIndexExist = PHYSICAL_MANAGER->GetCameraTwoIndex() != 0;
					if(IsDebugCameraIndexExist)
					{
						DecideAfterCamNum();
					}
					else{ AfterApexCamNum = 3; }
				}
#endif
				
				switch (AfterApexCamNum)
				{
				case 1:
					InitReverseFixedCameraWork(Controller, Player->GetActorLocation());
					break;

				case 2:
					sideCameraBeginTime = flyingTime;
					InitSideCameraWork(Controller, Player);
					break;

				case 3:
					InitTraceCameraWork(Controller, Player);
					break;

				case 4:
					InitSideBellowCameraWork(Controller, Player);
					break;

				case 5:
					InitBadPlaceHitCameraWork(Controller, Player);
					break;
				}

				IsAfterApexCamNumDecided = true;
			}
		}
	}
}

//AfterApexCamNum을 결정한다.
void ASGCamera::DecideAfterCamNum()
{
	AfterApexCamNum = -1;
	
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if (nullptr == GameInst) return;

	APlayerController* Controller = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
	if (nullptr == Controller) return;

	ASGPlayerCharacter* Player = Cast<ASGPlayerCharacter>(Controller->GetPawn());
	if (nullptr == Player) return;

	ASGCourseMode* CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();

	FVector BallLocation = Player->GetActorLocation();
	FVector BallVelocity = Player->GetVelocity();
	FVector BowlerFreeViewLastPosition = Player->GetBowlerFreeView()->GetLastLocation();
	FVector MagnusVector = Player->GetTotalMagnusVector();

	if(IsHitBadPlace) {	AfterApexCamNum = 5; }
	
	float WindPower = 0;
	float OutWindDirection = 0;
	float windforcePowerRatio = 1.923937;
	FVector WindArrowDirection = FVector::ZeroVector;
	if(!IsDrivingMode)
	{
		CourseMode->GetWindInfo(WindPower, OutWindDirection);
		CourseMode->GetWindArrowDirection(WindArrowDirection);
	}
	WindArrowDirection.Normalize();
	
	float playerMass = 0.045930f;

	if (Player->GetCapsuleComponent()->IsSimulatingPhysics() == true)
	{
		playerMass = Player->GetCapsuleComponent()->GetMass();
	}

	FVector TargetWindForceVector = playerMass * (WindPower * 100.0f * windforcePowerRatio) * WindArrowDirection * 0.2f;

	float dotProduct = FVector::DotProduct(Player->GetVelocity(), TargetWindForceVector);
	if (dotProduct > 0)
	{
		// 순풍
		TargetWindForceVector *= 0.5f;		// 순풍은 역풍 영향의 절반
	}

	//예상 낙구 지점 연산
	PredictLandingPosition(BallLocation, BallVelocity, MagnusVector, BowlerFreeViewLastPosition, TargetWindForceVector, Player->GetMagnusForceCount());

	SelectedSecondCameraRecord = GetTargetSecondCameraPriority(1, Player);
	if(IsValid(SelectedSecondCameraRecord))
	{
		AfterApexCamNum = GetAfterApexCamNumFromStringValue(SelectedSecondCameraRecord->Camera_Result);
	}
}


void ASGCamera::InitFixedCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	StartFixedCamera->GetCamera()->FieldOfView = Player->GetFollowCamera()->FieldOfView;
	
	FVector CameraLocation = Player->GetActorLocation();
	StartFixedCamZPosFix(CameraLocation);

	StartFixedCamera->GetSpringArm()->SetRelativeRotation(FRotator(0, -1 * ShiftRotateAngle, 0));
	StartFixedCamera->SetActorLocation(CameraLocation);

	FVector CamToLookVector = Player->GetActorLocation() - StartFixedCamera->GetCamera()->GetComponentLocation();
	FVector ShiftedCamToLookVector = CamToLookVector.RotateAngleAxis(ShiftRotateAngle, FVector::ZAxisVector);
	ShiftedCamToLookVector.Z = 0;
	
	FRotator CurrentRotation = StartFixedCamera->GetCamera()->GetComponentRotation();
	FRotator Rot = FRotationMatrix::MakeFromX(ShiftedCamToLookVector).Rotator();

	FRotator ResultRot = FMath::Lerp(CurrentRotation, Rot, 0.5f);
	StartFixedCamera->GetCamera()->SetWorldRotation(ResultRot);
	
	Controller->SetViewTarget(StartFixedCamera);
}

void ASGCamera::FixedCameraWork(ASGPlayerCharacter* Player)
{
	FVector CamToLookVector = Player->GetActorLocation() - StartFixedCamera->GetCamera()->GetComponentLocation();
	FVector ShiftedCamToLookVector = CamToLookVector.RotateAngleAxis(ShiftRotateAngle, FVector::ZAxisVector);
	
	FRotator CurrentRotation = StartFixedCamera->GetCamera()->GetComponentRotation();
	FRotator Rot = FRotationMatrix::MakeFromX(ShiftedCamToLookVector).Rotator();

	FRotator ResultRot = FMath::Lerp(CurrentRotation, Rot, 0.5f);
	StartFixedCamera->GetCamera()->SetWorldRotation(ResultRot);
}


//(정방향) 추적 카메라 초기화
void ASGCamera::InitTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	if(IsInitTraceCamera) return;
	
	if(IsDrivingMode)
	{
		TraceCamera->SetActorLocation(Player->GetActorLocation());
	}
	else
	{
		float OffSetShiftRotateAngle = 0.0f;
		if(!IsTeeShot){ OffSetShiftRotateAngle = ShiftSecondShotOffSetAngle; }
		
		FVector CamToBallDirVector = Player->GetActorLocation() - TraceCamera->GetCamera()->GetComponentLocation();
		FVector ShiftedCamToBallDirVector = CamToBallDirVector.RotateAngleAxis(ShiftRotateAngle + OffSetShiftRotateAngle, FVector::ZAxisVector);
		ShiftedCamToBallDirVector.Z = 0;
		FRotator TargetRot = FRotationMatrix::MakeFromX(ShiftedCamToBallDirVector).Rotator();

		TraceCamera->GetCamera()->SetWorldRotation(TargetRot);
	}
	
	IsInitTraceCamera = true;
	
 	Controller->SetViewTarget(TraceCamera);
}

//측면 카메라 초기화
void ASGCamera::InitSideCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	FVector BallLocation = Player->GetActorLocation();
	FVector NewApexPredictPosition = FVector::ZeroVector;

	float ResultSideCameraLocationZRatio = IsValid(SelectedSecondCameraRecord) ?
		SelectedSecondCameraRecord->Side_Camera_Z_Position :
		SideCameraLocationZRatio;

	float ResultSideCameraLocationXYRatio = IsValid(SelectedSecondCameraRecord) ?
		SelectedSecondCameraRecord->Side_Camera_XY_Position :
		SideCameraLocationXYRatio;
	
	if(IsLanding)
	{
		float distX = predictLandingPosition.X - BallLocation.X;	//현재 공 위치와 예상 낙구지점 사이의 거리
		float distY = predictLandingPosition.Y - BallLocation.Y;
		float distZ = BallLocation.Z - predictLandingPosition.Z;	//현재 공 위치와 예상 낙구지점 사이의 거리
		
		//45도 측면 카메라의 위치를 APEX와 예측 낙구 지점 중간으로 잡는다.
		SG_LOG(Log, "SJW 001 InitSideCameraWork called after apex");
		NewApexPredictPosition = FVector(
			BallLocation.X + distX * ResultSideCameraLocationXYRatio,
			BallLocation.Y + distY * ResultSideCameraLocationXYRatio,
			predictLandingPosition.Z + distZ * ResultSideCameraLocationZRatio
		);
	}
	//아직 상승중
	else
	{
		float distX = predictLandingPosition.X - predictApexPosition.X;	//APEX 지점과 예상 낙구지점 사이의 거리
		float distY = predictLandingPosition.Y - predictApexPosition.Y;
		float distZ = predictApexPosition.Z - predictLandingPosition.Z;	//APEX 지점과 예상 낙구지점 사이의 거리

		SG_LOG(Log, "SJW 001 InitSideCameraWork distX %f", distX);
		SG_LOG(Log, "SJW 001 InitSideCameraWork distZ %f", distZ);
		
		SG_LOG(Log, "SJW 001 InitSideCameraWork called before apex");
		NewApexPredictPosition = FVector(
			predictApexPosition.X + distX * ResultSideCameraLocationXYRatio,
			predictApexPosition.Y + distY * ResultSideCameraLocationXYRatio,
			predictLandingPosition.Z + distZ * ResultSideCameraLocationZRatio
		);
	}
	
	GroundCheck(SideCamera->GetCamera()->GetComponentLocation(), sideCameraLocation);

	//예외 상황 방어코드
	if((predictApexPosition.X == 0) && (predictApexPosition.Y == 0))
	{
		sideCameraLocation = BallLocation;
	}
	else
	{
		sideCameraLocation = FVector(NewApexPredictPosition.X, NewApexPredictPosition.Y, NewApexPredictPosition.Z);
	}
	
	SG_LOG(Log, "SJW 110 ballLocation : %s", *BallLocation.ToString());
	SG_LOG(Log, "SJW 110 predictApexPosition : %s", *predictApexPosition.ToString());
	SG_LOG(Log, "SJW 110 NewApexPredictPosition : %s", *NewApexPredictPosition.ToString());
	SG_LOG(Log, "SJW 110 sideCameraLocation : %s", *sideCameraLocation.ToString());

	FVector MoveDirection = BallLocation - predictLandingPosition;
	FVector CrossProduct = FVector::CrossProduct(BeginForwardDir, holecupLocation - startLocation);
	FVector SideDir = MoveDirection.RotateAngleAxis(90, FVector::ZAxisVector).GetSafeNormal();
	SideDir.Z = 0;
	
	// SG_LOG(Log, "SJW 110 CrossProduct %s", *CrossProduct.ToString());
	bool IsCameraRightCondition = CrossProduct.Z > 0;
	
	bool ConditionResult = IsFineConditionToSideCamera(sideSpinRate, launchSideAngleInDegree);

#ifdef ENABLE_DEBUG_CAMERA
	bool IsDebugIndexExist = PHYSICAL_MANAGER->GetCameraTwoIndex() != 0;
	if(IsDebugIndexExist) ConditionResult = true;
#endif
	
	if(ConditionResult)
	{
		SG_LOG(Log, "SJW 110 ConditionResult true");
		IsCameraRightCondition = sideSpinRate > 0;
	}
	else
	{
		SG_LOG(Log, "SJW 110 ConditionResult false");
	}

	float ResultSideCameraDist = IsValid(SelectedSecondCameraRecord) ?
				SelectedSecondCameraRecord->Side_Camera_Distance * 100 :
				SideCameraDist;

	if (Player->GetInOutFanceBoundarieWhite())
	{
		if(IsCameraRightCondition)
		{
			SG_LOG(Log, "SJW 110 normal right camera case aaa");
			
			sideCameraLocation += SideDir * ResultSideCameraDist;
			
			bool IsOB = IsOBZPos(sideCameraLocation);
			if(IsOB)
			{
				//side spin이 1000 이상인 경우 apex 지점 방향으로 이동하면서 holebox가 감지되는 지점까지 카메라를 당긴다.
				if(FMath::Abs(sideSpinRate) > 1000)
				{
					FVector ResultLoc = sideCameraLocation;
					GetFairWayPosFromOBToApex(sideCameraLocation, NewApexPredictPosition, ResultLoc);
					sideCameraLocation = ResultLoc;
					sideCameraLocation -= SideDir * 500;
				}
				else
				{
					sideCameraLocation -= SideDir * ResultSideCameraDist * 0.2f;
				}
			}
		}
		else
		{
			SG_LOG(Log, "SJW 110 normal left camera case aaa");
			sideCameraLocation -= SideDir * ResultSideCameraDist;
	
			//Side Camera가 OB영역까지 이동했으면 FairWay 안쪽으로 재보정 해준다.
			bool IsOB = IsOBZPos(sideCameraLocation);
			if(IsOB)
			{
				//side spin이 1000 이상인 경우 apex 지점 방향으로 이동하면서 holebox가 감지되는 지점까지 카메라를 당긴다.
				if(FMath::Abs(sideSpinRate) > 1000)
				{
					FVector ResultLoc = sideCameraLocation;
					GetFairWayPosFromOBToApex(sideCameraLocation, NewApexPredictPosition, ResultLoc);
					sideCameraLocation = ResultLoc;
					sideCameraLocation += SideDir * 500;
				}
				else
				{
					sideCameraLocation += SideDir * ResultSideCameraDist * 0.2f;
				}
			}
		}
	}
	else
	{
		FVector FromSideCameraToLeftSafeAreaPos = FVector::ZeroVector;
		FVector FromSideCameraToRightSafeAreaPos = FVector::ZeroVector;
		GetLeftRightFairWayPos(
			MoveDirection,
			NewApexPredictPosition,
			FromSideCameraToLeftSafeAreaPos,
			FromSideCameraToRightSafeAreaPos
		);
	
		if(FromSideCameraToLeftSafeAreaPos != FVector::ZeroVector)
		{
			sideCameraLocation = FVector(FromSideCameraToLeftSafeAreaPos.X, FromSideCameraToLeftSafeAreaPos.Y, predictApexPosition.Z);
			sideCameraLocation -= SideDir * 500;
		}
		else if(FromSideCameraToRightSafeAreaPos != FVector::ZeroVector)
		{
			sideCameraLocation = FVector(FromSideCameraToRightSafeAreaPos.X, FromSideCameraToRightSafeAreaPos.Y, predictApexPosition.Z);
			sideCameraLocation += SideDir * 500;
		}
		//예외 상황 [현재 수직 아래도 좌우로도 홀박스 감지되지 않은 경우 ex. 호수 가로질러 가는 코스, 너무 멀리 OB를 벗어나서 가는 경우]
		else
		{
			if(IsCameraRightCondition)
			{
				SG_LOG(Log, "SJW 007 normal right camera case bbb");
				sideCameraLocation -= SideDir * ResultSideCameraDist;
			}
			else
			{
				SG_LOG(Log, "SJW 007 normal left camera case bbb");
				sideCameraLocation += SideDir * ResultSideCameraDist;
			}
		}
	}
	
	SideCamera->SetActorLocation(sideCameraLocation);

	FRotator Rot = FRotationMatrix::MakeFromX(Player->GetActorLocation() - SideCamera->GetActorLocation()).Rotator();
	SideCamera->SetActorRotation(Rot);
	
	Controller->SetViewTarget(SideCamera);
}

bool ASGCamera::IsFineConditionToSideCamera(float SideSpinRate, float SideAngleInDegree)
{
	float AbsSideSpinRate = FMath::Abs(SideSpinRate);
	float AbsSideAngleInDegree = FMath::Abs(SideAngleInDegree);

	if(AbsSideAngleInDegree > 90) AbsSideAngleInDegree = 180 - AbsSideAngleInDegree;

	SG_LOG(Log, "SJW 007 AbsSideAngleInDegree %f", AbsSideAngleInDegree);
	
	if((AbsSideSpinRate >= 500) && (AbsSideSpinRate <= 1500))
	{
		if((AbsSideAngleInDegree >= 5) && (AbsSideAngleInDegree <= 15))
		{
			SG_LOG(Log, "SJW 007 IsFineConditionToSideCamera true case aaa");
			return true;
		}
	}
	else if((AbsSideSpinRate >= 0) &&(AbsSideSpinRate < 500))
	{
		if((AbsSideAngleInDegree >= 0) && (AbsSideAngleInDegree <= 10))
		{
			SG_LOG(Log, "SJW 007 IsFineConditionToSideCamera true case bbb");
			return true;
		}
	}
	
	return false;
}


void ASGCamera::GetFairWayPosFromOBToApex(FVector From, FVector To, FVector &ResultLoc)
{
	float Offset = 500;

	To.Z = From.Z;
	FVector Dir = To - From;
	Dir.Normalize();
		
	for(int i = 1; i <= 20; i++)
	{
		FVector NextLocation = From + Dir * i * Offset;

		SG_LOG(Log, "GetFairWayPosFromLeftOBToApex NextLocation %s", *NextLocation.ToString());
		
		FVector Start = NextLocation + FVector(0, 0, 100);
		FVector End = NextLocation - FVector(0, 0, 10000);

		FHitResult HitResult;
		EStimpMeterType::Type SurfaceType;

		bool IsInsideBoundary = CheckCurrentHole(Start, End, HitResult, SurfaceType);

		if (IsInsideBoundary)
		{
			//HoleBox 영역에서 FairWay 영역을 다시 세부적으로 찾는다.
			for (int j = 1; j <= 20; j++)
			{
				FVector NextLocation2 = NextLocation + j * 200;

				FVector CheckDetail1 = NextLocation2 + FVector(0, 0, 1000);
				FVector CheckDetail2 = NextLocation2 - FVector(0, 0, 10000);

				FHitResult DetailHitResult;
				EStimpMeterType::Type DetailSurfaceType;

				CheckCurrentHole(CheckDetail1, CheckDetail2, DetailHitResult, DetailSurfaceType);

				int32 DetectedSurfaceType = (int32)DetailSurfaceType;;

				if ((DetectedSurfaceType > 0) && (DetectedSurfaceType < 8))
				{
					ResultLoc = NextLocation2;
					SG_LOG(Log, "SJW GetFairWayPosFromOBToApex detected case 001");
					return;
				}
			}

			ResultLoc = FVector(HitResult.ImpactPoint.X, HitResult.ImpactPoint.Y, From.Z);
			SG_LOG(Log, "SJW GetFairWayPosFromOBToApex detected case 002");
			return;
		}
	}
}

//(역방향) 지면 고정 카메라 초기화
void ASGCamera::InitReverseFixedCameraWork(APlayerController* Controller, FVector BallLocation)
{
	FVector MoveDirection = predictLandingPosition - BallLocation;
	FVector SideDir = MoveDirection.RotateAngleAxis(90, FVector::ZAxisVector);
	SideDir.Normalize();

	float ResultReverseFixedCameraSideDist = IsValid(SelectedSecondCameraRecord) ?
		SelectedSecondCameraRecord->Land_Fixed_Distance * 100:
		ReverseFixedCameraSideDist;
	
	reverseLandFixedCameraLocation = predictLandingPosition + SideDir * ResultReverseFixedCameraSideDist;
	
	FHitResult HitResult;

	FVector Start = reverseLandFixedCameraLocation + FVector(0, 0, 10000);
	FVector End = reverseLandFixedCameraLocation - FVector(0, 0, 10000);

	float ResultReverseFixedCameraHeight = IsValid(SelectedSecondCameraRecord) ?
		SelectedSecondCameraRecord->Land_Fixed_Height * 100:
		ReverseFixedCameraHeight;
	
	bool bIsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
	if (bIsGround)
	{
		SG_LOG(Log, "Ground detected");
		reverseLandFixedCameraLocation.Z = HitResult.ImpactPoint.Z + ResultReverseFixedCameraHeight;
	}
	else
	{
		SG_LOG(Log, "Ground not detected");
		reverseLandFixedCameraLocation.Z += ResultReverseFixedCameraHeight;
	}

	ReverseLandFixedCamera->SetActorLocation(reverseLandFixedCameraLocation);

	RequestClosestDistDecalScaleChange(
		70,
		70,
		ReverseLandFixedCamera->GetCamera()->GetComponentLocation()
	);

	ReverseLandFixedCamera->GetSpringArm()->bEnableCameraRotationLag = false;
	ReverseLandFixedCamera->GetSpringArm()->bEnableCameraLag = false;
	
	FVector Loc = BallLocation;
	
	// 카메라 위치 지면 위로 보정
	GroundCheck(ReverseLandFixedCamera->GetCamera()->GetComponentLocation(), Loc);

	FRotator Rot = FRotationMatrix::MakeFromX(BallLocation - ReverseLandFixedCamera->GetActorLocation()).Rotator();
	ReverseLandFixedCamera->SetActorRotation(Rot);
	
	Controller->SetViewTarget(ReverseLandFixedCamera);
}

//(역방향) 추적 카메라 초기화
void ASGCamera::InitReverseTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	Controller->SetViewTarget(ReverseCamera);
	ReverseTraceCameraWork();
}

//45도 측면 카메라 초기화
void ASGCamera::InitSideBellowCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	FVector BallLocation = Player->GetActorLocation();
	FVector NewPosition = FVector::ZeroVector;

	float ResultSideCameraLocationZRatio = IsValid(SelectedSecondCameraRecord) ?
		SelectedSecondCameraRecord->Side_Camera_Z_Position :
	SideCameraLocationZRatio;
	
	if(IsLanding)
	{
		float distX = predictLandingPosition.X - BallLocation.X;	//현재 공 위치와 예상 낙구지점 사이의 거리
		float distY = predictLandingPosition.Y - BallLocation.Y;
		float distZ = BallLocation.Z - predictLandingPosition.Z;	//현재 공 위치와 예상 낙구지점 사이의 거리
		
		//45도 측면 카메라의 위치를 APEX와 예측 낙구 지점 중간으로 잡는다.
		SG_LOG(Log, "SJW 001 InitSideBellowCameraWork called after apex");
		NewPosition = FVector(
			BallLocation.X + distX * SideBellowCameraLocationXYRatio,
			BallLocation.Y + distY * SideBellowCameraLocationXYRatio,
			predictLandingPosition.Z + distZ * ResultSideCameraLocationZRatio
		);
	}
	//아직 상승중
	else
	{
		float distX = predictLandingPosition.X - predictApexPosition.X;	//APEX 지점과 예상 낙구지점 사이의 거리
		float distY = predictLandingPosition.Y - predictApexPosition.Y;
		float distZ = predictApexPosition.Z - predictLandingPosition.Z;	//APEX 지점과 예상 낙구지점 사이의 거리

		SG_LOG(Log, "SJW 001 InitSideBellowCameraWork distX %f", distX);
		SG_LOG(Log, "SJW 001 InitSideBellowCameraWork distZ %f", distZ);
		
		SG_LOG(Log, "SJW 001 InitSideBellowCameraWork called before apex");
		NewPosition = FVector(
			predictApexPosition.X + distX * SideBellowCameraLocationXYRatio,
			predictApexPosition.Y + distY * SideBellowCameraLocationXYRatio,
			predictLandingPosition.Z + distZ * ResultSideCameraLocationZRatio
		);
	}

	sideCameraLocation = NewPosition;
	SideCamera->SetActorLocation(sideCameraLocation);
	
	FRotator Rot = FRotationMatrix::MakeFromX(Player->GetActorLocation() - SideCamera->GetActorLocation()).Rotator();
	SideCamera->SetActorRotation(Rot);

	Controller->SetViewTarget(SideCamera);
}

void ASGCamera::InitPuttingTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	float FollowCameraFOV = Player->GetFollowCamera()->FieldOfView + 5;
	
	PuttingTraceCamera->GetCamera()->FieldOfView = FollowCameraFOV;
	
	PuttingTraceCameraDir = Player->GetVelocity().GetSafeNormal();
	PuttingTraceCameraDir.Z = 0;
	
	FVector BallLocation = Player->GetActorLocation();

	float BackwardLength = 220;
	FVector TargetCameraLocation = BallLocation + (-1 * PuttingTraceCameraDir * BackwardLength) + FVector(0, 0, 50);

	FVector TraceCameraLocation = Player->GetFollowCamera()->GetComponentLocation();
	
	PuttingTraceCamera->GetCamera()->SetWorldLocation(TargetCameraLocation);
	PuttingTraceCamera->GetCamera()->SetWorldRotation(
		TraceCameraLocation.RotateAngleAxis(-0.2f, FVector::ZAxisVector).Rotation()
	);

	Controller->SetViewTarget(PuttingTraceCamera);
}

void ASGCamera::PuttingTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime)
{
	PuttingPassTime += DeltaTime;
	
	if(!CanTraceCamera) return;

	FVector CurrentCameraLocation = PuttingTraceCamera->GetCamera()->GetComponentLocation();
	FVector BallLocation = Player->GetActorLocation();
	
	if(!IsHoleIn && Player->GetBallMoving())
	{
		FVector NewCameraLocation = BallLocation - PuttingTraceCameraDir * 220 + FVector(0, 0, 60);

		FVector LerpCameraLocation = FMath::Lerp(CurrentCameraLocation, NewCameraLocation, 0.5f);
		PuttingTraceCamZPosFix(LerpCameraLocation);
		
		PuttingTraceCamera->GetCamera()->SetWorldLocation(LerpCameraLocation);
	}
	
	if(CanRotateTraceCamera)
	{
		FVector LookAt = Player->GetActorLocation() + FVector(0, 0, 60);
		FVector CamToLookVector = LookAt - PuttingTraceCamera->GetCamera()->GetComponentLocation();
		FVector ShiftedCamToLookVector = CamToLookVector.RotateAngleAxis(ShiftRotateAngle + ShiftPuttingShotOffSetAngle, FVector::ZAxisVector);
		// ShiftedCamToLookVector.Z = 0;

		FRotator CurrentRot = TraceCamera->GetCamera()->GetComponentRotation();
		FRotator TargetRot = FRotationMatrix::MakeFromX(ShiftedCamToLookVector).Rotator();
		
		FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, 0.5f);

		PuttingTraceCamera->GetCamera()->SetWorldRotation(ResultRot);
	}
}


void ASGCamera::PuttingZoomCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	float BallToHoleDist = FVector::Dist2D(holecupLocation, Player->GetActorLocation());
	
	FVector PuttingTraceCameraLocation = PuttingTraceCamera->GetCamera()->GetComponentLocation();
	if(!IsPuttingZoomCameraSetLocationRotation)
	{
		PuttingZoomCamera->GetCamera()->SetWorldLocation(PuttingTraceCameraLocation);
		IsPuttingZoomCameraSetLocationRotation = true;
	}

	float CurBallToHoleDist = FVector::Dist2D(Player->GetActorLocation(), holecupLocation);
	CanRotateTraceCamera = CurBallToHoleDist > 30;

	if(CanRotateTraceCamera)
	{
		FRotator CurrentRot = PuttingTraceCamera->GetCamera()->GetComponentRotation();
		FRotator TargetRot = FRotationMatrix::MakeFromX(Player->GetActorLocation() - PuttingTraceCameraLocation).Rotator();

		FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, 0.5f);
		PuttingZoomCamera->GetCamera()->SetWorldRotation(ResultRot);
	}
	
	//공이 일정 이상 떨어져 있고, 공이 홀에 점점 가까워지는중
	if(CurBallToHoleDist < PrevBallToHoleDist)
	{
		// SG_LOG(Log, "SJW 800 PuttingTraceCameraWork....111");

		float ResultPuttingCameraZoomInMin = IsValid(SelectedPuttingCameraRecord) ?
			SelectedPuttingCameraRecord->Zoom_Camera_Ratio : PuttingCameraZoomInMin;
		
		if((PuttingZoomCamera->GetCamera()->FieldOfView > ResultPuttingCameraZoomInMin) && CanRotateTraceCamera)
		{
			PuttingZoomCamera->GetCamera()->FieldOfView -= PuttingCameraZoomInSpeed;
		}
	}
	//공이 멀어지는 중
	else if((CurBallToHoleDist >= PrevBallToHoleDist) && CanRotateTraceCamera && (CurBallToHoleDist > 300))
	{
		// SG_LOG(Log, "SJW 800 PuttingTraceCameraWork....222");
		
		if(PuttingZoomCamera->GetCamera()->FieldOfView < 80)
		{
			PuttingZoomCamera->GetCamera()->FieldOfView += PuttingCameraZoomOutSpeed;
		}
	}
	
	if(CurBallToHoleDist < SG_HOLE_CUP_RADIUS)
	{
		CanRotateTraceCamera = false;
	}

	if(Controller->GetViewTarget() != PuttingZoomCamera)
	{
		Controller->SetViewTarget(PuttingZoomCamera);
	}
	
	PrevBallToHoleDist = BallToHoleDist;
}


//(정방향) 추적 카메라 Tick 단위 처리
void ASGCamera::TraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime)
{
	if(IsDrivingMode) {	DrivingModeTraceCameraSubWork(Controller, Player, DeltaTime); }	//연습장인 경우
	else { CourseModeTraceCameraSubWork(Controller, Player, DeltaTime); }					//코스인 경우
	
}


//(정방향) [코스 모드] 추적 카메라 하위 Work 처리 
void ASGCamera::CourseModeTraceCameraSubWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime)
{
	if(CameraStep == 0)	{ IS_VALID_RETURN(SelectedFirstCameraRecord); }
	else if(CameraStep == 1) { IS_VALID_RETURN(SelectedSecondCameraRecord); }

	if (!TraceCamera->GetSpringArm()->bEnableCameraLag)
		TraceCamera->GetSpringArm()->bEnableCameraLag = true;

	if (!TraceCamera->GetSpringArm()->bEnableCameraRotationLag)
		TraceCamera->GetSpringArm()->bEnableCameraRotationLag = true;

	FVector BallLocation = Player->GetActorLocation();
	FVector CurrentCameraLocation = TraceCamera->GetCamera()->GetComponentLocation();

	//샷 직후 추적카메라 Step인 경우
	if(CameraStep == 0)
	{
		float BallToCameraDist = FVector::Dist(BallLocation, CurrentCameraLocation);
		float TargetSheetFollowCamDist = SelectedFirstCameraRecord->Follow_Camera_Distance * 100;
		
		//공이 상승중이고, 공과 카메라 사이 거리가 시트의 기준 거리보다 가까운 경우 따라가지 않는다.
		if(!IsLanding && !IsPutting)
		{
			bool IsCameraCloseToBall = BallToCameraDist < TargetSheetFollowCamDist;
			CanTraceCamera = !IsCameraCloseToBall;
		}
		else
		{
			CanTraceCamera = true;
		}
		
		// SG_LOG(Log, "SJW BallToCameraDist %f", BallToCameraDist);
		// SG_LOG(Log, "SJW TargetSheetFollowCamDist %f", TargetSheetFollowCamDist);
	}

	#pragma region FOVChange
	if(IsTeeShot && !IsPutting && (launchAngle > 7))
	{
		float CurrentFOV = TraceCamera->GetCamera()->FieldOfView;
		if(CurrentFOV <= 80) { TraceCamera->GetCamera()->FieldOfView += 1; }
	}
	#pragma endregion

	#pragma region SocketOffsetChange
	if((flyingTime >= 0.3f) && IsTeeShot && !IsPutting)
	{
		FVector CurrentSocketOffset = TraceCamera->GetSpringArm()->SocketOffset;
		FVector TargetSocketOffset = FVector(0, 0, -100);
				
		FVector LerpSocketOffset = FMath::Lerp(CurrentSocketOffset, TargetSocketOffset, 0.5f);
		TraceCamera->GetSpringArm()->SocketOffset = LerpSocketOffset;
	}
	#pragma endregion

	#pragma region CameraTrace
	if(CanTraceCamera)
	{
		FVector PrevCameraLocation = CurrentCameraLocation;
		//공이 지면에 닿은 이후부터 카메라 고도 유지
		// if(IsBallImpacted) { CurrentCameraLocation.Z = TraceCameraPrevLocationAfterBallImpacted.Z; }
		TraceCamZPosFix(BallLocation, CurrentCameraLocation);

		//순서 중요
		FVector TargetLerpLocation = FMath::Lerp(CurrentCameraLocation, PrevCameraLocation, 0.5f);
		TraceCamera->GetCamera()->SetWorldLocation(TargetLerpLocation);
		
		TraceCamera->SetActorLocation(BallLocation);
	}
	#pragma endregion

	#pragma region PuttingCheck
	if(IsPutting)
	{
		Player->GetCameraBoom()->SetRelativeLocation(FVector::ZeroVector);
		PuttingTraceCameraWork(Controller, Player, DeltaTime);
		TraceCamera->GetCamera()->SetRelativeLocation(FVector(0, 0, 100));
	}
#pragma endregion
	
	#pragma region CameraRotate
	if(CanRotateTraceCamera)
	{
		bool IsOutSafeScreenArea = false;
		bool UpperUnSafe = false;
		bool BottomUnsafe = false;
		
		float OffSetRotateAngle = 0.0f;
		if(!IsTeeShot) { OffSetRotateAngle = ShiftSecondShotOffSetAngle; }
		
		FVector CamToBallDirVector = Player->GetActorLocation() - TraceCamera->GetCamera()->GetComponentLocation();
		FVector ShiftedCamToBallDirVector = CamToBallDirVector.RotateAngleAxis(ShiftRotateAngle + OffSetRotateAngle, FVector::ZAxisVector);
		FRotator TargetRot = FRotationMatrix::MakeFromX(ShiftedCamToBallDirVector).Rotator();
		
		FRotator CurrentRot = TraceCamera->GetCamera()->GetComponentRotation();
		TargetRot.Yaw = CurrentRot.Yaw;
		
		if(IsPutting)
		{
			FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, 0.5f);
			TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
		}
		else
		{
			if(IsBallImpacted)
			{
				if(IsBadImpacted)
				{
					FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, 0.5f);
					TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
				}
				else
				{
					TargetRot = FRotationMatrix::MakeFromX(ShiftedCamToBallDirVector).Rotator();
					
					FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, 0.02f);
					TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
				}
			}
			else
			{
				if(flyingTime >= 0.1f)
				{
					if(IsLanding)
					{
						if(BottomUnsafe && !IsBottomOutSafeAreaDetected)
						{
							// SG_LOG(Log, "SJW 777 Descending....And BottomUnsafe area detected");
							IsBottomOutSafeAreaDetected = true;
						}

						float Alpha = IsBottomOutSafeAreaDetected ? 0.4f : 0.1f;
					
						FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, Alpha);
						TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
					}
					else
					{
						if(UpperUnSafe && !IsUpperOutSafeAreaDetected)
						{
							// SG_LOG(Log, "SJW 777 Ascending....And UpperUnSafe area detected");
							IsUpperOutSafeAreaDetected = true;	
						}

						float Alpha = IsUpperOutSafeAreaDetected ? 0.4f : 0.1f;
					
						FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, Alpha);
						TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
					}
				}
			}
		}
	}
	#pragma endregion

	#pragma region DecalScaleChange
	if(IsLanding && Player->GetFlyingCheck())
	{
		if(CanChangeDecalScaleHeight(Player->GetActorLocation()))
		{
			RequestClosestDistDecalScaleChange(90, 70, Player->GetActorLocation());
		}
	}
#pragma endregion
}

//(정방향) [연습장 모드] 추적 카메라 하위 Work 처리
void ASGCamera::DrivingModeTraceCameraSubWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime)
{
	if (!TraceCamera->GetSpringArm()->bEnableCameraLag)
		TraceCamera->GetSpringArm()->bEnableCameraLag = true;

	if (!TraceCamera->GetSpringArm()->bEnableCameraRotationLag)
		TraceCamera->GetSpringArm()->bEnableCameraRotationLag = true;
	
#pragma region CameraTrace
	if(CanTraceCamera)
	{
		FVector BallLocation = Player->GetActorLocation();
		FVector CurrentCameraLocation = TraceCamera->GetCamera()->GetComponentLocation();
				
		FVector PrevCameraLocation = CurrentCameraLocation;

		FVector TargetLerpLocation = FMath::Lerp(CurrentCameraLocation, PrevCameraLocation, 0.5f);
		TraceCamera->GetCamera()->SetWorldLocation(TargetLerpLocation);
		
		TraceCamera->SetActorLocation(BallLocation);
#pragma endregion
	}

	#pragma region CameraRotate
	if(CanRotateTraceCamera)
	{
		if(IsBallImpacted)
		{
			traceCameraBreakTime += DeltaTime;

			FRotator CurrentRot = TraceCamera->GetCamera()->GetComponentRotation();
			FRotator TargetRot = FRotationMatrix::MakeFromX(Player->GetActorLocation() - TraceCamera->GetCamera()->GetComponentLocation()).Rotator();
			
			FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, 0.01f);
			TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
			
			CanTraceCamera = false;

			if (traceCameraBreakTime > 1.0f) {
				ActiveEndShotWaitCamera();
			}
		}
		else
		{
			FRotator CurrentRot = TraceCamera->GetCamera()->GetComponentRotation();
			FRotator TargetRot = FRotationMatrix::MakeFromX(Player->GetActorLocation() - TraceCamera->GetCamera()->GetComponentLocation()).Rotator();
		
			FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, 0.5f);
			TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
		}
	}
	#pragma endregion
}

//(역방향) 지면 고정 카메라 Tick 단위 처리
void ASGCamera::ReverseFixedCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	bool PrevRotationLag = ReverseLandFixedCamera->GetSpringArm()->bEnableCameraRotationLag;
	bool PrevLag = ReverseLandFixedCamera->GetSpringArm()->bEnableCameraLag;

	if(!PrevRotationLag) { ReverseLandFixedCamera->GetSpringArm()->bEnableCameraRotationLag = true; }
	if(!PrevLag){ ReverseLandFixedCamera->GetSpringArm()->bEnableCameraLag = true; }
	
	BackTraceDir2D = FVector2D(Player->GetVelocity()).GetSafeNormal();
	
	float ResultHeight = IsValid(SelectedSecondCameraRecord) ?
		SelectedSecondCameraRecord->Land_Fixed_Height * 100:
		Player->GetActorLocation().Z * BackTraceHeightRate;

	float ResultBacktraceDist = IsValid(SelectedSecondCameraRecord) ?
		SelectedSecondCameraRecord->Land_Fixed_Distance * 100 :	6000;

	FVector BallForwardVector = Player->GetActorForwardVector().GetSafeNormal();
	BallForwardVector *= 500;
	
	FVector NewCameraLocation = predictLandingPosition
	+ FVector(ResultBacktraceDist, ResultBacktraceDist, 0)
	+ BallForwardVector;
	
	NewCameraLocation.Z = ResultHeight;

	// 카메라 위치 지면 위로 보정
	GroundCheck(ReverseLandFixedCamera->GetCamera()->GetComponentLocation(), NewCameraLocation);

	FRotator Rot = FRotationMatrix::MakeFromX(Player->GetActorLocation() - ReverseLandFixedCamera->GetActorLocation()).Rotator();

	//Note. OB에서 공을 치는 경우, GetFlyingCheck 값이 항상 false 인 문제가 있어 우회함
	if(IsBallImpacted)
	{
		if(ReverseLandFixedCamera->GetSpringArm()->bEnableCameraRotationLag == false)
		{
			ReverseLandFixedCamera->GetSpringArm()->bEnableCameraRotationLag = true;
		}
		ReverseLandFixedCamera->GetSpringArm()->CameraRotationLagSpeed = 10;

		//지면에 닿은 이후 수직 움직임 차단
		Rot.Pitch = 0;
	}
	
	ReverseLandFixedCamera->SetActorRotation(Rot);
}

//측면 카메라 Tick 단위 처리
void ASGCamera::SideCameraWork(ASGPlayerCharacter* Player)
{
	FRotator Rot = FRotationMatrix::MakeFromX(Player->GetActorLocation() - SideCamera->GetActorLocation()).Rotator();
	SideCamera->SetActorRotation(Rot);
}

//측면 45도 사이드 카메라 Tick 단위 처리
void ASGCamera::SideBellowCameraWork(ASGPlayerCharacter* Player)
{
	FVector ResultLocation = SideCamera->GetCamera()->GetComponentLocation();
	GroundCheck(SideCamera->GetActorLocation(), ResultLocation);
	
	SideCamera->SetActorLocation(ResultLocation);
	
	FRotator Rot = FRotationMatrix::MakeFromX(Player->GetActorLocation() - SideCamera->GetActorLocation()).Rotator();
	SideCamera->SetActorRotation(Rot);
}

//(역방향) 추적 카메라 Tick 단위 처리
void ASGCamera::ReverseTraceCameraWork()
{
	
}

bool ASGCamera::IsCloseToGround(ASGPlayerCharacter* Player, float TargetHeight)
{
	FHitResult HitResult;
	
	FVector BallLocation = Player->GetActorLocation();
	FVector StartPoint = BallLocation;
	FVector EndPoint = BallLocation - FVector(0, 0, TargetHeight - SG_GOLF_BALL_DIAMETER);

	bool IsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, StartPoint, EndPoint, this);
	return IsGround;
}

bool ASGCamera::GetIsUnder100(ASGPlayerCharacter* Player, FVector HoleCupLoc)
{
	bool IsUnder100 = FVector::Dist(Player->GetActorLocation(), HoleCupLoc) < 10000;
	return IsUnder100;
}


bool ASGCamera::IsPredictWrongLandingZone()
{
	if (predictLandingPosition == FVector::ZeroVector)
		return true;

	FVector Start = predictLandingPosition + FVector(0, 0, 10000);
	FVector End = predictLandingPosition - FVector(0, 0, 10000);

	FHitResult HitResult;
	EStimpMeterType::Type SurfaceType;

	bool IsInsideBoundary = CheckCurrentHole(Start, End, HitResult, SurfaceType);

	if (IsInsideBoundary)
	{
		int32 SurfaceTypeNum = (int32)SurfaceType;

		if ((SurfaceTypeNum == 0) || (SurfaceTypeNum >= 7))
		{
			return true;
		}

		SG_LOG(Log, "No wrong case detected...[Success]");
		SG_LOG(Log, "Detected Surface Type... %i", SurfaceTypeNum);
		return false;
	}

	SG_LOG(Log, "PredictWrongLandingZone different Hole");
	return true;
}

bool ASGCamera::CheckCurrentHole(FVector InStart, FVector InEnd, FHitResult& OutHitResult, EStimpMeterType::Type& OutSurfaceType)
{
	bool IsInsideBoundary = false;

	OutSurfaceType = EStimpMeterType::OB;

	const bool bIsGround = USGGroundChecker::LineTraceGroundCheck(OutHitResult, InStart, InEnd, this);

	if (bIsGround)
	{
		ASGCourseMode* const CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();

		if (IsValid(CourseMode))
		{
			IsInsideBoundary = CourseMode->CheckFanceBoundarieInWhite(OutHitResult.ImpactPoint);

			ASGPixelEdgeFinder* const PixelEdgeFinder = CourseMode->GetPixelEdgeFinder();

			if (IsValid(PixelEdgeFinder))
			{
				OutSurfaceType = PixelEdgeFinder->GetFindCurStimpMeterType(OutHitResult.ImpactPoint);
			}
		}
	}

	return IsInsideBoundary;
}


void ASGCamera::LandingCheck(float DeltaTime)
{
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if (nullptr == GameInst) return;

	APlayerController* Controller = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
	if (nullptr == Controller) return;

	ASGPlayerCharacter* Player = Cast<ASGPlayerCharacter>(Controller->GetCharacter());
	if (nullptr == Player) return;

	if (oldLocation.Z > Player->GetActorLocation().Z)
	{
		IsLanding = true;
	}
	else
	{
		IsLanding = false;
	}
	oldLocation = Player->GetActorLocation();
}

void ASGCamera::InitCameraRelativeRotation()
{
	TraceCamera->GetCamera()->SetRelativeRotation(FRotator::ZeroRotator);
	StartFixedCamera->GetCamera()->SetRelativeRotation(FRotator::ZeroRotator);
	ReverseCamera->GetCamera()->SetRelativeRotation(FRotator::ZeroRotator);
	GreenCamera->GetCamera()->SetRelativeRotation(FRotator::ZeroRotator);
	SideCamera->GetCamera()->SetRelativeRotation(FRotator::ZeroRotator);
}

void ASGCamera::InitCameraLag()
{
	TraceCamera->GetSpringArm()->bEnableCameraLag = false;
	TraceCamera->GetSpringArm()->bEnableCameraRotationLag = true;
	ReverseCamera->GetSpringArm()->bEnableCameraLag = false;
	
	ReverseLandFixedCamera->GetSpringArm()->bEnableCameraRotationLag = false;
}

void ASGCamera::SetCurFollowCam()
{
	ASGBaseGameMode* const gameMode = Cast<ASGBaseGameMode>(GetWorld()->GetAuthGameMode());
	if (IsValid(gameMode)
		&& IsValid(gameMode->FindPlayerCharacterCurrentTurn())
		&& IsValid(gameMode->FindPlayerCharacterCurrentTurn()->GetFollowCamera()))
	{
		CurFollowCam = gameMode->FindPlayerCharacterCurrentTurn()->GetFollowCamera();
	}
}

//예상 낙구지점 산출
void ASGCamera::PredictLandingPosition(
	FVector BallLocation,
	FVector BallVelocity,
	FVector MagnusVelocity,
	FVector BowlerFreeViewLastPosition,
	FVector TargetWindForceVector,
	int MagnusForceCount)
{
	FPredictProjectilePathResult PathResult;
		
	FPredictProjectilePathParams PredictParams;
	PredictParams.StartLocation = BallLocation;
	PredictParams.bTraceWithCollision = true;
	PredictParams.SimFrequency = 15.0f;
	PredictParams.TraceChannel = ECollisionChannel::ECC_Pawn;
	PredictParams.MaxSimTime = 10;
	FVector LaunchVelocity = BallVelocity;
	PredictParams.LaunchVelocity = LaunchVelocity;

	bool bHit = UGameplayStatics::PredictProjectilePath(GetWorld(), PredictParams, PathResult);
	
	//FPredictProjectilePathParams에 의해서 예상 낙구 지점이 감지된 경우
	if(bHit && (PathResult.PathData.Num() > 2))
	{
		float MaxZ = PathResult.PathData[0].Location.Z;
		if(IsLanding)
		{
			predictApexPosition = PathResult.PathData[0].Location;
		}

		int PathNum = PathResult.PathData.Num();
		for (auto PathData : PathResult.PathData)
		{
			if(IsLanding)
			{
				if(PathData.Location.Z > MaxZ)
				{
					MaxZ = PathData.Location.Z;
				}
			}
			else
			{
				if(PathData.Location.Z > MaxZ)
				{
					MaxZ = PathData.Location.Z;
					predictApexPosition = PathData.Location;
				}
			}
		}
		
		predictLandingPosition = PathResult.PathData.Last().Location;
		if((predictLandingPosition.X == 0) && (predictApexPosition.Y == 0)) return;

		SG_LOG(Log, "SJW 111 BallLocation %s", *BallLocation.ToString());
		SG_LOG(Log, "SJW 111 TargetWindForceVector %s", *TargetWindForceVector.ToString());
		SG_LOG(Log, "SJW 111 origin Predict End Position %s", *predictLandingPosition.ToString());
		SG_LOG(Log, "SJW 111 Magnus Velocity %s", *MagnusVelocity.ToString());
		SG_LOG(Log, "SJW 111 PathNum %i", PathNum);
		
		USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
		if(GameInst != nullptr)
		{
			predictLandingPosition.X += MagnusVelocity.X;
			predictLandingPosition.Y += MagnusVelocity.Y;

			//test code
			// ASGCourseMode* CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();
			// if(IsValid(CourseMode))
			// {
			// 	CourseMode->SpawnDebugBlueBall(predictLandingPosition);
			// }
			//~test code
		}
		
		//SG_LOG(Log, "SJW PredictApexPosition %s", *predictApexPosition.ToString());
		SG_LOG(Log, "SJW 111 custom after Predict End Position case 01 %s", *predictLandingPosition.ToString());
	}
	else
	{
		predictLandingPosition = BallLocation;
		SG_LOG(Log, "SJW 111 Predict End Position case 02 %s", *predictLandingPosition.ToString());
	}
}

void ASGCamera::GroundCheck(FVector CameraLoc, FVector& ResultLoc)
{
	FHitResult HitResult;

	FVector Start = CameraLoc + FVector(0, 0, 10000);
	FVector End = CameraLoc - FVector(0, 0, 500);

	bool bIsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
	if (bIsGround)
	{
		float ResultZ = HitResult.ImpactPoint.Z + 300;
		ResultLoc = FVector(CameraLoc.X, CameraLoc.Y, ResultZ);
	}
	else
	{
		ResultLoc = CameraLoc;
	}
}

bool ASGCamera::IsOBZPos(FVector CurrentLocation)
{
	FVector Start = CurrentLocation + FVector(0, 0, 1000);
	FVector End = CurrentLocation - FVector(0, 0, 1000);

	FHitResult HitResult;
	EStimpMeterType::Type SurfaceType;

	bool IsInsideBoundary = CheckCurrentHole(Start, End, HitResult, SurfaceType);

	return IsInsideBoundary == false;
}


void ASGCamera::GetLeftRightFairWayPos(FVector MoveDir, FVector FromLocation, FVector& LeftLoc, FVector& RightLoc)
{
	FVector GroundFromApex = FromLocation;
	FVector SideDir = MoveDir.RotateAngleAxis(90, FVector::ZAxisVector).GetSafeNormal();

	//1. Apex 기준 지면을 찾는다.
	{
		FHitResult HitResult;
		FVector Start = GroundFromApex + FVector(0, 0, 10000);
		FVector End = GroundFromApex - FVector(0, 0, 10000);
		bool bIsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);

		if(bIsGround)
		{
			GroundFromApex = HitResult.ImpactPoint;
		}
	}

	bool IsFind = false;
	
	//2. 찾은 지면으로부터 Offset 만큼 좌측으로 이동하면서 OB 지점을 찾는다.
	//TODO case 1번에서 감지된 지면이 이미 OB인 경우, FairWay 지점을 먼저 찾고 그 지점을 기준으로 좌우 OB 구간을 찾는다?
	{
		float Offset = 500;
		
		for(int i = 1; i <= 100; i++)
		{
			FVector NextLocation = GroundFromApex + -1 * SideDir * i * Offset;

			FVector Start = NextLocation + FVector(0, 0, 1000);
			FVector End = NextLocation - FVector(0, 0, 1000);

			FHitResult HitResult;
			EStimpMeterType::Type SurfaceType;

			bool IsInsideBoundary = CheckCurrentHole(Start, End, HitResult, SurfaceType);

			if (IsInsideBoundary)
			{
				FVector HoleBoxLocation = HitResult.ImpactPoint;
				SG_LOG(Log, "SJW detected HoleBoxLocation left %s", *HoleBoxLocation.ToString());
				LeftLoc = HitResult.ImpactPoint;
				IsFind = true;

				return;
			}

			if (IsFind)
				break;
		}
	}

	if(!IsFind)
	{
		//3. 찾은 지면으로부터 Offset 만큼 우측으로 이동하면서 FairWay 지점을 찾는다.
		{
			float Offset = 500;
			for(int i = 1; i <= 100; i++)
			{
				FVector NextLocation = GroundFromApex + SideDir * i * Offset;

				FVector Start = NextLocation + FVector(0, 0, 1000);
				FVector End = NextLocation - FVector(0, 0, 1000);

				FHitResult HitResult;
				EStimpMeterType::Type SurfaceType;

				bool IsInsideBoundary = CheckCurrentHole(Start, End, HitResult, SurfaceType);

				if (IsInsideBoundary)
				{
					FVector HoleBoxLocation = HitResult.ImpactPoint;
					SG_LOG(Log, "SJW detected HoleBoxLocation right %s", *HoleBoxLocation.ToString());
					RightLoc = HitResult.ImpactPoint;
					IsFind = true;

					return;
				}

				if (IsFind)
					break;
			}
		}
	}
}

void ASGCamera::StartFixedCamZPosFix(FVector& CameraLoc)
{
	FHitResult HitResult;

	FVector Start = CameraLoc;
	FVector End = CameraLoc - FVector(0, 0, 50);
	
	bool bIsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
	if (bIsGround)
	{
		//SG_LOG(Log, "SJW 003 OBGroundCamCheck case 003");
		CameraLoc.Z = HitResult.ImpactPoint.Z + 50;
	}
}

void ASGCamera::TraceCamZPosFix(FVector& CameraLoc)
{
	FHitResult HitResult;

	FVector Start = CameraLoc + FVector(0, 0, 10000);
	FVector End = CameraLoc - FVector(0, 0, 100);
	
	bool bIsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
	if (bIsGround)
	{
		CameraLoc.Z = HitResult.ImpactPoint.Z + 100;
	}
}


void ASGCamera::TraceCamZPosFix(FVector& SpringArmLoc, FVector& CameraLoc)
{
	if(flyingTime >= 1.0f)
	{
		FHitResult HitResult;

		FVector Start = CameraLoc + FVector(0, 0, 10000);
		FVector End = CameraLoc - FVector(0, 0, 100);
		
		bool bIsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
		if (bIsGround)
		{
			// SG_LOG(Log, "SJW 222 TraceCamZPosFix case 1 !!!!");
			CameraLoc.Z = HitResult.ImpactPoint.Z + 400;
		}
	}
	//샷 직후 지면 처리
	else
	{
		FHitResult HitResult;

		FVector Start = CameraLoc + FVector(0, 0, 10000);
		FVector End = CameraLoc - FVector(0, 0, 100);

		bool bIsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
		if (bIsGround)
		{
			// SG_LOG(Log, "SJW 222 TraceCamZPosFix case 2 !!!!");
			CameraLoc.Z = HitResult.ImpactPoint.Z + 100;
		}
	}
}

void ASGCamera::PuttingTraceCamZPosFix(FVector& CameraLoc)
{
	FHitResult HitResult;

	FVector Start = CameraLoc + FVector(0, 0, 10000);
	FVector End = CameraLoc - FVector(0, 0, 100);
	
	bool bIsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
	if (bIsGround)
	{
		CameraLoc.Z = HitResult.ImpactPoint.Z + 100;
	}
}


void ASGCamera::RequestClosestDistDecalScaleChange(float Percentage, float MinPercentage, FVector FromLocation)
{
	if(!IsDecalRequestAvailable) return;
	if(IPFinder == nullptr) return;

	//SG_LOG(Log, "SJW 003 RequestClosestDistDecalScaleChange");

	bool available = true;
	IPFinder->SetClosestDecalScale(Percentage, MinPercentage, available, FromLocation);
	IsDecalRequestAvailable = available;
}

bool ASGCamera::CanChangeDecalScaleHeight(FVector FromLocation)
{
	const float TargetZHeight = 2000;		//20m
	FHitResult HitResult;
	FVector Start = FromLocation;
	FVector End = FromLocation - FVector(0, 0, TargetZHeight);
	bool bIsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
	return bIsGround;
}

void ASGCamera::InitBadPlaceHitCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	if(IsBadPlaceHitCameraLocated) return;
	
	FVector BallLocation = Player->GetActorLocation();
	FVector TargetLocation = BallLocation;

	SG_LOG(Log, "SJW 002 InitBadPlaceHitCameraWork BallLocation %s", *BallLocation.ToString());

	//이전 카메라가 샷 추적 카메라인 경우
	if(BeforeApexCamNum == 2)
	{
		SG_LOG(Log, "SJW 002 InitBadPlaceHitCameraWork case aaa");
		
		TargetLocation = TraceCamera->GetCamera()->GetComponentLocation();
	}
	else
	{
		SG_LOG(Log, "SJW 002 InitBadPlaceHitCameraWork case bbb");
		
		FVector MoveDir = BallLocation - startLocation;
		MoveDir *= 0.8f;
		
		FVector SideDir = MoveDir.RotateAngleAxis(90, FVector::ZAxisVector).GetSafeNormal();
		SideDir.Z = 0;
		
		float Offset = 500;
		bool IsFind = false;

		//좌측으로 페어웨이 영역 찾기
		for(int i = 1; i <= 20; i++)
		{
			FVector NextLocation = (startLocation + MoveDir) + -1 * SideDir * i * Offset;

			// SG_LOG(Log, "SJW 002 left side NextLocation %s", *NextLocation.ToString());
			
			FVector Start = NextLocation + FVector(0, 0, 1000);
			FVector End = NextLocation - FVector(0, 0, 10000);

			FHitResult HitResult;
			EStimpMeterType::Type SurfaceType;

			bool IsInsideBoundary = CheckCurrentHole(Start, End, HitResult, SurfaceType);

			if (IsInsideBoundary)
			{
				SG_LOG(Log, "NextLocation %s", *NextLocation.ToString());
				SG_LOG(Log, "i Value %i", i);
				SG_LOG(Log, "SJW 002 InitBadPlaceHitCameraWork case left");
				TargetLocation = FVector(NextLocation.X, NextLocation.Y, BallLocation.Z);
				IsFind = true;
			}

			if (IsFind)
				break;
		}

		//우측으로 페어웨이 영역 찾기
		if(!IsFind)
		{
			for(int i = 1; i <= 20; i++)
			{
				FVector NextLocation = (startLocation + MoveDir) + SideDir * i * Offset;

				// SG_LOG(Log, "SJW 002 right side NextLocation %s", *NextLocation.ToString());
				
				FVector Start = NextLocation + FVector(0, 0, 1000);
				FVector End = NextLocation - FVector(0, 0, 10000);

				FHitResult HitResult;
				EStimpMeterType::Type SurfaceType;

				bool IsInsideBoundary = CheckCurrentHole(Start, End, HitResult, SurfaceType);

				if (IsInsideBoundary)
				{
					SG_LOG(Log, "NextLocation %s", *NextLocation.ToString());
					SG_LOG(Log, "i Value %i", i);
					SG_LOG(Log, "SJW 002 InitBadPlaceHitCameraWork case right");
					TargetLocation = FVector(NextLocation.X, NextLocation.Y, BallLocation.Z);
					IsFind = true;
				}

				if (IsFind)
					break;
			}
		}

		if(!IsFind)
		{
			FVector CrossProduct = FVector::CrossProduct(BeginForwardDir, holecupLocation - startLocation);
			bool IsCameraRightCondition = CrossProduct.Z > 0;

			if(IsCameraRightCondition)
			{
				TargetLocation = BallLocation + SideDir * Offset;
			}
			else
			{
				TargetLocation = BallLocation - SideDir * Offset;
			}
		}
	}
	
	TargetLocation.Z = BallLocation.Z;
	// FVector TargetCameraLocation = BadPlaceHitFixedCamera->GetCamera()->GetComponentLocation();
	// TraceCamZPosFix(TargetLocation, TargetCameraLocation);
	
	BadPlaceHitFixedCamera->SetActorLocation(TargetLocation);

	FRotator Rot = FRotationMatrix::MakeFromX(Player->GetActorLocation() - BadPlaceHitFixedCamera->GetActorLocation()).Rotator();
	BadPlaceHitFixedCamera->SetActorRotation(Rot);
	
	Controller->SetViewTarget(BadPlaceHitFixedCamera);

	IsBadPlaceHitCameraLocated = true;
}


void ASGCamera::BadPlaceHitCameraWork(ASGPlayerCharacter* Player)
{
	// SG_LOG(Log, "SJW 002 BadPlaceHitCameraWork");
	
	FRotator Rot = FRotationMatrix::MakeFromX(Player->GetActorLocation() - BadPlaceHitFixedCamera->GetActorLocation()).Rotator();
	BadPlaceHitFixedCamera->SetActorRotation(Rot);
}


bool ASGCamera::IsSafeSurface(EPhysicalSurface Surface)
{
	bool IsSafe = true;

	switch (Surface)
	{
	case SurfaceType9:			// OB
	case SurfaceType10:			// Hazard
	case SurfaceType11:			// Building
	case SurfaceType12:			// PenaltyYellow
	case SurfaceType13:			// PenaltyRed
		{
			SG_LOG(Log, "SJW 008 OB ................");
			IsSafe = false;
		}
		break;
	}

	return IsSafe;
}


UFirstCameraRecord* ASGCamera::GetFirstCameraRecordByIndex(int32 ID)
{
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if(nullptr == GameInst) return nullptr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return nullptr;

	UFirstCameraRecord* TargetRecord = CameraTable->m_FirstCameraSheet->GetRecord(ID);
	return TargetRecord;
}

TArray<UFirstCameraRecord*> ASGCamera::GetFirstCameraRecordsByPriority(int32 ID)
{
	TArray<UFirstCameraRecord*> TargetArr;
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if(nullptr == GameInst) return TargetArr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return TargetArr;

	TargetArr = GameInst->GetTableManager()->GetFirstCameraRecordsByPriority(ID);
	TargetArr.Sort([](const UFirstCameraRecord& A, const UFirstCameraRecord& B)
	{
		return A.Camera_Active_Rate < B.Camera_Active_Rate;
	});
	return TargetArr;
}

UFirstCameraRecord* ASGCamera::GetTargetFirstCameraPriority(int32 Priority, ASGPlayerCharacter* Player)
{
#ifdef ENABLE_DEBUG_CAMERA
	int DebugFirstCameraIndex = PHYSICAL_MANAGER->GetCameraOneIndex();
	bool IsDebugIndexExist = DebugFirstCameraIndex != 0;
	if(IsDebugIndexExist)
	{
		SG_LOG(Log, "SJW [DebugCamera] First Camera %i", DebugFirstCameraIndex);
		
		return GetFirstCameraRecordByIndex(DebugFirstCameraIndex);
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
						SG_LOG(Log, "SJW 333 TeeShot Condition true");
						continue;
					}
					
					SG_LOG(Log, "SJW 333 TeeShot Condition false");
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
			SG_LOG(Log, "GetTargetFirstCameraPriority NextPriority Case a...");
			
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
			SG_LOG(Log, "GetTargetFirstCameraPriority NextPriority Case b...");
			
			Priority += 1;
			return GetTargetFirstCameraPriority(Priority, Player);
		}

		if(IsValid(TargetRecord))
		{
			SG_LOG(Log, "SJW 111 Selected FirstCameraPriority Index %i, Priority %i", TargetRecord->Index, Priority);
		}

		return TargetRecord;
	}
	
	SG_LOG(Log, "GetTargetFirstCameraPriority Exception Case...");
	return nullptr;
}

USecondCameraRecord* ASGCamera::GetTargetSecondCameraPriority(int32 Priority, ASGPlayerCharacter* Player)
{
#ifdef ENABLE_DEBUG_CAMERA
	int DebugSecondCameraIndex = PHYSICAL_MANAGER->GetCameraTwoIndex();
	bool IsDebugIndexExist = DebugSecondCameraIndex != 0;
	if(IsDebugIndexExist)
	{
		SG_LOG(Log, "SJW [DebugCamera] Second Camera %i", DebugSecondCameraIndex);

		return GetSecondCameraRecordByIndex(DebugSecondCameraIndex);
	}
#endif
	
	auto TargetRecords = GetSecondCameraRecordsByPriority(Priority);
	if(TargetRecords.Num() > 0)
	{
		TArray<USecondCameraRecord*> ValidRecords;	//조건을 최종적으로 만족하는 Record 그룹

		for (auto TargetRecord : TargetRecords)
		{
			//1. 시작 카메라 조건 검사
			{
				FString FirstCamera = TargetRecord->First_Camera;
				int32 TargetConditionFirstCameraNum = GetBeforeApexCamNumFromStringValue(FirstCamera);
				if(TargetConditionFirstCameraNum != BeforeApexCamNum) { continue; }
			}

			//2. 티샷 여부
			{
				int32 TeeShotCond = TargetRecord->Tee_Shot;
				bool TeeShotCondExist = TeeShotCond != -1;
				if(TeeShotCondExist)
				{
					if(TeeShotCond == 1)
					{
						if(IsTeeShot == false) { continue; }
					}
					else
					{
						if(IsTeeShot == true) { continue; }
					}
				}
			}

			//3. 볼 파워 조건 검사
			{
				int32 BallPowerMinCond = TargetRecord->Ball_Power_Min;
				int32 BallPowerMaxCond = TargetRecord->Ball_Power_Max;

				if(BallPowerMinCond != -1)
				{
					if(ballPower < BallPowerMinCond) { continue; }
				}

				if(BallPowerMaxCond != -1)
				{
					if(ballPower > BallPowerMaxCond) { continue; }
				}
			}

			//4. 발사각 조건 검사
			{
				int32 LaunchDegreeMinCond = TargetRecord->Launch_Degree_Min;
				int32 LaunchDegreeMaxCond = TargetRecord->Launch_Degree_Max;

				if(LaunchDegreeMinCond != -1)
				{
					if(launchAngle < LaunchDegreeMinCond) {	continue; }
				}

				if(LaunchDegreeMaxCond != -1)
				{
					if(launchAngle > LaunchDegreeMaxCond) { continue; }
				}
			}

			//5. 낙구 예측 지점 지형 조건 검사
			{
				bool NeedTerrainCheck = false;
				TArray<int32> StopTerrains = TargetRecord->Stop_Terrain;
				if(StopTerrains.Num() > 1)
				{
					NeedTerrainCheck = true;
				}
				else
				{
					if(StopTerrains.Num() == 1)
					{
						if(StopTerrains[0] != -1)
						{
							NeedTerrainCheck = true;
						}
					}
				}

				if (NeedTerrainCheck)
				{
					FVector Start = predictLandingPosition + FVector(0, 0, 10000);
					FVector End = predictLandingPosition - FVector(0, 0, 10000);

					FHitResult HitResult;
					EStimpMeterType::Type SurfaceType;

					bool IsInsideBoundary = CheckCurrentHole(Start, End, HitResult, SurfaceType);

					if (IsInsideBoundary)
					{
						SG_LOG(Log, "SJW 111 GetTargetSecondCameraPriority Func PredictLandingPosition %s", *predictLandingPosition.ToString());

						int32 TargetLocationSurfaceType = (int32)SurfaceType;

						if (!StopTerrains.Contains(TargetLocationSurfaceType))
						{
							SG_LOG(Log, "TargetLocationSurfaceType %i not included", TargetLocationSurfaceType);
							continue;
						}
					}
				}
			}

			//6. Apex 지점 조건 검사
			{
				// float ApexHeightMinCond = TargetRecord->Apex_Height_Min;
				// float ApexHeightMaxCond = TargetRecord->Apex_Height_Max;
				// if(ApexHeightMinCond != -1)
				// {
				// 	
				// }
			}

			//조건을 모두 만족하는 경우
			ValidRecords.Add(TargetRecord);
		}

		//조건을 만족하는 그룹이 없는 경우 다음 우선순위 진행
		if(ValidRecords.Num() == 0)
		{
			// SG_LOG(Log, "GetTargetSecondCameraPriority NextPriority Case a...");
			
			Priority += 1;
			return GetTargetSecondCameraPriority(Priority, Player);
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
		
		USecondCameraRecord* TargetRecord = nullptr;

		//4. 조건을 만족하는 그룹에서 확률을 기반으로 무작위 Record를 추출한다.
		int RndResult = FMath::RandRange(1, 100);
		for(int i = 0; i < PercentageDatas.Num(); i++)
		{
			if((RndResult >= PercentageDatas[i].Min) && (RndResult < PercentageDatas[i].Max))
			{
				TargetRecord = TargetRecords[i];
				break;
			}
		}

		if(TargetRecord == nullptr)
		{
			SG_LOG(Log, "GetTargetSecondCameraPriority NextPriority Case b...");
			
			Priority += 1;
			return GetTargetSecondCameraPriority(Priority, Player);
		}

		if(IsValid(TargetRecord))
		{
			SG_LOG(Log, "SJW 111 Selected SecondCameraRecord Index %i, Priority %i", TargetRecord->Index, Priority);
		}
		
		return TargetRecord;
	}

	//다음 우선순위가 비어있는 경우(연속적이지 않은 경우)에 대한 예외처리
	if((TargetRecords.Num() == 0) && (Priority < GetLastSecondCameraRecord()->Priority))
	{
		Priority += 1;
		return GetTargetSecondCameraPriority(Priority, Player);
	}
	
	SG_LOG(Log, "GetTargetSecondCameraPriority Exception Case...");
	return nullptr;
}

TArray<USecondCameraRecord*> ASGCamera::GetSecondCameraRecordsByPriority(int32 ID)
{
	TArray<USecondCameraRecord*> TargetArr;
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if(nullptr == GameInst) return TargetArr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return TargetArr;

	TargetArr = GameInst->GetTableManager()->GetSecondCameraRecordsByPriority(ID);
	return TargetArr;
}

USecondCameraRecord* ASGCamera::GetLastSecondCameraRecord()
{
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if(nullptr == GameInst) return nullptr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return nullptr;

	return CameraTable->m_SecondCameraSheet->arrayTable.Last();
}


int32 ASGCamera::GetBeforeApexCamNumFromStringValue(FString TargetValue)
{
	TargetValue = TargetValue.ToLower();
	if(TargetValue == TEXT("shot_fixed")) {	return 1; }
	if(TargetValue == TEXT("shot_follow")) { return 2; }
	
	return 2;
}

int32 ASGCamera::GetAfterApexCamNumFromStringValue(FString TargetValue)
{
	TargetValue = TargetValue.ToLower();
	if(TargetValue == TEXT("side_fixed")){ return 2; }
	if(TargetValue == TEXT("land_fixed")){ return 1; }
	if(TargetValue == TEXT("shot_follow")){ return 3; }

	return 3;
}

bool ASGCamera::IsAvailableThirdCamera(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	if(!IsValid(SelectedSecondCameraRecord)) return false;

	FString SecondCameraStr = SelectedSecondCameraRecord->Camera_Result.ToLower();
	int32 RecordApexCamNum = GetAfterApexCamNumFromStringValue(SecondCameraStr);
	
	int32 LandCameraActiveCond = SelectedSecondCameraRecord->Third_Camera_Active;
	
#ifdef ENABLE_DEBUG_CAMERA
	int DebugThirdCameraIndex = PHYSICAL_MANAGER->GetCameraThreeIndex();
	bool IsDebugCamera = PHYSICAL_MANAGER->GetCameraThreeIndex() != 0;
	if(IsDebugCamera)
	{
		if(DebugThirdCameraIndex == -1)
		{
			LandCameraActiveCond = 0;
		}
		else if(DebugThirdCameraIndex == 1)
		{
			LandCameraActiveCond = 1;
		}
	}
#endif
	
	if(LandCameraActiveCond == 1)
	{
		float TargetHeight = SelectedSecondCameraRecord->Third_Camera_Height;

		//test code
		//TargetHeight = 0.5f;
		//end test code
		
		TargetHeight *= 100;
		
		SG_LOG(Log, "TargetHeight %f", TargetHeight);
		
		return IsCloseToGround(Player, TargetHeight);
	}
		
	return false;
}

ASGCamera::PercentageData::PercentageData(int32 mMin, int32 mMax)
{
	Min = mMin;
	Max = mMax;
}

UPuttCameraRecord* ASGCamera::GetLastPuttingCameraRecord()
{
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if(nullptr == GameInst) return nullptr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return nullptr;

	return CameraTable->m_PuttCameraSheet->arrayTable.Last();
}

UPuttCameraRecord* ASGCamera::GetTargetPuttingCameraRecordByPriority(int32 Priority, ASGPlayerCharacter* Player)
{
	auto TargetRecords = GetPuttingCameraRecordsByPriority(Priority);
	if(TargetRecords.Num() > 0)
	{
		TArray<UPuttCameraRecord*> ValidRecords;	//조건을 최종적으로 만족하는 Record 그룹

		FVector BallLocation = Player->GetActorLocation();
		
		for (auto TargetRecord : TargetRecords)
		{
			//1. 홀컵 시작 거리 최소 거리 조건
			{
				float HoleStartDistMinCond = TargetRecord->Hole_Start_Distance_Min;
				float HoleStartDistMaxCond = TargetRecord->Hole_Start_Distance_Max;
				
				float HoleCupToBallDist = FVector::Dist2D(BallLocation, holecupLocation);

				if(HoleStartDistMinCond != -1)
				{
					if(HoleCupToBallDist < HoleStartDistMinCond * 100) { continue; }
				}

				if(HoleStartDistMaxCond != -1)
				{
					if(HoleCupToBallDist > HoleStartDistMaxCond * 100) { continue; }
				}
			}

			//2. 예상 공 멈추는 지점에 대한 조건
			{
				ASGPuttingGuide* PuttingGuide = Player->GetPuttingGuideActor();
				if (IsValid(PuttingGuide))
				{
					float HoleStopDistMinCond = TargetRecord->Hole_Stop_Distance_Min;
					float HoleStopDistMaxCond = TargetRecord->Hole_Stop_Distance_Max;

					float TargetHeight = BallLocation.Z - holecupLocation.Z;
					float PredictDist = PuttingGuide->CalculatePredictDist(ballPower, TargetHeight);
					predictPuttingPosition = BallLocation + Player->GetActorForwardVector().GetSafeNormal() * PredictDist;

					SG_LOG(Log, "SJW 111 Predicted After Putting Location : %s", *predictPuttingPosition.ToString());
					
					float DistPredictedLocToHole = FVector::Dist2D(predictPuttingPosition, holecupLocation);
					
					SG_LOG(Log, "SJW 111 DistPredictedLocToHole %f", DistPredictedLocToHole);
					
					if(HoleStopDistMinCond != -1)
					{
						if(DistPredictedLocToHole < HoleStopDistMinCond * 100) { continue; }
					}

					if(HoleStopDistMaxCond != -1)
					{
						if(DistPredictedLocToHole > HoleStopDistMaxCond * 100) { continue; }
					}
				}
			}

			//조건을 모두 만족하는 경우
			ValidRecords.Add(TargetRecord);
		}

		//조건을 만족하는 그룹이 없는 경우 다음 우선순위 진행
		if(ValidRecords.Num() == 0)
		{
			// SG_LOG(Log, "GetTargetPuttingCameraRecordByPriority NextPriority Case a...");
			
			Priority += 1;
			return GetTargetPuttingCameraRecordByPriority(Priority, Player);
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
		
		UPuttCameraRecord* TargetRecord = nullptr;

		//4. 조건을 만족하는 그룹에서 확률을 기반으로 무작위 Record를 추출한다.
		int RndResult = FMath::RandRange(1, 100);
		for(int i = 0; i < PercentageDatas.Num(); i++)
		{
			if((RndResult >= PercentageDatas[i].Min) && (RndResult < PercentageDatas[i].Max))
			{
				TargetRecord = TargetRecords[i];
				break;
			}
		}

		if(TargetRecord == nullptr)
		{
			SG_LOG(Log, "GetTargetPuttingCameraRecordByPriority NextPriority Case b...");
			
			Priority += 1;
			return GetTargetPuttingCameraRecordByPriority(Priority, Player);
		}

		if(IsValid(TargetRecord))
		{
			SG_LOG(Log, "SJW 111 Selected PuttingRecord Index %i, Priority %i", TargetRecord->Index, Priority);
		}
		
		return TargetRecord;
	}

	//다음 우선순위가 비어있는 경우(연속적이지 않은 경우)에 대한 예외처리
	if((TargetRecords.Num() == 0) && (Priority < GetLastPuttingCameraRecord()->Priority))
	{
		Priority += 1;
		return GetTargetPuttingCameraRecordByPriority(Priority, Player);
	}
	
	SG_LOG(Log, "GetTargetPuttingCameraRecordByPriority Exception Case...");
	return nullptr;
}

TArray<UPuttCameraRecord*> ASGCamera::GetPuttingCameraRecordsByPriority(int32 ID)
{
	TArray<UPuttCameraRecord*> TargetArr;
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if(nullptr == GameInst) return TargetArr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return TargetArr;

	TargetArr = GameInst->GetTableManager()->GetPuttCameraRecordsByPriority(ID);
	return TargetArr;
}

int32 ASGCamera::GetPuttingCamNumFromStringValue(FString TargetValue)
{
	TargetValue = TargetValue.ToLower();
	if(TargetValue == TEXT("putt_follow")){ return 1; }
	if(TargetValue == TEXT("putt_fixed")){ return 2; }

	return 1;
}

void ASGCamera::ActiveEndShotWaitCamera()
{
	SG_LOG(Log, "SJW 333 ActiveEndShotWaitCamera.......");
	
	EndShotWaitCameraNeed = true;

	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if (nullptr == GameInst) return;

	APlayerController* Controller = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
	if (nullptr == Controller) return;

	ASGPlayerCharacter* Player = Cast<ASGPlayerCharacter>(Controller->GetCharacter());
	if (nullptr == Player) return;
	
	float BallToHoleDist = FVector::Dist2D(holecupLocation, Player->GetActorLocation());
	if(BallToHoleDist < 20) return;
	
	FVector EndShotWaitCameraLocation = Controller->PlayerCameraManager->GetCameraLocation();
	FRotator EndShotWaitCameraRotator = Controller->PlayerCameraManager->GetCameraRotation();

	float FOV = Controller->PlayerCameraManager->GetFOVAngle();
	EndShotWaitCamera->GetCamera()->FieldOfView = FOV;
	EndShotWaitCamera->GetCamera()->SetWorldLocation(EndShotWaitCameraLocation);
	EndShotWaitCamera->GetCamera()->SetWorldRotation(EndShotWaitCameraRotator);
	
	Controller->SetViewTarget(EndShotWaitCamera);
}

UPuttCameraRecord* ASGCamera::GetTargetPuttingCameraRecordByIndex(int32 Index)
{
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if(nullptr == GameInst) return nullptr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return nullptr;

	UPuttCameraRecord* TargetRecord = GameInst->GetTableManager()->GetPuttCameraRecordByID(Index);
	return TargetRecord;
}

USecondCameraRecord* ASGCamera::GetSecondCameraRecordByIndex(int32 ID)
{
	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if(nullptr == GameInst) return nullptr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return nullptr;

	USecondCameraRecord* TargetRecord = CameraTable->m_SecondCameraSheet->GetRecord(ID);
	return TargetRecord;
}

//현재 공이 화면 가장자리에 있는 상태인가?
bool ASGCamera::CheckUnSafeScreenArea(int32 CurrentScreenY, bool& UpperUnSafe, bool& BottomUnsafe)
{
	float TargetRatio = 0;
	if((ballPower >= 0) && (ballPower < 2000))
	{
		TargetRatio = 0.5f;
	}
	
	if((ballPower >= 2000) && (ballPower < 3000))
	{
		TargetRatio = 0.4f;
	}
	
	if((ballPower >= 3000) && (ballPower < 4000))
	{
		TargetRatio = 0.3f;
	}
	
	if((ballPower >= 4000) && (ballPower < 6000))
	{
		TargetRatio = 0.2f;
	}
	
	if(ballPower >= 6000)
	{
		TargetRatio = 0.1f;
	}

	float UpperLimit = ScreenSize.Y * TargetRatio;
	float BottomLimit = ScreenSize.Y * (1 - TargetRatio);

	bool IsInSafeArea = (CurrentScreenY > UpperLimit) && (CurrentScreenY < BottomLimit);

	UpperUnSafe = CurrentScreenY <= UpperLimit;
	BottomUnsafe = CurrentScreenY >= BottomLimit;
	
	// SG_LOG(Log, "SJW 777 CurrentScreenY %i, UpperLimit %f, BottomLimit %f", CurrentScreenY, UpperLimit, BottomLimit);
	
	return !IsInSafeArea;
}

void ASGCamera::ChangeCameraShiftSettings(float TargetShiftRotateAngle) 
{
	//비율로 ShiftLeftAmount 값을 찾는다. [15도일 때 100]
	ShiftLeftAmount = 15.0f / (TargetShiftRotateAngle * 100.0f);

	USGGameInstance* GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if (nullptr == GameInst) return;

	APlayerController* Controller = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
	if (nullptr == Controller) return;

	ASGPlayerCharacter* Player = Cast<ASGPlayerCharacter>(Controller->GetCharacter());
	if (nullptr == Player) return;

	ASGCourseMode* const CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();
	bool IsCourseMode = IsValid(CourseMode);
	
	ShiftRotateAngle = TargetShiftRotateAngle;
	Player->FollowCameraFixed();
	Player->UpdateCameraFieldOfView();
	
	if(ShiftRotateAngle == 0)
	{
		TraceCamera->GetSpringArm()->SetRelativeRotation(FRotator::ZeroRotator);
	}
	else
	{
		float OffSetAngle = 0.0f;
		
		if(IsCourseMode)
		{
			bool bIsTeeShot = SHOT_DATA_MANAGER->GetIsTeeShot(Player->GetUniqID());
			bool IsApronOrGreen = Player->IsOnApronORGreenWithStayMeterType();
			
			if(!bIsTeeShot && IsApronOrGreen)
			{
				OffSetAngle = ShiftPuttingShotOffSetAngle;
			}

			if(!bIsTeeShot && !IsApronOrGreen)
			{
				OffSetAngle = ShiftSecondShotOffSetAngle;
			}
		}
		
		TraceCamera->GetSpringArm()->SetRelativeRotation(FRotator(0, -1 * (ShiftRotateAngle + OffSetAngle), 0));
	}
}
