#include "SGCamera.h"

#include "SGBowlerFreeView.h"
#include "SGGameInstance.h"
#include "SGCameraMode.h"
#include "SGCameraRailActor.h"
#include "SGCourseMode.h"
#include "SGDataCenter.h"
#include "SGDrivingMode.h"
#include "SGPlayerCharacter.h"
#include "SGGroundChecker.h"
#include "SGPixelEdgeFinder.h"
#include "SGPuttingGuide.h"
#include "SGShotCameraTable.h"
#include "SGShotDataManager.h"
#include "SGTableManager.h"
#include "SGUserData.h"

#include "EngineUtils.h"
#include "InstancedFoliageActor.h"
#include "FoliageInstancedStaticMeshComponent.h"
#include "Camera/PlayerCameraManager.h"
#include "Components/ActorComponent.h"
#include "Components/InstancedStaticMeshComponent.h"

#define ENABLE_DEBUG_CAMERA
#define EPSILON 0.00001
#define ENABLE_OCCLUSION

ASGCamera::ASGCamera()
{
	PrimaryActorTick.bCanEverTick = true;
}

void ASGCamera::BeginPlay()
{
	Super::BeginPlay();
	
	SpawnCamera();

	IsDecalRequestAvailable = false;
	
	GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	PlayerController = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
	PlayerChar = Cast<ASGPlayerCharacter>(PlayerController->GetCharacter());
}

void ASGCamera::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	Super::EndPlay(EndPlayReason);

	if(IsValid(GetWorld()))
	{
		GetWorld()->GetTimerManager().ClearTimer(TreeHitHandle);
		GetWorld()->GetTimerManager().ClearTimer(PuttingTraceCameraWaitHandle);
		GetWorld()->GetTimerManager().ClearTimer(SkyTraceHandle);
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

	PuttingFixedCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if (nullptr != PuttingFixedCamera)
	{
#if WITH_EDITOR
		PuttingFixedCamera->SetActorLabel(TEXT("Z_GreenCamera"));
#endif
		PuttingFixedCamera->GetCamera()->SetRelativeLocation(FVector(0, 0, 900));

		PuttingFixedCamera->GetSpringArm()->bDoCollisionTest = false;
		PuttingFixedCamera->GetSpringArm()->TargetArmLength = 450;
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
		PuttingTraceCamera->GetSpringArm()->bUsePawnControlRotation = false;
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

	SkyTraceCamera = Cast<ASGCameraMode>(GetWorld()->SpawnActor<AActor>(CourseCameraFindClass, FVector::ZeroVector, FRotator::ZeroRotator));
	if(nullptr != SkyTraceCamera)
	{
#if WITH_EDITOR
		SkyTraceCamera->SetActorLabel(TEXT("Z_SkyTraceCamera"));
#endif
		SkyTraceCamera->GetSpringArm()->Deactivate();
		SkyTraceCamera->GetSpringArm()->bDoCollisionTest = false;
		SkyTraceCamera->GetSpringArm()->bEnableCameraLag = false;
		SkyTraceCamera->GetSpringArm()->bEnableCameraRotationLag = false;
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

	// GetWorld()->GetTimerManager().SetTimer(FoliageFindTimer, this, &ASGCamera::SetFoliageOpacity, 1.0f, true, 1.0f);
	
	IPFinder = Cast<ASGIPFinder>(UGameplayStatics::GetActorOfClass(GetWorld(), ASGIPFinder::StaticClass()));
	if(IPFinder != nullptr)	{ IPFinder->ResetAllDecalScale(); }
	
	if (nullptr == GameInst) return;
	if (nullptr == PlayerController) return;
	if (nullptr == PlayerChar) return;

	TraceCamera->GetSpringArm()->SocketOffset = FVector::ZeroVector;

	PlayerChar->ClearWaitCameraTimer();

	ballPower = GameInst->GetBallSpeed();
	launchAngle = GameInst->GetLaunchAngle();
	
	sideSpinRate = GameInst->GetSideSpinRate();
	
	if(ballPower < 2000)
    {
    	IsLowApex = true;
    	IsLowApexInitialized = true;
    }

	//현재 화면 크기 정보를 가져온다. [에디터에서 스크린 사이즈를 중간에 변경할 수 있기 때문에 Impact에서 갱신한다.]
	{
		int32 ViewPortSizeX = 1920;
		int32 ViewPortSizeY = 1080;
		PlayerController->GetViewportSize(ViewPortSizeX, ViewPortSizeY);
		ScreenSize = FVector2d(ViewPortSizeX, ViewPortSizeY);
	}

	FVector NormalizeBallForwardVector = PlayerChar->GetActorForwardVector().GetSafeNormal();
	FVector NormalizeBeginForwardVector = BeginForwardDir.GetSafeNormal();
	float DotProduct = FVector::DotProduct(NormalizeBallForwardVector, NormalizeBeginForwardVector);

	BallForwardVectorAfterImpact = PlayerChar->GetActorForwardVector();
	FVector ShiftedCamToBallDirVector = BallForwardVectorAfterImpact.RotateAngleAxis(ShiftRotateAngle, FVector::ZAxisVector);
	
	float AngleInRadian = FMath::Acos(FMath::Clamp(DotProduct, -1.0f, 1.0f));
	launchSideAngleInDegree = FMath::RadiansToDegrees(AngleInRadian);
	
	{
		IsNotChange = false;
		CanTickCameraWork = true;
		IsLanding = false;
		IsPutting = false;
		flyingTime = 0;
		oldLocation = startLocation = PlayerChar->GetActorLocation() - FVector(0, 0, 30); // 처음 최고점 구할 때는 oldLocation이 현재 플레이어 높이보다 낮아야함

		CanTraceCamera = true;
		CanRotateTraceCamera = true;
	}

	ASGDrivingMode* const drivingMode = Cast<ASGDrivingMode>(GetWorld()->GetAuthGameMode());
	if (IsValid(drivingMode))
	{
		IsDrivingMode = true;
		if (IsValid(drivingMode->GetTargetActor()))
			holecupLocation = drivingMode->GetTargetActorLocation(drivingMode->GetTargetActor()); // 홀컵 위치 찾기
	}
	else
	{
		IsDrivingMode = false;
		holecupLocation = PlayerChar->CharacterComponent->GetPinPos(); // 홀컵 위치 찾기

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
 		if(IsValid(PHYSICAL_MANAGER))
 		{
 			bool IsDebugCameraExist = PHYSICAL_MANAGER->GetCameraOneIndex() != 0;
 			if(IsDebugCameraExist)
 			{
 				SelectedFirstCameraRecord = GetTargetFirstCameraPriority(1, PlayerChar);
 				BeforeApexCamNum = SelectedFirstCameraRecord != nullptr ?
					GetBeforeApexCamNumFromStringValue(SelectedFirstCameraRecord->Camera_Result) : 2;
 			}
 			else
 			{
 				if(drivingMode->GetIsPutter()) { BeforeApexCamNum = 0; }
 				else { BeforeApexCamNum = 2; }
 			}
 		}
#endif
	}
	else
	{
		SelectedFirstCameraRecord = GetTargetFirstCameraPriority(1, PlayerChar);
		SecondCameraRecordPool = GetSecondCameraRecordsByConditions(SelectedFirstCameraRecord->Camera_Result, ballPower, launchAngle);
		
		BeforeApexCamNum = SelectedFirstCameraRecord != nullptr ?
			GetBeforeApexCamNumFromStringValue(SelectedFirstCameraRecord->Camera_Result) : 2;
		
		// SG_LOG(Log, "SJW BeforeApexCamNum : %i", BeforeApexCamNum);

		CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();
		if(IsValid(CourseMode))
		{
			IsDebugSkyTraceState = CourseMode->IsDebugSkyTrace();
		}
		
		if(IsDebugSkyTraceState){ BeforeApexCamNum = 3; }
		
		//test code
		// BeforeApexCamNum = 1;
		//end test code
	}

	bool IsTraceCamera = (BeforeApexCamNum == 2) || (BeforeApexCamNum == 3);

#ifdef ENABLE_OCCLUSION
	bool IsOcclusionEnable = IsTraceCamera;
	if(OcclusionMPCInstance)
	{
		float TargetParValue = IsOcclusionEnable ? 1.0f : 0.0f;
		OcclusionMPCInstance->SetScalarParameterValue("EnableOcclusion", TargetParValue);
	}
#endif
	
	PlayerChar->UpdateBallTail(IsTraceCamera);

	if(!IsValid(PHYSICAL_MANAGER)){ IsPutting = PlayerChar->IsOnApronORGreen(); }
	else
	{
		IsPutting = PlayerChar->IsOnApronORGreen() && (PHYSICAL_MANAGER->GetDrivingModeType() == EDrivingModeType::Putter);	
	}
	
	if (IsDrivingMode)
	{
		TraceCamera->SetActorRotation(StartPlayerRot);
		StartFixedCamera->SetActorRotation(StartPlayerRot);
	}
	else
	{
		CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();

		if (IsPutting)
		{
			bool IsDebugPutting = CourseMode->IsDebugPutting();
			auto DebugSettingMap = CourseMode->GetDebugPuttingSettingData();
			
			if(IsValid(CourseMode) && IsDebugPutting)
			{
				SelectedPuttingCameraRecord = NewObject<UPuttCameraRecord>();

				int32 CameraResult = DebugSettingMap["CameraResult"];
				if(CameraResult == 0) {	SelectedPuttingCameraRecord->Camera_Result = "putt_follow";	}
				else { SelectedPuttingCameraRecord->Camera_Result = "putt_fixed"; }
				
				SelectedPuttingCameraRecord->Camera_Active_Rate = 100;
				SelectedPuttingCameraRecord->Follow_Camera_Distance = DebugSettingMap["Follow_Camera_Distance"];
				
				SelectedPuttingCameraRecord->Zoom_Camera_Active = DebugSettingMap["Zoom_Camera_Active"];
				SelectedPuttingCameraRecord->Zoom_Camera_Active_Distance = DebugSettingMap["Zoom_Camera_Active_Distance"];
				SelectedPuttingCameraRecord->Zoom_Camera_Ratio = DebugSettingMap["Zoom_Camera_Ratio"];

				SelectedPuttingCameraRecord->Putt_Hole_Fixed = DebugSettingMap["Putt_Hole_Fixed"];
				SelectedPuttingCameraRecord->Putt_Fixed_Degree = DebugSettingMap["Putt_Fixed_Degree"];
				SelectedPuttingCameraRecord->Putt_Side_Position = DebugSettingMap["Putt_Side_Position"];

				SelectedPuttingCameraRecord->Putt_Fixed_Height = DebugSettingMap["Putt_Fixed_Height"];
				SelectedPuttingCameraRecord->Putt_Fixed_Distance = DebugSettingMap["Putt_Fixed_Distance"];
			}
			else
			{
				SelectedPuttingCameraRecord = GetTargetPuttingCameraRecordByPriority(1, PlayerChar);	
			}

			//test code
			//SelectedPuttingCameraRecord = GetTargetPuttingCameraRecordByIndex(25);
			//end test code

			TraceCamera->GetSpringArm()->SocketOffset = FVector(0, 0, 0);
			TraceCamera->GetSpringArm()->TargetArmLength = PlayerChar->GetCameraBoom()->TargetArmLength;
			TraceCamera->SetActorLocation(PlayerChar->GetFollowCamera()->GetComponentLocation());
			
			if(IsValid(SelectedPuttingCameraRecord))
			{
				FString CameraResultStr = SelectedPuttingCameraRecord->Camera_Result;
				PuttingCamNum = GetPuttingCamNumFromStringValue(CameraResultStr);

				if(PuttingCamNum == 1)
				{
					InitPuttingTraceCameraWork(PlayerController, PlayerChar);
				}
				else if(PuttingCamNum == 2)
				{
					InitPuttingFixedCameraWork(PlayerController, PlayerChar);
				}
			}
		}
		//Note. 한번 디버그 설정하면 해제하는 방법이 없는데, 샷이 끝날 때마다 초기화 하려면 주석 해제 필요
		// if(IsValid(CourseMode))	{ CourseMode->IsCameraFOVDebugForced = false; }
		else
		{
			FRotator Rot = FRotationMatrix::MakeFromX(ShiftedCamToBallDirVector).Rotator();
			Rot.Pitch = 0;
			TraceCamera->GetCamera()->SetWorldRotation(Rot);
		}

		CourseMode->GetWindArrowDirection(WindVector);
	}

	if(OcclusionMPCInstance)
	{
		OcclusionMPCInstance->SetScalarParameterValue("EnableGrassOcclusion", 0.0f);
	}
	IsUnder100Shot = GetIsUnder100(PlayerChar, holecupLocation);
}


void ASGCamera::EndShot()
{
	if(IsValid(GameInst)){ PlayerController = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController()); }
	if(IsValid(PlayerController)) { PlayerChar = Cast<ASGPlayerCharacter>(PlayerController->GetCharacter()); }

	IsFoliageTimerInitialized = false;

	if(OcclusionMPC)
	{
		if(!OcclusionMPCInstance)
		{
			OcclusionMPCInstance = GetWorld()->GetParameterCollectionInstance(OcclusionMPC.Get());
		}
		if(OcclusionMPCInstance)
		{
			OcclusionMPCInstance->SetVectorParameterValue("BallLocation", PlayerChar->GetActorLocation());
			OcclusionMPCInstance->SetScalarParameterValue("EnableGrassOcclusion", 1.0f);
		}
	}

#ifdef ENABLE_OCCLUSION
	if(OcclusionMPC)
	{
		if(!OcclusionMPCInstance)
		{
			OcclusionMPCInstance = GetWorld()->GetParameterCollectionInstance(OcclusionMPC.Get());
		}
		if(OcclusionMPCInstance)
		{
			OcclusionMPCInstance->SetScalarParameterValue("EnableOcclusion", 1.0f);
		}
	}
#endif
	
	if(CameraRailActor != nullptr)
	{
		CameraRailActor->EndTrace();
	}

	if(IsValid(GetWorld()))
	{
		GetWorld()->GetTimerManager().ClearTimer(FoliageFindTimer);
	}

	OcclusionCheckGrassPassTime = 0;
	LandingTime = 0;
	ImpactedTime = 0;
	
	TargetHorizontalSafeAreaRatio = DefaultTargetHorizontalSafeAreaRatio;
	IsChangedSafeAreaRatioAfterImpacted = false;

	PrevBallImpacted = false;
	
	DistanceAlongSpline = 0;

	IsPredictApexSet = false;
	IsApexPositionSet = false;
	IsInitTraceCamera = false;
	IsTooClosePutting = false;
	IsTraceCameraPrevAlreadyStopped = false;
	IsAlreadyAddBezierLandingLine = false;
	IsAlreadyPauseTrace = false;
	IsPassedCamBrakePosition = false;
	IsRemoveCamPointToEndPoint = false;
	IsPredictGreenLanding = false;
	IsPuttingZoomCameraSetLocationRotation = false;
	IsTooCloseCalled = false;
	
	CanAddLandingPathLine = true;
	
	IsUpperOutSafeAreaDetected = false;
	IsBottomOutSafeAreaDetected = false;

	bIsTraceCameraSocketChangeLerping = true;
	bIsTraceCameraSocketChangeLerping2 = true;
	
	bIsTraceCameraLagChangeLerping = true;
	bIsTraceCameraLagChangeLerping2 = true;
	bIsTraceCameraRotateChangeAfterImpactedLerping = true;
	bIsZoomInLerping = true;

	IsSkyTraceImpactedPathAdded = false;

	TraceCameraLagChangeTime = 0;
	TraceCameraLagChangeTime2 = 0;
	
	TraceCameraSocketChangeTime = 0;
	TraceCameraRotateTimeChangeAfterImpactedTime = 0;
	TraceCameraSocketChangeTime2 = 0;
	SkyTraceCameraDownVectorAfterLandingPassTime = 0;
	ZoomInPassTime = 0;
	
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
	IsBallAlmostImpacted = false;

	IsBadImpacted = false;
	IsHoleIn = false;

	IsPutting = false;

	IsLowApexInitialized = false;
	IsLowApex = false;

	SkyTraceDescendVelocityRatio = 1.0f;
	
	if(!IsDrivingMode && IsValid(IPFinder))
	{ 
		IPFinder->ResetAllDecalScale();
	}
	
	InitCameraLag();
	InitCameraRelativeRotation();
	TraceCamera->GetSpringArm()->TargetArmLength = 300;
	TraceCamera->GetSpringArm()->CameraLagSpeed = 1;
	TraceCamera->GetSpringArm()->SocketOffset = FVector(0, 0, 100);
	TraceCamera->GetCamera()->SetRelativeLocation(FVector::ZeroVector);

	SkyTraceStopDist = 700;
	SkyTraceLowApexStopDist = 700;

	if(ShiftRotateAngle == 0)
	{
		TraceCamera->GetSpringArm()->SetRelativeRotation(FRotator::ZeroRotator);
	}
	else
	{
		TraceCamera->GetSpringArm()->SetRelativeRotation(FRotator(0, -1 * ShiftRotateAngle, 0));	
	}

	PuttingZoomCamera->GetCamera()->FieldOfView = 80;
	
	if (nullptr != GameInst)
	{
		PlayerController = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
		if (nullptr != PlayerController)
		{
			PlayerChar = Cast<ASGPlayerCharacter>(PlayerController->GetPawn());
			if (nullptr != PlayerChar)
			{
				PlayerController->SetViewTarget(PlayerChar->GetFollowCamera()->GetOwner());

				FVector PlayerLocation = PlayerChar->GetActorLocation();
				StartFixedCamera->SetActorLocation(PlayerLocation);

				FVector BallToTraceCameraVector = PlayerChar->GetActorLocation() - TraceCamera->GetCamera()->GetComponentLocation();
				FVector LeftVector = BallToTraceCameraVector.RotateAngleAxis(-90, FVector::ZAxisVector).GetSafeNormal();
				LeftVector.Z = 0;
				LeftVector *= ShiftLeftAmount;
				
				TraceCamera->SetActorLocation(PlayerLocation);
				FRotator Rot = FRotationMatrix::MakeFromX(BallToTraceCameraVector + LeftVector).Rotator();
				TraceCamera->GetCamera()->SetWorldRotation(Rot);
				
				BadPlaceHitFixedCamera->SetActorLocation(PlayerLocation);

				BeginForwardDir = PlayerChar->GetActorForwardVector();

				PlayerChar->GetCameraBoom()->SocketOffset = FVector(0, 0, 100);
				PlayerChar->GetCameraBoom()->SetRelativeLocation(FVector::ZeroVector);
				PlayerChar->GetFollowCamera()->SetRelativeLocation(FVector::ZeroVector);
				
				PlayerChar->UpdateCameraFieldOfView();
				
				PlayerChar->GetFollowCamera()->SetRelativeRotation(FRotator(2, 0, 0));
			}
		}
	}

	CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();
	if(IsValid(CourseMode))
	{
		CourseMode->ClearDebugPuttingState();
	}
}

void ASGCamera::TickCameraWork(float DeltaTime)
{
	if (nullptr == GameInst) return;
	if (nullptr == PlayerController) return;

	ASGPlayerCharacter* Player = Cast<ASGPlayerCharacter>(PlayerController->GetPawn());
	if (nullptr == Player) return;

	if(IsBallImpacted) { OcclusionCheckGrassPassTime += DeltaTime; }
	
	if(OcclusionCheckGrassPassTime > OcclusionGrassCheckTimeInterval)
	{
		//TODO 공이 지면에 닿은 이후 부터 Tick보다는 낮은 주기로 OB인지 아닌지를 판단하여 OB가 아닌 경우 잔디 Occlusion 활성화
		int GroundType = Player->GetMovingStimpMeterType();
		float IsEnableGrassOcclusion = GroundType < 7 ? 1.0f : 0.0f; 
		OcclusionMPCInstance->SetScalarParameterValue("EnableGrassOcclusion", IsEnableGrassOcclusion);
		
		// SG_LOG(Log, "SJW 777 GroundType Check....%i", GroundType);
		OcclusionCheckGrassPassTime = 0;
	}

	if(OcclusionMPC)
	{
		if(!OcclusionMPCInstance)
		{
			OcclusionMPCInstance = GetWorld()->GetParameterCollectionInstance(OcclusionMPC.Get());
		}
		if(OcclusionMPCInstance)
		{
			OcclusionMPCInstance->SetVectorParameterValue("BallLocation", Player->GetActorLocation());
		}
	}

	Player->TraceCameraLastLocation = TraceCamera->GetActorLocation();

	if(Player->GetIsHitBadPlace())
	{
		if(!IsHitBadPlace)
		{
			if(Player->GetLastHitSurface() == SurfaceType14)
			{
				// FVector BeforeBallForwardVector = Player->GetActorForwardVector().GetSafeNormal();
				// FVector CurrentBallForwardVector = Player->GetActorForwardVector().GetSafeNormal();
				//
				// float DotProduct = FVector::DotProduct(BeforeBallForwardVector, CurrentBallForwardVector);
				// float AngleInRadian = FMath::Acos(FMath::Clamp(DotProduct, -1.0f, 1.0f));
				// 	
				// float Degree = FMath::RadiansToDegrees(AngleInRadian);
				// SG_LOG(Log, "Tree hit After Degree %f", Degree);
				//
				// if((Degree > 15) && Player->GetFlyingCheck())
				// {
				// 	SG_LOG(Log, "SJW Hit BadPlace");
				// 	IsHitBadPlace = true;
				// }

				IsHitBadPlace = true;
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
		// ResultRecord = GetTargetPuttingCameraRecordByIndex(7);
		// PuttingCamNum = 1;
		//end test code

		float BallToHoleCupDist = FVector::Dist2D(Player->GetActorLocation(), holecupLocation);

		//퍼팅 고정 카메라
		if (PuttingCamNum == 2)
		{
			if(BallToHoleCupDist > SG_HOLE_CUP_DIAMETER)
			{
				PuttingFixedCameraWork(PlayerController, Player, DeltaTime);
			}
		}
		// 추적 카메라 (정방향)
		else
		{
			if(BallToHoleCupDist < SG_HOLE_CUP_RADIUS)
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
				if(IsHoleIn && (BallToHoleCupDist > 10))
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
					//test code
					// ZoomCameraActiveDist = 200;
					//end test code
					
					if(BallToHoleCupDist <= ZoomCameraActiveDist)
					{
						PuttingZoomCameraWork(PlayerController, Player, DeltaTime);
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

		if(flyingTime >= 0.2f && !IsPredictApexSet)
		{
			PredictApexPosition(Player->GetActorLocation(), Player->GetVelocity(), Player->GetMagnusVector());
			if(IsValid(CameraRailActor))
			{
				CameraRailActor->UpdatePredictedApex(predictApexPosition);
			}

			float DiffGroundFixToApex = FMath::Abs(predictApexPosition.Z - startLocation.Z);
			IsApexUnderGroundFix = DiffGroundFixToApex < NoneGreenTrailLimitHeight;
			
			IsPredictApexSet = true;
		}
		
		if(IsLanding){
			if(!IsApexPositionSet) { apexPosition = Player->GetActorLocation();	}
			IsApexPositionSet = true;
		}

		if(IsLanding && !IsBallImpacted) { LandingTime += DeltaTime; }
		
		if(IsLanding && !IsLowApexInitialized){
			float BallZPos = Player->GetActorLocation().Z;
			float DiffZBallToStartPosZ = FMath::Abs(BallZPos - startLocation.Z);
			IsLowApex = DiffZBallToStartPosZ < 1000.0f;
		
			IsLowApexInitialized = true;

			SG_LOG(Log, "DiffZBallToStartPosZ %f", DiffZBallToStartPosZ);
			if(!IsLowApex){ SG_LOG(Log, "IsLow Apex is False"); }
			else{ SG_LOG(Log, "IsLow Apex is True"); }
		}
		
		if(IsLanding && !Player->GetFlyingCheck())
		{
			// SG_LOG(Log, "SJW [000] Ball Impacted ground...");

			if(!PrevBallImpacted)
			{
				BallImpactedPosition = Player->GetActorLocation();
				BallVelocityBeforeImpacted = Player->GetVelocity();
				IsBallImpacted = true;
				PrevBallImpacted = true;
			}

			ImpactedTime += DeltaTime;
		}

		if(IsLanding && IsCloseToGround(Player, 200))
		{
			if(!IsBallAlmostImpacted)
			{
				TraceCameraPrevLocationAfterBallImpacted = TraceCamera->GetCamera()->GetComponentLocation();
				IsBallAlmostImpacted = true;
			}
		}

		if(IsHitBadPlace)
		{
			// AfterApexCamNum = 3;
			// IsAfterApexCamNumDecided = true;
			// InitBadPlaceHitCameraWork(Controller, Player);
			//
			// CameraStep = 1;
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
				//시작 고정 카메라 [회전x]	
				case 0:
					FixedCameraWithNoRotateWork(Player);
					break;
				//기본 시작 고정 카메라
				case 1:
					FixedCameraWork(Player);
					break;

				//(정방향) 추적 카메라	
				case 2:
					TraceCameraWork(PlayerController, Player,	DeltaTime);
					break;

				case 3:
					SkyTraceCameraWork(PlayerController, Player, DeltaTime);
				}
			}
			else
			{
				switch (BeforeApexCamNum)
				{
				case 0:
					InitFixedCameraWithNoRotateWork(PlayerController, Player);
					break;
				case 1:
					InitFixedCameraWork(PlayerController, Player);
					break;

				case 2:
					InitTraceCameraWork(PlayerController, Player);
					break;

				case 3:
					InitSkyTraceCameraWork(PlayerController, Player);
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
					ReverseFixedCameraWork(PlayerController, Player);
					break;
					
				//측면 카메라
				case 2:
					if(FlagCameraToReverseCamera)
					{
						sideCameraPassTime = flyingTime - sideCameraBeginTime;
					}
					
					{
						if(IsLanding && IsAvailableThirdCamera(PlayerController, Player) && !IsHitBadPlace)
						{
#ifdef ENABLE_DEBUG_CAMERA
							if(IsValid(PHYSICAL_MANAGER))
							{
								int DebugThirdCameraIndex = PHYSICAL_MANAGER->GetCameraThreeIndex();
								bool IsDebugCameraExist = DebugThirdCameraIndex != 0;
								if(IsDebugCameraExist)
								{
									SG_LOG(Log, "[DebugCamera] Third Camera");
								
									AfterApexCamNum = 1;

									InitReverseFixedCameraWork(PlayerController, Player->GetActorLocation());
									ReverseFixedCameraWork(PlayerController, Player);

									return;
								}
							}
#endif
							
							if(IsValid(SelectedSecondCameraRecord))
							{
								if(SelectedSecondCameraRecord->Third_Camera_Result.ToLower() == "land_fixed")
								{
									FlagCameraToReverseCamera = false;
							
									if(!IsPredictWrongLandingZone())
									{
										// SG_LOG(Log, "SJW 002 Reverse Fixed Camera!!!!");
								
										AfterApexCamNum = 1;

										InitReverseFixedCameraWork(PlayerController, Player->GetActorLocation());
										ReverseFixedCameraWork(PlayerController, Player);
									}
								}
								else if(SelectedSecondCameraRecord->Third_Camera_Result == "shot_follow")
								{
									AfterApexCamNum = 3;
									
									InitTraceCameraWork(PlayerController, Player);
									TraceCameraWork(PlayerController, Player, DeltaTime);
								}
							}
							
						}
					}
					
					SideCameraWork(Player);
					break;
					
				//(정방향) 추적 카메라
				case 3:
					TraceCameraWork(PlayerController, Player, DeltaTime);
					break;

				//측면 45도 카메라 (구. 사이드 카메라)
				case 4:
					{
						if(IsLanding && IsCloseToGround(Player, SideCameraRemainHeightCM) && !IsPredictWrongLandingZone())
						{
							SG_LOG(Log, "SJW 002 Reverse Fixed Camera!!!!");
							AfterApexCamNum = 1;
							
							InitReverseFixedCameraWork(PlayerController, Player->GetActorLocation());
							ReverseFixedCameraWork(PlayerController, Player);
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

				case 6:
					{
						SkyTraceCameraWork(PlayerController, Player, DeltaTime);
					}
					break;

				case 7:
					{
						FixedCameraWork(Player);
					}
				}
			}
			else
			{
 				if(!IsDrivingMode) DecideAfterCamNum();
				else
				{
					IsAfterApexCamNumDecided = true;
					AfterApexCamNum = 3;
					return;
				}
 #ifdef ENABLE_DEBUG_CAMERA
 				// {
 				// 	bool IsDebugCameraIndexExist = PHYSICAL_MANAGER->GetCameraTwoIndex() != 0;
 				// 	if(IsDebugCameraIndexExist)
 				// 	{
 				// 		DecideAfterCamNum();
 				// 	}
 				// 	else
 				// 	{
 				// 		if(BeforeApexCamNum == 0) { AfterApexCamNum = -1; }
 				// 		else { AfterApexCamNum = 3; }
 				// 	}
 				// }

 				if(IsDebugSkyTraceState){ AfterApexCamNum = 6; }
				
				// test code
				// AfterApexCamNum = 2;
				// end test code
 #endif
				
				switch (AfterApexCamNum)
				{
				case 1:
					InitReverseFixedCameraWork(PlayerController, Player->GetActorLocation());
					break;

				case 2:
					sideCameraBeginTime = flyingTime;
					InitSideCameraWork(PlayerController, Player);
					break;

				case 3:
					if(BeforeApexCamNum != 3) InitTraceCameraWork(PlayerController, Player);
					break;

				case 4:
					InitSideBellowCameraWork(PlayerController, Player);
					break;

				case 5:
					InitBadPlaceHitCameraWork(PlayerController, Player);
					break;

				case 6:
					if(BeforeApexCamNum != 3) InitSkyTraceCameraWork(PlayerController, Player);
					//Note. BeforeApexCamNum이 추적카메라인 경우, 한프레임 아무것도 호출되지 않는 경우
					//카메라 멈추는 듯한 느낌을 주는 것으로 판단되어 그 경우, SkyTraceCameraWork를 호출해준다.
					else { SkyTraceCameraWork(PlayerController, Player, DeltaTime); }
					break;
					
				case 7:
					if(BeforeApexCamNum != 1) InitFixedCameraWork(PlayerController, Player);
					break;
				}

				IsAfterApexCamNumDecided = true;
			}
		}
	}

	if (false == Player->GetBallMoving())
	{
		CanTickCameraWork = false;

		if (AfterApexCamNum != 6)
		{
			ActiveEndShotWaitCamera();
		}
	}
}

//AfterApexCamNum을 결정한다.
void ASGCamera::DecideAfterCamNum()
{
	AfterApexCamNum = -1;
	
	if (nullptr == GameInst) return;
	if (nullptr == PlayerController) return;
	if (nullptr == PlayerChar) return;

	CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();

	FVector BallLocation = PlayerChar->GetActorLocation();
	FVector BallVelocity = PlayerChar->GetVelocity();
	FVector BowlerFreeViewLastPosition = PlayerChar->GetBowlerFreeView()->GetLastLocation();
	FVector MagnusVector = PlayerChar->GetTotalMagnusVector();

	if(IsHitBadPlace) {	AfterApexCamNum = 5; }
	
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

	if (PlayerChar->GetCapsuleComponent()->IsSimulatingPhysics() == true)
	{
		playerMass = PlayerChar->GetCapsuleComponent()->GetMass();
	}

	FVector TargetWindForceVector = playerMass * (WindPower * 100.0f * windforcePowerRatio) * WindArrowDirection * 0.2f;

	float dotProduct = FVector::DotProduct(PlayerChar->GetVelocity(), TargetWindForceVector);
	if (dotProduct > 0)
	{
		// 순풍
		TargetWindForceVector *= 0.5f;		// 순풍은 역풍 영향의 절반
	}

	//예상 낙구 지점 연산
	PredictLandingPosition(BallLocation, BallVelocity, MagnusVector, BowlerFreeViewLastPosition, TargetWindForceVector, PlayerChar->GetMagnusForceCount());

	SelectedSecondCameraRecord = GetTargetSecondCameraPriority(1, PlayerChar);
	if(IsValid(SelectedSecondCameraRecord))
	{
		AfterApexCamNum = GetAfterApexCamNumFromStringValue(SelectedSecondCameraRecord->Camera_Result);
	}
	bool IsTraceCamera = (AfterApexCamNum == 3) || (AfterApexCamNum == 6);
	PlayerChar->UpdateBallTail(IsTraceCamera);

#ifdef ENABLE_OCCLUSION
	bool IsOcclusionEnable = IsTraceCamera || (AfterApexCamNum == 1);
	if(OcclusionMPCInstance)
	{
		float TargetParValue = IsOcclusionEnable ? 1.0f : 0.0f; 
		if(IsOcclusionEnable)
		{
			OcclusionMPCInstance->SetScalarParameterValue("EnableOcclusion", TargetParValue);
		}
	}
#endif

	IsFoliageTimerInitialized = true;
}

void ASGCamera::InitFixedCameraWithNoRotateWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	StartFixedCamera->SetActorLocation(Player->GetActorLocation());
	
	StartFixedCamera->GetSpringArm()->SocketOffset = FVector(0, 0, 100);

	StartFixedCamera->GetCamera()->SetRelativeLocation(FVector::ZeroVector);
	StartFixedCamera->GetCamera()->SetRelativeRotation(FRotator::ZeroRotator);
	StartFixedCamera->GetCamera()->FieldOfView = Player->GetFollowCamera()->FieldOfView;
	
	FRotator CurrentRotation = Player->GetCameraBoom()->GetTargetRotation();
	StartFixedCamera->GetCamera()->SetWorldRotation(CurrentRotation);
	
	Controller->SetViewTarget(StartFixedCamera);
}

void ASGCamera::InitFixedCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	StartFixedCamera->GetSpringArm()->bDoCollisionTest = false;
	
	StartFixedCamera->GetCamera()->FieldOfView = Player->GetFollowCamera()->FieldOfView;
	
	FVector FollowCameraSocketOffset = Player->GetCameraBoom()->SocketOffset;
	FVector UnfixedPosition = Player->GetCameraBoom()->GetUnfixedCameraPosition();

	FRotator SpringArmTargetRotation = Player->GetCameraBoom()->GetTargetRotation();
	SpringArmTargetRotation.Yaw += ShiftRotateAngle;
	StartFixedCamera->GetCamera()->SetWorldRotation(SpringArmTargetRotation);
	
	StartFixedCamera->GetSpringArm()->SocketOffset = FollowCameraSocketOffset;
	
	StartFixedCamera->GetCamera()->SetUsingAbsoluteLocation(true);
	
	GroundCheck(UnfixedPosition, UnfixedPosition);
	StartFixedCamera->GetCamera()->SetWorldLocation(UnfixedPosition);
	
	Controller->SetViewTarget(StartFixedCamera);
}

void ASGCamera::FixedCameraWithNoRotateWork(ASGPlayerCharacter* Player) { }

void ASGCamera::FixedCameraWork(ASGPlayerCharacter* Player) { }


//(정방향) 추적 카메라 초기화
void ASGCamera::InitTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	if(IsInitTraceCamera) return;
	IsInitTraceCamera = true;
	
	if(IsDrivingMode)
	{
		FVector UnfixedPosition = Player->GetCameraBoom()->GetUnfixedCameraPosition();
		FRotator TargetRotation = Player->GetCameraBoom()->GetTargetRotation();

		TraceCamera->GetSpringArm()->bDoCollisionTest = false;
		TraceCamera->GetSpringArm()->bEnableCameraLag = false;
		TraceCamera->GetSpringArm()->bEnableCameraRotationLag = false;
		
		TraceCamera->GetSpringArm()->SocketOffset = FVector(0, 0, 100);
		TraceCamera->SetActorLocation(Player->GetActorLocation());

		TraceCamera->GetSpringArm()->SetWorldRotation(TargetRotation);
		TraceCamera->GetCamera()->SetWorldLocation(UnfixedPosition);
	}
	else
	{
		FVector BallLocation = Player->GetActorLocation();
		TraceCamera->GetCamera()->FieldOfView = Player->GetFollowCamera()->FieldOfView;
	
		float OffSetShiftRotateAngle = 0.0f;
		if(!IsTeeShot){ OffSetShiftRotateAngle = ShiftSecondShotOffSetAngle; }
	
		FVector CamToBallDirVector = Player->GetActorLocation() - TraceCamera->GetCamera()->GetComponentLocation();
		FVector ShiftedCamToBallDirVector = CamToBallDirVector.RotateAngleAxis(ShiftRotateAngle + OffSetShiftRotateAngle, FVector::ZAxisVector);
		ShiftedCamToBallDirVector.Z = 0;
		FRotator TargetRot = FRotationMatrix::MakeFromX(ShiftedCamToBallDirVector).Rotator();
	
		float BackwardLength = 300;
	
		FVector FollowCameraLoc = ((BallLocation - Player->GetActorForwardVector() * BackwardLength));
		GroundCheck(FollowCameraLoc, FollowCameraLoc);
		
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
	if(IsValid(PHYSICAL_MANAGER))
	{
		bool IsDebugIndexExist = PHYSICAL_MANAGER->GetCameraTwoIndex() != 0;
		if(IsDebugIndexExist) ConditionResult = true;
	}
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

	//test code
	IsCameraRightCondition = true;
	//end test code
	
	if (Player->GetInOutFanceBoundarieWhite())
	{
		if(IsCameraRightCondition)
		{
			SG_LOG(Log, "SJW 110 normal right camera case aaa");
			
			sideCameraLocation += SideDir * ResultSideCameraDist;
			
			// bool IsOB = IsOBZPos(sideCameraLocation);
			// if(IsOB)
			// {
			// 	//side spin이 1000 이상인 경우 apex 지점 방향으로 이동하면서 holebox가 감지되는 지점까지 카메라를 당긴다.
			// 	if(FMath::Abs(sideSpinRate) > 1000)
			// 	{
			// 		FVector ResultLoc = sideCameraLocation;
			// 		GetFairWayPosFromOBToApex(sideCameraLocation, NewApexPredictPosition, ResultLoc);
			// 		sideCameraLocation = ResultLoc;
			// 		sideCameraLocation -= SideDir * 500;
			// 	}
			// 	else
			// 	{
			// 		sideCameraLocation -= SideDir * ResultSideCameraDist * 0.2f;
			// 	}
			// }
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
 
	GroundCheck(SideCamera->GetCamera()->GetComponentLocation(), sideCameraLocation);
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
	FVector BallLocation = Player->GetActorLocation();

	FRotator SpringArmTargetRotation = Player->GetCameraBoom()->GetTargetRotation();
	FVector FollowCameraSocketOffset = Player->GetCameraBoom()->SocketOffset;
	
	float BackwardLength = 300;
    	if(IsValid(SelectedPuttingCameraRecord)){ BackwardLength = SelectedPuttingCameraRecord->Follow_Camera_Distance * 100; }

	float BackwardOffset = 50.0f;
	
	FVector FollowCameraLoc = ((BallLocation - BallForwardVectorAfterImpact * (BackwardLength + BackwardOffset)) + FollowCameraSocketOffset);
	// FVector LeftVector = FollowCameraLoc.RotateAngleAxis(90, FVector::ZAxisVector).GetSafeNormal() * 50;
	// LeftVector.Z = 0;
	// FollowCameraLoc += LeftVector;
	
	PuttingTraceCamera->GetCamera()->FieldOfView = Player->GetFollowCamera()->FieldOfView + 10;
	PuttingTraceCamera->GetCamera()->SetWorldLocation(FollowCameraLoc);
	
	SpringArmTargetRotation.Yaw += ShiftRotateAngle;
	
	PuttingTraceCamera->GetCamera()->SetWorldRotation(SpringArmTargetRotation);

	SG_LOG(Log, "SJW InitPuttingTraceCameraWork : %s", *FollowCameraLoc.ToString());
	
	Controller->SetViewTarget(PuttingTraceCamera);
}

void ASGCamera::PuttingTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime)
{
	FVector BallLocation = Player->GetActorLocation();
	
	FVector FollowCameraSocketOffset = Player->GetCameraBoom()->SocketOffset;

	float BackwardLength = 300;
	if(IsValid(SelectedPuttingCameraRecord)){ BackwardLength = SelectedPuttingCameraRecord->Follow_Camera_Distance * 100; }
	
	float BackwardOffset = 50.0f;
	
	FVector FollowCameraLoc = (
		(BallLocation - BallForwardVectorAfterImpact * (BackwardLength + BackwardOffset)) + FollowCameraSocketOffset);
	
	if(CanTraceCamera)
	{
		// FVector LeftVector = FollowCameraLoc.RotateAngleAxis(90, FVector::ZAxisVector).GetSafeNormal() * 50;
		// FollowCameraLoc += LeftVector;

		PuttingTraceCamera->GetCamera()->SetWorldLocation(FollowCameraLoc);
	}

	if(CanRotateTraceCamera)
	{
		FRotator SpringArmTargetRotation = Player->GetCameraBoom()->GetTargetRotation();
		SpringArmTargetRotation.Yaw += ShiftRotateAngle;
		PuttingTraceCamera->GetCamera()->SetWorldRotation(SpringArmTargetRotation);
	}
}

void ASGCamera::PuttingZoomCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime)
{
	float BallToHoleDist = FVector::Dist2D(holecupLocation, Player->GetActorLocation());

	float CurBallToHoleDist = FVector::Dist2D(Player->GetActorLocation(), holecupLocation);
	CanRotateTraceCamera = CurBallToHoleDist > 30;

	if(!IsPuttingZoomCameraSetLocationRotation)
	{
		FVector PuttingTraceCameraLocation = PuttingTraceCamera->GetCamera()->GetComponentLocation();
		FRotator PuttingTraceCameraRotation = PuttingTraceCamera->GetCamera()->GetComponentRotation();

		PuttingZoomCamera->SetActorLocation(Player->GetActorLocation());
		PuttingZoomCamera->GetCamera()->SetWorldLocation(PuttingTraceCameraLocation);
		PuttingZoomCamera->GetCamera()->SetWorldRotation(PuttingTraceCameraRotation);
			
		IsPuttingZoomCameraSetLocationRotation = true;
	}
	
	//공이 일정 이상 떨어져 있고, 공이 홀에 점점 가까워지는중
	if(CurBallToHoleDist < PrevBallToHoleDist)
	{
		// SG_LOG(Log, "SJW 800 PuttingTraceCameraWork....111");

		float ResultPuttingCameraZoomInMin = IsValid(SelectedPuttingCameraRecord) ?
			SelectedPuttingCameraRecord->Zoom_Camera_Ratio : PuttingCameraZoomInMin;

		//test code
		// ResultPuttingCameraZoomInMin = 40;
		//end test code

		float CurrentFOV = PuttingZoomCamera->GetCamera()->FieldOfView;

		if(bIsZoomInLerping)
		{
			if(!CanRotateTraceCamera)
			{
				bIsZoomInLerping = false;
				return;
			}
			ZoomInPassTime += DeltaTime;

			float TargetAlpha = ZoomInPassTime / ZoomInLerpTime;
			float TargetLerpFOV = FMath::Lerp(CurrentFOV, ResultPuttingCameraZoomInMin, TargetAlpha);
			PuttingZoomCamera->GetCamera()->FieldOfView = TargetLerpFOV;

			SG_LOG(Log, "SJW PuttingZoom ZoomInPassTime %f", ZoomInPassTime);
			
			if(ZoomInPassTime > ZoomInLerpTime)
			{
				bIsZoomInLerping = false;
			}
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
		Controller->SetViewTargetWithBlend(PuttingZoomCamera, 0.1f, VTBlend_EaseIn);
	}
	
	PrevBallToHoleDist = BallToHoleDist;
}

void ASGCamera::InitPuttingFixedCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	IS_VALID_RETURN(SelectedPuttingCameraRecord);
	
	FVector PlayerFollowCameraLocation = Player->GetActorLocation();
	FVector NewHoleCupLocation = holecupLocation;
	NewHoleCupLocation.Z = PlayerFollowCameraLocation.Z;
	FVector BallLocation = Player->GetActorLocation();

	//사이드 배치인 경우, 홀컵과 공 사이 어느 지점에서부터 사이드로 이동시킬 것인지 기준점
	//음수인 경우, 역방향 [공과 홀컵을 반대쪽에서 바라본다.]
	float PuttSidePosition = SelectedPuttingCameraRecord->Putt_Side_Position;
	int32 RotateDegree = SelectedPuttingCameraRecord->Putt_Fixed_Degree;
	float DistCameraToHoleCup = SelectedPuttingCameraRecord->Putt_Fixed_Distance * 100;
	
	FVector OriginHoleToCamNormalVector = BallLocation - NewHoleCupLocation;
	FVector HoleToCamNormalVector = OriginHoleToCamNormalVector;
	HoleToCamNormalVector = HoleToCamNormalVector.RotateAngleAxis(-1 * RotateDegree, FVector::ZAxisVector);
	HoleToCamNormalVector.Normalize();
	HoleToCamNormalVector.Z = 0;
	
	SG_LOG(Log, "SJW NewHoleCupLocation : %s", *NewHoleCupLocation.ToString());
	SG_LOG(Log, "SJW HoleToCamNormalVector : %s", *HoleToCamNormalVector.ToString());
	SG_LOG(Log, "SJW DistCameraToHoleCup : %f", DistCameraToHoleCup);
	
	SG_LOG(Log, "SJW PlayerFollowCameraLocation : %s", *PlayerFollowCameraLocation.ToString());

	bool IsRotateZero = RotateDegree == 0;
	FVector NewTargetCameraLocation = IsRotateZero ?
		PlayerFollowCameraLocation - BallForwardVectorAfterImpact * 300 + FVector(0, 0, 100) :
		NewHoleCupLocation + HoleToCamNormalVector * DistCameraToHoleCup;
	
	if(RotateDegree != 0)
	{
		float OffSet = DistCameraToHoleCup * PuttSidePosition;
		FVector OffSetVector = OriginHoleToCamNormalVector.GetSafeNormal() * OffSet;
		OffSetVector.Z = 0;
		NewTargetCameraLocation += OffSetVector;
	}
	
	SG_LOG(Log, "SJW BallLocation : %s", *BallLocation.ToString());
	
	if((RotateDegree == 0)) { PuttingFixedCamera->GetCamera()->FieldOfView = Player->GetFollowCamera()->FieldOfView; }
	else { PuttingFixedCamera->GetCamera()->FieldOfView = 80; }
	
	float HeightOffset = 0;
	if(IsValid(SelectedPuttingCameraRecord) && (SelectedPuttingCameraRecord->Putt_Fixed_Height > 0))
	{
		HeightOffset = SelectedPuttingCameraRecord->Putt_Fixed_Height * 100;
	}
	
	NewTargetCameraLocation.Z += HeightOffset;
	
	PuttingFixedCamera->SetActorLocation(NewHoleCupLocation);
	PuttingFixedCamera->GetCamera()->SetWorldLocation(NewTargetCameraLocation);

	{
		float Putt_Hole_Fixed = SelectedPuttingCameraRecord->Putt_Hole_Fixed;
		bool IsPuttHoleFixedCond = Putt_Hole_Fixed == 1;

		FVector CamToLookVector = Player->GetActorLocation() - PuttingFixedCamera->GetCamera()->GetComponentLocation();
		FVector ShiftedCamToLookVector = CamToLookVector.RotateAngleAxis(ShiftRotateAngle + ShiftPuttingShotOffSetAngle, FVector::ZAxisVector);
		FVector ShiftedHoleCupLookVector = (holecupLocation - PuttingFixedCamera->GetCamera()->GetComponentLocation())
			.RotateAngleAxis(ShiftRotateAngle + ShiftPuttingShotOffSetAngle, FVector::ZAxisVector);
		
		FVector LookTarget = IsPuttHoleFixedCond ?
			ShiftedHoleCupLookVector :
			ShiftedCamToLookVector;
	
		FRotator Rot = FRotationMatrix::MakeFromX(LookTarget).Rotator();
		PuttingFixedCamera->GetCamera()->SetWorldRotation(Rot);

		SG_LOG(Log, "SJW NewTargetCameraLocation : %s", *NewTargetCameraLocation.ToString());
	}
	Controller->SetViewTarget(PuttingFixedCamera);
}

void ASGCamera::PuttingFixedCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime)
{
	IS_VALID_RETURN(SelectedPuttingCameraRecord);

	float Putt_Hole_Fixed = SelectedPuttingCameraRecord->Putt_Hole_Fixed;
	bool IsPuttHoleFixedCond = Putt_Hole_Fixed == 1;

	FVector CamToLookVector = Player->GetActorLocation() - PuttingFixedCamera->GetCamera()->GetComponentLocation();
	FVector ShiftedCamToLookVector = CamToLookVector.RotateAngleAxis(ShiftRotateAngle + ShiftPuttingShotOffSetAngle, FVector::ZAxisVector);
	FVector ShiftedHoleCupLookVector = (holecupLocation - PuttingFixedCamera->GetCamera()->GetComponentLocation())
		.RotateAngleAxis(ShiftRotateAngle + ShiftPuttingShotOffSetAngle, FVector::ZAxisVector);
	FVector LookTarget = IsPuttHoleFixedCond ?
		ShiftedHoleCupLookVector :
		ShiftedCamToLookVector;
	
	FRotator Rot = FRotationMatrix::MakeFromX(LookTarget).Rotator();
	PuttingFixedCamera->GetCamera()->SetWorldRotation(Rot);
}

//(정방향) 추적 카메라 Tick 단위 처리
void ASGCamera::TraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime)
{
	if(IsDrivingMode) {	DrivingModeTraceCameraSubWork(Controller, Player, DeltaTime); }		//연습장인 경우
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
	FVector NewTargetCameraLocation = TraceCamera->GetCamera()->GetComponentLocation();
	
	float BallToCameraDist = FVector::Dist(BallLocation, NewTargetCameraLocation);
	// TraceCamera->GetSpringArm()->TargetArmLength = 1000;
	
	if(CameraStep == 0)
	{
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
	}
	else if(CameraStep == 1)
	{
		float TargetSheetFollowCamDist = SelectedSecondCameraRecord->Follow_Camera_Distance * 100;

		float CurrentArmLength = TraceCamera->GetSpringArm()->TargetArmLength;
		if(!IsBallImpacted && (CurrentArmLength < TargetSheetFollowCamDist))
		{
			TraceCamera->GetSpringArm()->TargetArmLength += 10;
		}
	}

	#pragma region FOVChange
	if(IsTeeShot && !IsPutting && (launchAngle > 7))
	{
		float CurrentFOV = TraceCamera->GetCamera()->FieldOfView;
		if(CurrentFOV <= 80) { TraceCamera->GetCamera()->FieldOfView += 1; }
	}
	#pragma endregion

	#pragma region SocketOffsetChange
	if((flyingTime > 0.8f) && CanTraceCamera)
	{
		if(bIsTraceCameraSocketChangeLerping)
		{
			TraceCameraSocketChangeTime += DeltaTime;

			float SocketAlpha = TraceCameraSocketChangeTime /  TraceCameraSocketChangeLerpTime;
			FVector LerpSocketOffset = FMath::Lerp(
				FVector(0, 0, 0),
				FVector(0, 0, -150),
				SocketAlpha
			);
			
			TraceCamera->GetSpringArm()->SocketOffset = LerpSocketOffset;

			if(TraceCameraSocketChangeTime >= TraceCameraSocketChangeLerpTime)
			{
				bIsTraceCameraSocketChangeLerping = false;
			}
		}
	}
	#pragma endregion

	#pragma region CameraTrace
	if(CanTraceCamera)
	{
		FVector PrevCameraLocation = NewTargetCameraLocation;
		
		if(IsBallAlmostImpacted)
		{
			//그린에 공이 지면에 닿은 이후부터 카메라 고도 유지
			NewTargetCameraLocation.Z = TraceCameraPrevLocationAfterBallImpacted.Z;
		}
		
		TraceCamZPosFix(BallLocation, NewTargetCameraLocation);

		float TargetAlpha = !IsBallAlmostImpacted ? 0.5f : 0.1f;
		
		//순서 중요
		FVector TargetLerpLocation = FMath::Lerp(NewTargetCameraLocation, PrevCameraLocation, TargetAlpha);
		TraceCamera->GetCamera()->SetWorldLocation(TargetLerpLocation);
		
		TraceCamera->SetActorLocation(BallLocation);
	}
	else{
		TraceCamZPosFix(BallLocation, NewTargetCameraLocation);
		TraceCamera->GetCamera()->SetWorldLocation(NewTargetCameraLocation);
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

	bool IsOutSafeScreenArea = false;
	bool UpperUnSafe = false;
	bool BottomUnsafe = false;
	
	#pragma region CameraRotate
	if(CanRotateTraceCamera)
	{
		if(!IsPutting)
		{
			FVector2D ScreenLocation;
			bool bIsOnScreen = Controller->ProjectWorldLocationToScreen(Player->GetActorLocation(), ScreenLocation);
			if(bIsOnScreen)
			{
				//7도 이하에서는 SafeArea 처리를 하지 않는다.
				if(launchAngle <= 7)
				{
					IsOutSafeScreenArea = true;
				}
				else
				{
					// SG_LOG(Log, "SJW 777 %hs", IsOutSafeScreenArea ? "Out SafeArea" : "In SafeArea");
					CheckUnSafeScreenArea(FMath::Abs(ScreenLocation.Y), UpperUnSafe, BottomUnsafe);
				}
			}
		}

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
			if(IsBallAlmostImpacted)
			{
				if(IsBadImpacted)
				{
					FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, 0.5f);
					TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
				}
				else
				{
					//지면에 닿은 이후
					if(IsBallImpacted)
					{
						//처음에 회전을 매우 느리게 해주고
						if(bIsTraceCameraRotateChangeAfterImpactedLerping)
						{
							TraceCameraRotateTimeChangeAfterImpactedTime += DeltaTime;
							
							float CameraRotAlpha = TraceCameraRotateTimeChangeAfterImpactedTime / TraceCameraRotateChangeAfterImpactedTime;

							FRotator ResultRot =  FMath::Lerp(CurrentRot, TargetRot, CameraRotAlpha);
							TraceCamera->GetCamera()->SetWorldRotation(ResultRot);

							if (TraceCameraRotateTimeChangeAfterImpactedTime >= TraceCameraRotateChangeAfterImpactedTime)
							{
								bIsTraceCameraRotateChangeAfterImpactedLerping = false;
							}
						}
						//처음 이후 회전 처리
						else
						{
							TargetRot = FRotationMatrix::MakeFromX(ShiftedCamToBallDirVector).Rotator();
					
							FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, 0.02f);
							TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
						}
					}
					//아직 지면에 닿지 않은 상황
					else
					{
						TargetRot = FRotationMatrix::MakeFromX(ShiftedCamToBallDirVector).Rotator();
					
						FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, 0.02f);
						TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
					}
				}
			}
			else
			{
				if(IsBottomOutSafeAreaDetected)
				{
					FRotator ResultRot = FMath::Lerp(CurrentRot, TargetRot, 0.03f);
					TraceCamera->GetCamera()->SetWorldRotation(ResultRot);
				}
			}
		}
	}
	#pragma endregion

	#pragma region CameraLagChange
	if(flyingTime >= 0.1f)
	{
		if(!IsLanding)
		{
			if (IsTeeShot) 
			{
				if (flyingTime <= 0.8f)
				{
					TraceCamera->GetSpringArm()->CameraLagSpeed = 1;
				}
				else
				{
					//날라가면서 살짝 당겨지는 연출
					if (bIsTraceCameraLagChangeLerping && IsTeeShot)
					{
						TraceCameraLagChangeTime += DeltaTime;

						float CameraLagAlpha = TraceCameraLagChangeTime / TraceCameraLagLerpTime;
						float LerpLagSpeed = FMath::Lerp(1, 4.0f, CameraLagAlpha);

						TraceCamera->GetSpringArm()->CameraLagSpeed = LerpLagSpeed;

						if (TraceCameraLagChangeTime >= TraceCameraLagLerpTime)
						{
							bIsTraceCameraLagChangeLerping = false;
						}
					}
				}
			}
			else 
			{
				TraceCamera->GetSpringArm()->CameraLagSpeed = 2;
			}
		}
		else
		{
			if(BottomUnsafe && !IsBottomOutSafeAreaDetected)
			{
				// SG_LOG(Log, "SJW 777 Descending....And BottomUnsafe area detected");
				IsBottomOutSafeAreaDetected = true;
			}
			
			if(IsBottomOutSafeAreaDetected)
			{
				if(!IsBallAlmostImpacted)
				{
					if(bIsTraceCameraLagChangeLerping2)
					{
						TraceCameraLagChangeTime2 += DeltaTime;
				
						float CameraLagAlpha = TraceCameraLagChangeTime2 / TraceCameraLagLerpTime2;
						float LerpLagSpeed = FMath::Lerp(2, 10.0f, CameraLagAlpha);
				
						TraceCamera->GetSpringArm()->CameraLagSpeed = LerpLagSpeed;
				
						if(TraceCameraLagChangeTime2 >= TraceCameraLagLerpTime2)
						{
							bIsTraceCameraLagChangeLerping2 = false;
						}
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

		TraceCamZPosFix(BallLocation, CurrentCameraLocation);

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
		CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();

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
	if (nullptr == GameInst) return;
	if (nullptr == PlayerController) return;
	if (nullptr == PlayerChar) return;

	if(IsBallImpacted) return;
	
	if (oldLocation.Z > PlayerChar->GetActorLocation().Z)
	{
		IsLanding = true;
	}
	else
	{
		IsLanding = false;
	}
	
	oldLocation = PlayerChar->GetActorLocation();
}

void ASGCamera::InitCameraRelativeRotation()
{
	TraceCamera->GetCamera()->SetRelativeRotation(FRotator::ZeroRotator);
	StartFixedCamera->GetCamera()->SetRelativeRotation(FRotator::ZeroRotator);
	ReverseCamera->GetCamera()->SetRelativeRotation(FRotator::ZeroRotator);
	PuttingFixedCamera->GetCamera()->SetRelativeRotation(FRotator::ZeroRotator);
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
				}
			}
		}
		
		predictLandingPosition = PathResult.PathData.Last().Location;
		if((predictLandingPosition.X == 0) && (predictApexPosition.Y == 0))
		{
			SG_LOG(Log, "SJW predictLandingPosition zero case...");
			
			predictApexPosition = PathResult.PathData[0].Location;
		}

		// SG_LOG(Log, "SJW 111 BallLocation %s", *BallLocation.ToString());
		// SG_LOG(Log, "SJW 111 TargetWindForceVector %s", *TargetWindForceVector.ToString());
		// SG_LOG(Log, "SJW 111 origin Predict End Position %s", *predictLandingPosition.ToString());
		// SG_LOG(Log, "SJW 111 Magnus Velocity %s", *MagnusVelocity.ToString());
		// SG_LOG(Log, "SJW 111 PathNum %i", PathNum);
		// SG_LOG(Log, "SJW 111 predictApexPosition %s", *predictApexPosition.ToString());
		SG_LOG(Log, "PathNum : %i, WindVector : %s", PathNum, *WindVector.ToString());
		
		// predictLandingPositionForSkyTrace = predictLandingPosition;
		
		if(GameInst != nullptr)
		{
			predictLandingPosition.X += MagnusVelocity.X;
			predictLandingPosition.Y += MagnusVelocity.Y;
		}
	}
	else
	{
		predictLandingPosition = BallLocation;
	}

	GetGroundFromPosition(predictLandingPosition, FVector::DownVector, predictLandingPosition);

	// SG_LOG(Log, "predictLandingPosition origin %s", *predictLandingPosition.ToString());
	FVector LeftDirVector = (predictLandingPosition - BallLocation).RotateAngleAxis(-90.0f, FVector::ZAxisVector).GetSafeNormal();
	LeftDirVector.Z = 0;
	LeftDirVector.Y = 0;
	predictLandingPositionForSkyTrace = predictLandingPosition + LeftDirVector * sideSpinRate;
	predictLandingPositionForSkyTrace.Y -= FMath::Abs(sideSpinRate);
	// SG_LOG(Log, "predictLandingPosition after %s", *predictLandingPositionForSkyTrace.ToString());

	float WindDotRes = FVector::DotProduct(BallVelocity.GetSafeNormal(), WindVector);
	// SG_LOG(Log, "WindDotRes : %f", WindDotRes);

	//역풍인 경우 예측 지점 뒤로 당긴다.
	if((WindDotRes < -0.5f) && (ballPower >= 6000))
	{
		// ASGCourseMode* CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();
		// if(IsValid(CourseMode))	{ CourseMode->CreateDummyTracePoint(predictLandingPositionForSkyTrace, "111"); }
		
		FVector DirBallToStart = (BallLocation - startLocation).GetSafeNormal();
		DirBallToStart.Z = 0;

		FVector WindOffSetVector = DirBallToStart * WindPower * WindVectorLandingPosOffset;
		// SG_LOG(Log, "WindOffSetVector : %s", *WindOffSetVector.ToString());
		
		predictLandingPositionForSkyTrace -= WindOffSetVector;

		// if(IsValid(CourseMode))	{ CourseMode->CreateDummyTracePoint(predictLandingPositionForSkyTrace, "222"); }
	}

	IsPredictGreenLanding = IsOnGreen(predictLandingPositionForSkyTrace);
	if(IsPredictGreenLanding)
	{
		SG_LOG(Log, "Is Green Maybe....");
		SkyTraceStopDist = SkyTraceLowApexStopDist = 400;
		if(IsValid(CameraRailActor))
		{
			CameraRailActor->UpdateLimitGroundZ(GreenTrailLimitHeight);
		}
	}
	else
	{
		if(IsValid(CameraRailActor))
		{
			CameraRailActor->UpdateLimitGroundZ(NoneGreenTrailLimitHeight);
		}
	}

	if(IsValid(CameraRailActor))
	{
		CameraRailActor->IsPredictOBLanding = IsOBZPos(predictLandingPositionForSkyTrace);
	}
}

void ASGCamera::PredictApexPosition(FVector BallLocation, FVector BallVelocity, FVector MagnusVector)
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

	if(bHit && (PathResult.PathData.Num() > 2))
	{
		int Index = 0;
		int AscendCount = 0;
		float MaxZ = PathResult.PathData[0].Location.Z;
		
		for (auto PathData : PathResult.PathData)
		{
			if(PathData.Location.Z > MaxZ)
			{
				MaxZ = PathData.Location.Z;
				predictApexPosition = PathData.Location;
			}

			if(Index > 0)
			{
				float PrevZ = PathResult.PathData[Index - 1].Location.Z;
				if(PrevZ < PathData.Location.Z){ AscendCount++; }
			}
			Index++;
		}

		// SG_LOG(Log, "SJW [111] predictApexPosition before : %s", *predictApexPosition.ToString());

		// if(AscendCount > 20) AscendCount = 20;
		predictApexPosition.Z += AscendCount * MagnusVector.Z + 300.0f;

		// SG_LOG(Log, "SJW [111] predictApexPosition after : %s", *predictApexPosition.ToString());
		// SG_LOG(Log, "SJW [111] AscendCount : %i", AscendCount);
		// SG_LOG(Log, "SJW [111] MagnusVector : %s", *MagnusVector.ToString());
		// predictApexPosition.Z += MagnusVector.Z * AscendCount;
	}
}


void ASGCamera::GroundCheck(FVector CameraLoc, FVector& ResultLoc)
{
	FHitResult HitResult;

	FVector Start = ResultLoc + FVector(0, 0, 10000);
	FVector End = ResultLoc - FVector(0, 0, 100);

	bool bIsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
	if (bIsGround)
	{
		float ResultZ = HitResult.ImpactPoint.Z + GroundCheckUpperAmount;
		ResultLoc.Z = ResultZ;
	}
}

FVector ASGCamera::GetVerticalGroundPos(FVector From)
{
	FHitResult HitResult;

	FVector Start = From;
	FVector End = From - FVector(0, 0, 100000);

	bool bIsGround = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
	if (bIsGround)
	{
		return HitResult.ImpactPoint;
	}

	return From;
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

bool ASGCamera::IsOnGreen(FVector Pos)
{
	// ASGCourseMode* CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();
	// if(IsValid(CourseMode))	{ CourseMode->CreateDummyTracePoint(Pos, "SphereTraceFieldCheck"); }

	float DetectRadius = 500.0f;
	int DetectCount = 8;
	int AnglePerDetect = 360 / DetectCount;

	FVector Start = Pos + FVector(0, 0, 1000);
	FVector End = Pos - FVector(0, 0, 1000);
		
	FHitResult HitResult;
	bool IsDetected = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
	if(IsDetected)
	{
		EPhysicalSurface FindedSurface = UGameplayStatics::GetSurfaceType(HitResult);

		if (FindedSurface == SurfaceType4 || FindedSurface == SurfaceType6) // Green, Apron
		{
			return true;
		}
	}
	
	FVector TargetDir = FVector::LeftVector;
	for(int i = 0; i < DetectCount; i++)
	{
		FVector NextDir = TargetDir.RotateAngleAxis(AnglePerDetect * i, FVector::ZAxisVector);
		FVector TargetLoc = Pos + NextDir * DetectRadius;

		Start = TargetLoc + FVector(0, 0, 1000);
		End = TargetLoc - FVector(0, 0, 1000);
		
		IsDetected = USGGroundChecker::LineTraceGroundCheck(HitResult, Start, End, this);
		if(IsDetected)
		{
			EPhysicalSurface FindedSurface = UGameplayStatics::GetSurfaceType(HitResult);

			if (FindedSurface == SurfaceType4 || FindedSurface == SurfaceType6) // Green, Apron
			{
				return true;
			}
		}
		
		// ASGCourseMode* CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();
		// if(IsValid(CourseMode))	{ CourseMode->CreateDummyTracePoint(TargetLoc, "SphereTraceFieldCheck"); }
	}
	
	return false;
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
			CameraLoc.Z = HitResult.ImpactPoint.Z + 100;
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
	else if(BeforeApexCamNum == 3)
	{
		SG_LOG(Log, "SJW 002 InitBadPlaceHitCameraWork case bbb");
		
		TargetLocation = SkyTraceCamera->GetCamera()->GetComponentLocation();
	}
	else
	{
		SG_LOG(Log, "SJW 002 InitBadPlaceHitCameraWork case ccc");
		
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
	if(nullptr == GameInst) return nullptr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return nullptr;

	UFirstCameraRecord* TargetRecord = CameraTable->m_FirstCameraSheet->GetRecord(ID);
	return TargetRecord;
}

TArray<UFirstCameraRecord*> ASGCamera::GetFirstCameraRecordsByPriority(int32 ID)
{
	TArray<UFirstCameraRecord*> TargetArr;
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

TArray<USecondCameraRecord*> ASGCamera::GetSecondCameraRecordsByConditions(FString Name, float BallPower, float LaunchDegree)
{
	int TeeShotValue = IsTeeShot ? 1 : 0;
	TArray<USecondCameraRecord*> TargetSecondCameraRecords = GameInst->GetTableManager()->GetSecondCameraRecordsByFirstCameraConditions(Name, TeeShotValue, BallPower, LaunchDegree);

	//1. 조건이 확실히 만족 안되는 Records들은 목록에서 제거한다.
	return TargetSecondCameraRecords;
}

USecondCameraRecord* ASGCamera::GetTargetSecondCameraPriority(int32 Priority, ASGPlayerCharacter* Player)
{
#ifdef ENABLE_DEBUG_CAMERA
	if (IsValid(PHYSICAL_MANAGER))
	{
		int DebugSecondCameraIndex = PHYSICAL_MANAGER->GetCameraTwoIndex();
		bool IsDebugIndexExist = DebugSecondCameraIndex != 0;
		if(IsDebugIndexExist)
		{
			SG_LOG(Log, "SJW [DebugCamera] Second Camera %i", DebugSecondCameraIndex);

			return GetSecondCameraRecordByIndex(DebugSecondCameraIndex);
		}
	}
#endif
	
	if(SecondCameraRecordPool.Num() > 0)
	{
		TArray<USecondCameraRecord*> ValidRecords;	//조건을 최종적으로 만족하는 Record 그룹

		for (auto TargetRecord : SecondCameraRecordPool)
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
					//지형 검사가 필요하지만, 바운더리를 벗어난 경우
					else { continue; }
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
			SG_LOG(Log, "Exception Case 1002...");
			return GetLastSecondCameraRecord();
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
				TargetRecord = ValidRecords[i];
				break;
			}
		}

		if(TargetRecord == nullptr)
		{
			SG_LOG(Log, "Exception Case 1001...");
			return GetLastSecondCameraRecord();
		}

		if(IsValid(TargetRecord))
		{
			SG_LOG(Log, "SJW 111 Selected SecondCameraRecord Index %i, Priority %i", TargetRecord->Index, TargetRecord->Priority);
		}
		
		return TargetRecord;
	}

	//다음 우선순위가 비어있는 경우(연속적이지 않은 경우)에 대한 예외처리
	if((SecondCameraRecordPool.Num() == 0) && (Priority < GetLastSecondCameraRecord()->Priority))
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
	if(nullptr == GameInst) return TargetArr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return TargetArr;

	TargetArr = GameInst->GetTableManager()->GetSecondCameraRecordsByPriority(ID);
	return TargetArr;
}

USecondCameraRecord* ASGCamera::GetLastSecondCameraRecord()
{
	if(nullptr == GameInst) return nullptr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return nullptr;

	return SecondCameraRecordPool.Last();
}

int32 ASGCamera::GetBeforeApexCamNumFromStringValue(FString TargetValue)
{
	TargetValue = TargetValue.ToLower();
	if(TargetValue == TEXT("shot_fixed")) {	return 1; }
	if(TargetValue == TEXT("shot_follow")) { return 2; }
	if(TargetValue == TEXT("sky_follow")) { return 3; }
	
	return 2;
}

int32 ASGCamera::GetAfterApexCamNumFromStringValue(FString TargetValue)
{
	TargetValue = TargetValue.ToLower();
	if(TargetValue == TEXT("side_fixed")){ return 2; }
	if(TargetValue == TEXT("land_fixed")){ return 1; }
	if(TargetValue == TEXT("shot_follow")){ return 3; }
	if(TargetValue == TEXT("sky_follow")){ return 6; }
	if(TargetValue == TEXT("shot_fixed")){ return 7; }

	return 3;
}

bool ASGCamera::IsAvailableThirdCamera(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	if(!IsValid(SelectedSecondCameraRecord)) return false;

	FString SecondCameraStr = SelectedSecondCameraRecord->Camera_Result.ToLower();
	
	int32 LandCameraActiveCond = SelectedSecondCameraRecord->Third_Camera_Active;
	
#ifdef ENABLE_DEBUG_CAMERA
	if(IsValid(PHYSICAL_MANAGER))
	{
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

					// SG_LOG(Log, "SJW 111 Predicted After Putting Location : %s", *predictPuttingPosition.ToString());
					
					float DistPredictedLocToHole = FVector::Dist2D(predictPuttingPosition, holecupLocation);
					
					// SG_LOG(Log, "SJW 111 DistPredictedLocToHole %f", DistPredictedLocToHole);
					
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
			// SG_LOG(Log, "GetTargetPuttingCameraRecordByPriority NextPriority Case b...");
			
			Priority += 1;
			return GetTargetPuttingCameraRecordByPriority(Priority, Player);
		}

		// if(IsValid(TargetRecord))
		// {
		// 	SG_LOG(Log, "SJW 111 Selected PuttingRecord Index %i, Priority %i", TargetRecord->Index, Priority);
		// }
		
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
	// SG_LOG(Log, "SJW 333 ActiveEndShotWaitCamera.......");

	//SkyTrace 카메라인 경우 ActiveEndShotWaitCamera 사용 x
	if(AfterApexCamNum == 6) return;
	
	EndShotWaitCameraNeed = true;

	if (nullptr == GameInst) return;
	if (nullptr == PlayerController) return;
	if (nullptr == PlayerChar) return;
	
	if(PlayerController->GetViewTarget() == EndShotWaitCamera) return;
	
	float BallToHoleDist = FVector::Dist2D(holecupLocation, PlayerChar->GetActorLocation());
	if(BallToHoleDist < 20) return;
	
	FVector EndShotWaitCameraLocation = PlayerController->PlayerCameraManager->GetCameraLocation();
	FRotator EndShotWaitCameraRotator = PlayerController->PlayerCameraManager->GetCameraRotation();

	float FOV = PlayerController->PlayerCameraManager->GetFOVAngle();
	EndShotWaitCamera->GetCamera()->FieldOfView = FOV;
	EndShotWaitCamera->GetCamera()->SetWorldLocation(EndShotWaitCameraLocation);
	EndShotWaitCamera->GetCamera()->SetWorldRotation(EndShotWaitCameraRotator);
	
	PlayerController->SetViewTarget(EndShotWaitCamera);
}

UPuttCameraRecord* ASGCamera::GetTargetPuttingCameraRecordByIndex(int32 Index)
{
	if(nullptr == GameInst) return nullptr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return nullptr;

	UPuttCameraRecord* TargetRecord = GameInst->GetTableManager()->GetPuttCameraRecordByID(Index);
	return TargetRecord;
}

USecondCameraRecord* ASGCamera::GetSecondCameraRecordByIndex(int32 ID)
{
	if(nullptr == GameInst) return nullptr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return nullptr;

	USecondCameraRecord* TargetRecord = CameraTable->m_SecondCameraSheet->GetRecord(ID);
	return TargetRecord;
}

TArray<UReadyCameraRecord*> ASGCamera::GetAllReadyCameraRecords()
{
	TArray<UReadyCameraRecord*> TargetArr;
	if(nullptr == GameInst) return TargetArr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return TargetArr;

	TargetArr = GameInst->GetTableManager()->GetAllReadyCameraRecords();
	return TargetArr;
}

UReadyCameraRecord* ASGCamera::GetTargetReadyCameraRecord(ASGPlayerCharacter* Player, bool mIsTeeShot)
{
	auto TargetRecords = GetAllReadyCameraRecords();
	
	if(TargetRecords.Num() > 0)
	{
		for (auto ItemRecord : TargetRecords)
		{
			float CurrentHoleCupDist = FVector::Dist2D(Player->GetActorLocation(), Player->CharacterComponent->GetPinPos());

			//1. 티샷 조건 검사
			{
				int32 TeeShot = ItemRecord->Tee_Shot;
				if(TeeShot != -1)
				{
					bool TeeShotCond = TeeShot == 1;
					if(mIsTeeShot != TeeShotCond) { continue; }
				}
			}

			//2. 퍼팅 여부 조건 검사
			{
				int32 Putt = ItemRecord->Putt;
				if(Putt != -1)
				{
					bool IsApronOrGreen = Player->IsOnApronORGreenWithStayMeterType();
					bool PuttCond = Putt == 1;
					if(IsApronOrGreen != PuttCond) { continue; }
				}
			}
			
			//3. 홀컵 거리 조건 검사
			{
				float HoleDistanceMin = ItemRecord->Hole_Start_Distance_Min;
				float HoleDistanceMax = ItemRecord->Hole_Start_Distance_Max;
			
				if(HoleDistanceMin != -1)
				{
					if(CurrentHoleCupDist < HoleDistanceMin) { continue; }
				}

				if(HoleDistanceMax != -1)
				{
					if(CurrentHoleCupDist > HoleDistanceMax) { continue; } 
				}
			}

			return ItemRecord;
		}
	}
	
	SG_LOG(Log, "GetTargetFirstCameraPriority Exception Case...");
	return nullptr;
}

UReadyCameraRecord* ASGCamera::GetLastReadyCameraRecord()
{
	if(nullptr == GameInst) return nullptr;

	USGShotCameraTable* CameraTable = GameInst->GetTableManager()->GetShotCameraTable();
	if(nullptr == CameraTable) return nullptr;

	return CameraTable->m_ReadyCameraSheet->arrayTable.Last();
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
	
	if(ballPower >= 4000)
	{
		TargetRatio = 0.8f;
	}

	float UpperLimit = ScreenSize.Y * (1 - TargetRatio);
	float BottomLimit = ScreenSize.Y * TargetRatio;

	bool IsInSafeArea = (CurrentScreenY > UpperLimit) && (CurrentScreenY < BottomLimit);

	UpperUnSafe = CurrentScreenY <= UpperLimit;
	BottomUnsafe = CurrentScreenY >= BottomLimit;
	
	return !IsInSafeArea;
}

bool ASGCamera::CheckSkyTraceVerticalUnSafeScreenArea(int32 CurrentScreenY, bool& UpperUnSafe, bool& BottomUnsafe)
{
	float TargetRatio = 0.2f;
	if(IsBallImpacted && IsPredictGreenLanding){ TargetRatio = 0.1f; }

	float UpperLimit = ScreenSize.Y * TargetRatio;
	float BottomLimit = ScreenSize.Y * (1 - TargetRatio);

	bool IsInSafeArea = (CurrentScreenY > UpperLimit) && (CurrentScreenY < BottomLimit);

	UpperUnSafe = CurrentScreenY <= UpperLimit;
	BottomUnsafe = CurrentScreenY >= BottomLimit;

	// SG_LOG(Log, "SJW 777 ScreenSize Y %f", ScreenSize.Y);
	// SG_LOG(Log, "SJW 777 CurrentScreenY %i, UpperLimit %f, BottomLimit %f", CurrentScreenY, UpperLimit, BottomLimit);
	
	return !IsInSafeArea;
}

bool ASGCamera::CheckSkyTraceHorizontalUnSafeScreenArea(int32 CurrentScreenX, bool& LeftUnSafe, bool& RightUnsafe)
{
	float RightLimit = ScreenSize.X * TargetHorizontalSafeAreaRatio;
	float LeftLimit = ScreenSize.X * (1 - TargetHorizontalSafeAreaRatio);

	bool IsInSafeArea = (CurrentScreenX < RightLimit) && (CurrentScreenX > LeftLimit);

	LeftUnSafe = CurrentScreenX <= LeftLimit;
	RightUnsafe = CurrentScreenX >= RightLimit;

	// if(IsInSafeArea == false)
	// {
	// 	SG_LOG(Log, "SJW 777 ScreenSize X %f", ScreenSize.X);
	// 	SG_LOG(Log, "SJW 777 CurrentScreenX %i, LeftLimit %f, RightLimit %f", CurrentScreenX, LeftLimit, RightLimit);
	// }
	return !IsInSafeArea;
}


bool ASGCamera::CheckSkyTraceOutOfScreen(APlayerController* Controller, FVector2d CurrentScreenPosition)
{
	int32 ScreenX, ScreenY;
	Controller->GetViewportSize(ScreenX, ScreenY);

	// SG_LOG(Log, "CheckSkyTraceOutOfScreen CurrentScreenPosition %s ScreenX %i ScreenY %i", *CurrentScreenPosition.ToString(), ScreenX, ScreenY);

	if (CurrentScreenPosition.X <= EPSILON || CurrentScreenPosition.X > ScreenX || CurrentScreenPosition.Y <= EPSILON || CurrentScreenPosition.Y > ScreenY)
	{
		return true;  // 화면 밖
	}
	
	return false;		//화면 안
}



void ASGCamera::ChangeCameraShiftSettings(float TargetShiftRotateAngle) 
{
	//비율로 ShiftLeftAmount 값을 찾는다. [15도일 때 100]
	ShiftLeftAmount = 15.0f / (TargetShiftRotateAngle * 100.0f);

	GameInst = Cast<USGGameInstance>(GetWorld()->GetGameInstance());
	if (nullptr == GameInst) return;

	PlayerController = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
	if (nullptr == PlayerController) return;

	PlayerChar = Cast<ASGPlayerCharacter>(PlayerController->GetCharacter());
	if (nullptr == PlayerChar) return;

	bool IsCourseMode = IsValid(CourseMode);
	
	ShiftRotateAngle = TargetShiftRotateAngle;
	PlayerChar->FollowCameraFixed();
	PlayerChar->UpdateCameraFieldOfView();
	
	if(ShiftRotateAngle == 0)
	{
		TraceCamera->GetSpringArm()->SetRelativeRotation(FRotator::ZeroRotator);
	}
	else
	{
		float OffSetAngle = 0.0f;
		
		if(IsCourseMode)
		{
			bool bIsTeeShot = SHOT_DATA_MANAGER->GetIsTeeShot(PlayerChar->GetUniqID());
			bool IsApronOrGreen = PlayerChar->IsOnApronORGreenWithStayMeterType();
			
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

float ASGCamera::EaseIn(float t)
{
	return t * t;
}

void ASGCamera::InitSkyTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player)
{
	if(IsInitTraceCamera) return;
	IsInitTraceCamera = true;
	
	FVector BallLocation = Player->GetActorLocation();

	FRotator SpringArmTargetRotation = Player->GetCameraBoom()->GetTargetRotation();
	SpringArmTargetRotation.Yaw += ShiftRotateAngle;
	// FVector FollowCameraSocketOffset = Player->GetCameraBoom()->SocketOffset;
	SkyTraceCamera->GetCamera()->FieldOfView = Player->GetFollowCamera()->FieldOfView;
	
	// float OffSetShiftRotateAngle = 0.0f;
	// if(!IsTeeShot){ OffSetShiftRotateAngle = ShiftSecondShotOffSetAngle; }
	
	// FVector CamToBallDirVector = Player->GetActorLocation() - Player->GetCamera()->GetComponentLocation();
	// FVector ShiftedCamToBallDirVector = CamToBallDirVector.RotateAngleAxis(ShiftRotateAngle, FVector::ZAxisVector);
	// ShiftedCamToBallDirVector.Z = 0;
	// FRotator TargetRot = FRotationMatrix::MakeFromX(ShiftedCamToBallDirVector).Rotator();
	
	float BackwardLength = 300;
	
	FVector FollowCameraLoc = ((BallLocation - Player->GetActorForwardVector() * BackwardLength));
	GroundCheck(FollowCameraLoc, FollowCameraLoc);
	
	SkyTraceCamera->GetCamera()->FieldOfView = Player->GetFollowCamera()->FieldOfView + 10;
	
	SkyTraceCamera->GetCamera()->SetUsingAbsoluteLocation(true);
	SkyTraceCamera->GetCamera()->SetWorldLocation(FollowCameraLoc);
	
	SkyTraceCamera->GetCamera()->SetUsingAbsoluteRotation(true);
	SkyTraceCamera->GetCamera()->SetWorldRotation(SpringArmTargetRotation);

	ASGBaseGameMode* const GameMode = GetWorld()->GetAuthGameMode<ASGBaseGameMode>();
	CameraRailActor = GameMode->GetCameraRail();
	if(IsValid(CameraRailActor))
	{
		CameraRailActor->UpdateCameraDirector(this);
		CameraRailActor->UpdatePlayer(Player);
		CameraRailActor->UpdateImpactedVector(BallForwardVectorAfterImpact);
		CameraRailActor->UpdateWindVector(WindVector);

		USGUserData* const playerData = DATA_CENTER->FindUserData(Player->GetUniqID());
		bool IsRightShift = playerData->HandIndex == 1;
		CameraRailActor->UpdatePlateInfo(IsRightShift);
		
		CameraRailActor->BeginTrace();
	}

	Controller->SetViewTarget(SkyTraceCamera);
}

void ASGCamera::SkyTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime)
{
	if(flyingTime < 0.3f) return;
	if(CameraRailActor == nullptr) return;

	// SG_LOG(Log, "SkyTraceCameraWork DeltaTime : %f", DeltaTime);

	bool UpperUnSafe = false;
	bool BottomUnsafe = false;

	FVector2D ViewportSize;
	GetWorld()->GetGameViewport()->GetViewportSize(ViewportSize);
	
	FVector2D BallScreenPosition;
	Controller->ProjectWorldLocationToScreen(Player->GetActorLocation(), BallScreenPosition);
	CheckSkyTraceVerticalUnSafeScreenArea(FMath::Abs(BallScreenPosition.Y), UpperUnSafe, BottomUnsafe);

	FVector CurrentCameraLocation = SkyTraceCamera->GetCamera()->GetComponentLocation();
	FVector CameraToBallVector = Player->GetActorLocation() - CurrentCameraLocation;
	
	FVector CamForwardVector2d = SkyTraceCamera->GetCamera()->GetForwardVector().GetSafeNormal();
	float CameraToBallDist = FVector::Dist(Player->GetActorLocation(), CurrentCameraLocation);
	float DotRes = FMath::Abs(FVector::DotProduct(CameraToBallVector.GetSafeNormal(), CamForwardVector2d));
	float AngleInRadians = FMath::Acos(DotRes);
	float AngleInDegrees = FMath::RadiansToDegrees(AngleInRadians);
	float TargetDist = CameraToBallDist * FMath::Cos(AngleInRadians);
	
#pragma region CanTraceChange
	if(!IsBallImpacted)
	{
		CanTraceCamera = true;
	}
	else
	{
		// CamForwardVector2d.Z = 0;
		// CameraToBallVector.Z = 0;
		float CurrentHalfOfFieldOfView = 40.0f;			//안전하게 시야각 80도 기준으로 한다.
		
		// SG_LOG(Log, "SJW [000] DotRes %f", DotRes);
		// SG_LOG(Log, "SJW [000] SkyTraceStopDist %f", SkyTraceStopDist);
		// SG_LOG(Log, "SJW [000] SkyTraceLowApexStopDist %f", SkyTraceLowApexStopDist);
		// SG_LOG(Log, "SJW [000] TargetDist %f", TargetDist);
		// SG_LOG(Log, "SJW [000] AngleInDegrees %f", AngleInDegrees);

		float WaitImpactedAfterTime = IsPredictGreenLanding ? 1.0f : 0.5f;
		if(ImpactedTime >= WaitImpactedAfterTime)
		{
			if(IsLowApex) {	CanTraceCamera = !IsTraceCameraPrevAlreadyStopped && Player->GetBallMoving() && (TargetDist > SkyTraceLowApexStopDist) && (AngleInDegrees < CurrentHalfOfFieldOfView) && (DotRes > 0.4f); }
			else { CanTraceCamera = !IsTraceCameraPrevAlreadyStopped && Player->GetBallMoving() && (TargetDist > SkyTraceStopDist) && (AngleInDegrees < CurrentHalfOfFieldOfView) && (DotRes > 0.4f); }	
		}
	}

	if(IsBallImpacted && !CanTraceCamera)
	{
		IsTraceCameraPrevAlreadyStopped = true;
	}
#pragma endregion
	
#pragma region DrawLandingLine
	if(CanAddLandingPathLine)
	{
		if(IsLanding)
		{
			if(!IsAlreadyPauseTrace)
			{
				CameraRailActor->PauseTrace();
				IsAlreadyPauseTrace = true;
			}
	
			if(!IsOBZPos(predictLandingPositionForSkyTrace))
			{
				if(IsUnder100Shot)
				{
					SkyTraceDrawLandingLineWithFollowCamera(Controller, Player, 200, FVector(0, 0, 100));
				}
				else
				{
					if(IsLowApex)
					{
						SkyTraceDrawLandingLineWithFollowCamera(Controller, Player, 300, FVector(0, 0, 100));
					}
					else
					{
						if(LandingTime <= LandingWaitTime)
						{
							SkyTraceCameraDownVectorAfterLandingPassTime += DeltaTime;
							float TargetAlpha = SkyTraceCameraDownVectorAfterLandingPassTime / LandingWaitTime;
							if(TargetAlpha > LandingWaitTime) TargetAlpha = LandingWaitTime;
							
							float Result = FMath::Lerp(0, -100, TargetAlpha);
							SkyTraceDrawLandingLineWithFollowCamera(Controller, Player, 100, FVector(0, 0, Result));
						}
						else
						{
							if(!IsAlreadyAddBezierLandingLine)
							{
								SkyTraceDrawLandingLineWithBezierCurve(Controller, Player, DeltaTime);
							}
					
							if(IsBallImpacted)
							{
								SkyTraceDrawLandingLineWithFollowCamera(Controller, Player, 300, FVector(0, 0, 100));
							}
						}
					}
				}
			}
			else
			{
				SkyTraceDrawLandingLineWithFollowCamera(Controller, Player, 300, FVector(0, 0, 100));
			}
		}
	}
#pragma endregion
	
#pragma region SetCameraFollowSpeed
	if(IsLowApex)
	{
		if(SkyTraceAscendVelocityRatio > 1.0f) SkyTraceAscendVelocityRatio = 1.0f;
		if(SkyTraceDescendVelocityRatio > 1.0f) SkyTraceDescendVelocityRatio = 1.0f;
	}
#pragma endregion
	
#pragma region CalcDistAlongSpline
	if(CanTraceCamera && !IsTraceCameraPrevAlreadyStopped)
	{
		if(!IsBallImpacted)
		{
			//상승중인 경우
			if(!IsLanding)
			{
				DistanceAlongSpline += Player->GetVelocity().Size() * DeltaTime * SkyTraceAscendVelocityRatio;
				
				// SG_LOG(Log, "SkyTraceCameraWork [000] Velocity Case IsRising %f", Player->GetVelocity().Size());	
			}
			//하강중인 경우
			else
			{
				DistanceAlongSpline += Player->GetVelocity().Size() * DeltaTime * SkyTraceDescendVelocityRatio;
				
				// SG_LOG(Log, "SkyTraceCameraWork [000] Velocity Case IsLanding %f", Player->GetVelocity().Size());
			}
			// SG_LOG(Log, "SkyTraceCameraWork [000] Velocity 111 %f", Player->GetVelocity().Size());
		}
		else
		{
			float Velocity = Player->GetVelocity().Size();
			// if(Velocity < 400) Velocity = 400;
			DistanceAlongSpline += Velocity * DeltaTime * SkyTraceAscendVelocityRatio;
		}
	}
#pragma endregion
	
	FVector NewLocation = CameraRailActor->GetLocationAtDistanceAlongSpline(DistanceAlongSpline, ESplineCoordinateSpace::World);
	
	if(CanTraceCamera)
	{
		//이전에 한번 거리 제한으로 멈춘 경우
		if(IsTraceCameraPrevAlreadyStopped)
		{
			// FVector PrevCameraLocation = SkyTraceCamera->GetCamera()->GetComponentLocation();
			// FVector TargetLerpLocation = FMath::Lerp(PrevCameraLocation, NewLocation, 0.01f);
			//
			// SkyTraceCamera->GetCamera()->SetWorldLocation(TargetLerpLocation);
		}
		else
		{
			FVector PrevCameraLocation = SkyTraceCamera->GetCamera()->GetComponentLocation();
			FVector TargetLerpLocation = FMath::Lerp(PrevCameraLocation, NewLocation, 0.5f);
			SkyTraceCamera->GetCamera()->SetWorldLocation(TargetLerpLocation);
		}
	}

	FVector BallLocation = Player->GetActorLocation();
	FVector NewLocationToBallLocation = BallLocation -  SkyTraceCamera->GetCamera()->GetComponentLocation();
	
	float OffSetRotateAngle = 0.0f;
	if(!IsTeeShot) { OffSetRotateAngle = ShiftSecondShotOffSetAngle; }
	
	FRotator CurrentRotation = SkyTraceCamera->GetCamera()->GetComponentRotation();
	FVector ShiftedCamToBallDirVector = NewLocationToBallLocation.RotateAngleAxis(ShiftRotateAngle + OffSetRotateAngle, FVector::ZAxisVector);
	FRotator TargetRotation = FRotationMatrix::MakeFromX(ShiftedCamToBallDirVector).Rotator();
	FRotator ResultRot = FMath::Lerp(CurrentRotation, TargetRotation, 0.02f);
	
	if(IsUnder100Shot)
	{
		if(!IsBallImpacted && BottomUnsafe)
		{
			FVector2D TargetScreenPosition = FVector2D(ViewportSize.X * 0.5f, ViewportSize.Y * 0.8f);
			FVector2D Delta = TargetScreenPosition - BallScreenPosition;
		
			float RotationSpeed = 1.0f; // 카메라 회전 속도 조절
		
			if (Delta.Size() > KINDA_SMALL_NUMBER)
			{
				FRotator NewRotation = SkyTraceCamera->GetCamera()->GetComponentRotation();
				NewRotation.Pitch += Delta.Y * RotationSpeed * DeltaTime;
		
				SkyTraceCamera->GetCamera()->SetWorldRotation(NewRotation);
			}
		}
	}
	else
	{
		if(!IsBallImpacted && (UpperUnSafe || BottomUnsafe))
		{
			FVector2D TargetScreenPosition = UpperUnSafe ?
				FVector2D(ViewportSize.X * 0.5f, ViewportSize.Y * 0.2f) :
				FVector2D(ViewportSize.X * 0.5f, ViewportSize.Y * 0.8f);
			
			FVector2D Delta = TargetScreenPosition - BallScreenPosition;
			
			float RotationSpeed = 1.0f; // 카메라 회전 속도 조절

			FRotator PrevRotator = SkyTraceCamera->GetCamera()->GetComponentRotation();
			FRotator TargetRotator = PrevRotator;
			TargetRotator.Pitch += Delta.Y * RotationSpeed * DeltaTime;
			
			FRotator TargetLerpedRot = FMath::Lerp(PrevRotator, TargetRotator, 0.5f);
			SkyTraceCamera->GetCamera()->SetWorldRotation(TargetLerpedRot);
		}
	}

	if(IsBallImpacted || IsBadImpacted)
	{
		bool IsTooClose = TargetDist < 300;
		if(IsTooClose) IsTooCloseCalled = true;
		FVector CameraForwardDir = SkyTraceCamera->GetCamera()->GetForwardVector();
		CameraForwardDir.Z = 0;
		FVector BallForwardDir = Player->GetVelocity();
		BallForwardDir.Z = 0;

		if(!IsChangedSafeAreaRatioAfterImpacted)
		{
			TargetHorizontalSafeAreaRatio = IsOBZPos(predictLandingPositionForSkyTrace) ? 0.6f : 0.9f;
			IsChangedSafeAreaRatioAfterImpacted = true;
		}
		
		if(IsTooCloseCalled)
		{
			FVector PrevCameraLocation = SkyTraceCamera->GetCamera()->GetComponentLocation();
			FVector TargetLocation = BallLocation - BallForwardVectorAfterImpact * 500;
			float DistCamToBall = FVector::Dist2D(PrevCameraLocation, BallLocation);
			
			TargetLocation.Z = PrevCameraLocation.Z;
			GroundCheck(TargetLocation, TargetLocation);
			
			FVector LerpedTargetLocation = FMath::Lerp(PrevCameraLocation, TargetLocation, 0.5f);
			SkyTraceCamera->GetCamera()->SetWorldLocation(LerpedTargetLocation);

			SG_LOG(Log, "SJW [BackStepTest] DistCamToBall %f", DistCamToBall);
			SG_LOG(Log, "SJW [BackStepTest] 1001.......");
		}
		else
		{
			if(BottomUnsafe)
			{
				FVector2D TargetScreenPosition = FVector2D(BallScreenPosition.X, ViewportSize.Y * 0.8f);
				FVector2D Delta = TargetScreenPosition - BallScreenPosition;
		
				float RotationSpeed = 1.0f; // 카메라 회전 속도 조절
		
				if (Delta.Size() > KINDA_SMALL_NUMBER)
				{
					FRotator NewRotation = SkyTraceCamera->GetCamera()->GetComponentRotation();
					NewRotation.Pitch += Delta.Y * RotationSpeed * DeltaTime;
		
					SkyTraceCamera->GetCamera()->SetWorldRotation(NewRotation);
				}
			}
		}
	}

	bool LeftUnSafe = false;
	bool RightUnSafe = false;
	CheckSkyTraceHorizontalUnSafeScreenArea(BallScreenPosition.X, LeftUnSafe, RightUnSafe);
	
	if(LeftUnSafe || RightUnSafe)
	{
		FRotator PrevRotator = SkyTraceCamera->GetCamera()->GetComponentRotation();
		FRotator TargetRotator = PrevRotator;
		
		float RotationSpeed = 1; // 카메라 회전 속도 조절
		
		if (LeftUnSafe) {
			FVector2D TargetScreenPosition = FVector2D(ViewportSize.X * (1 - TargetHorizontalSafeAreaRatio), BallScreenPosition.Y);
			FVector2D Delta = TargetScreenPosition - BallScreenPosition;
			TargetRotator.Yaw -= Delta.X * RotationSpeed * DeltaTime;
		}
		else
		{
			FVector2D TargetScreenPosition = FVector2D(ViewportSize.X * TargetHorizontalSafeAreaRatio, BallScreenPosition.Y);
			FVector2D Delta = TargetScreenPosition - BallScreenPosition;
			TargetRotator.Yaw -= Delta.X * RotationSpeed * DeltaTime;
		}

		FRotator TargetLerpedRot = FMath::Lerp(PrevRotator, TargetRotator, 0.5f);
		SkyTraceCamera->GetCamera()->SetWorldRotation(TargetLerpedRot);
	}
}

void ASGCamera::GetGroundFromPosition(FVector StartLoc, FVector Dir, FVector& ResultLoc)
{
	FHitResult HitResult;
	FVector Start = StartLoc;
	FVector End = StartLoc + Dir * 100000;
	
	TArray<TEnumAsByte<EObjectTypeQuery>> ObjectTypes;
	ObjectTypes.Add(UEngineTypes::ConvertToObjectType(ECC_GameTraceChannel1));
	TArray<AActor*> IgnoreActors;
	
	// SG_LOG(Log, "GetGroundFromPosition.........StartLoc %s", *StartLoc.ToString());
	
	bool bIsGround = UKismetSystemLibrary::LineTraceSingleForObjects(GetWorld(), Start, End, ObjectTypes, false, IgnoreActors,  EDrawDebugTrace::None, HitResult, true);
	if(bIsGround)
	{
		ResultLoc = HitResult.ImpactPoint;
	}
}

void ASGCamera::SkyTraceDrawLandingLineWithBezierCurve(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime)
{
	// SG_LOG(Log, "SkyTraceDrawLandingLineWithBezierCurve called....predictLandingPositionForSkyTrace %s", *predictLandingPositionForSkyTrace.ToString());
	IsAlreadyAddBezierLandingLine = true;

	FVector TargetOffSetHeight = IsPredictGreenLanding ? FVector(0, 0, GreenTrailLimitHeight) : FVector(0, 0, 500);
	FVector EndTracePoint = predictLandingPositionForSkyTrace + TargetOffSetHeight;
	// SG_LOG(Log, "SkyTraceDrawLandingLineWithBezierCurve called....EndTracePoint %s", *EndTracePoint.ToString());
	CameraRailActor->AddBezier4PointTraceToEndPoint(EndTracePoint);
}

void ASGCamera::SkyTraceDrawLandingLineWithFollowCamera(APlayerController* Controller, ASGPlayerCharacter* Player, float BackwardAmount, FVector UpVector)
{
	// SG_LOG(Log, "SkyTraceDrawLandingLineWithFollowCamera called....");
	FVector BallLocation = Player->GetActorLocation();
	
	if(!IsBallImpacted)
	{
		FVector LastTracePoint = CameraRailActor->GetLastPoint();
		FVector BackwardVector = (BallLocation - LastTracePoint).GetSafeNormal();
		BackwardVector.Z = 0;
		
		FVector TargetLocation = BallLocation - BackwardVector * BackwardAmount;

		if(IsValid(CameraRailActor))
		{
			USGUserData* const playerData = DATA_CENTER->FindUserData(Player->GetUniqID());
			bool IsRightShift = playerData->HandIndex == 1;

			float TargetRotateAngle = IsRightShift ? -90.0f : 90.0f; 
			TargetLocation += BackwardVector.RotateAngleAxis(TargetRotateAngle, FVector::ZAxisVector).GetSafeNormal() * CameraRailActor->GetLeftVectorAmount();
		}
		
		// SG_LOG(Log, "TargetLocation %s", *TargetLocation.ToString());

		CameraRailActor->AddTracePoint(TargetLocation, true);
	}
	else
	{
		//공이 닿은 이후부터는 현재 카메라 위치 (살짝 앞쪽) 부터 공의 방향으로 Rail을 새로 그린다.
		if(!IsRemoveCamPointToEndPoint)
		{
			FVector CameraLocation = SkyTraceCamera->GetCamera()->GetComponentLocation();
			FVector DirCamToBall = (BallLocation - CameraLocation).GetSafeNormal();
			CameraRailActor->ReFormCamPointToEndPoint(CameraLocation, DirCamToBall);
			IsRemoveCamPointToEndPoint = true;
		}
		
		FVector TargetLocation = BallLocation;
		
		CameraRailActor->AddTracePointAfterImpacted(TargetLocation, true);
	}
}

void ASGCamera::SkyTraceDrawLandingLineApexLocation(ASGPlayerCharacter* Player) { }

void ASGCamera::SetFoliageOpacity()
{
	if(!IsFoliageTimerInitialized) return;
	if (nullptr == GameInst) return;
	if (nullptr == PlayerController) return;
	if (nullptr == PlayerChar) return;
	
	FVector CameraLocation = PlayerController->PlayerCameraManager->GetCameraLocation();
	
	TArray<UActorComponent*> InstancedStaticMeshCompArray;
	FVector BallLocation = PlayerChar->GetActorLocation();
	
	for (TActorIterator<AInstancedFoliageActor> Iter(GetWorld()); Iter; ++Iter)
	{
		AInstancedFoliageActor* FoliageMesh = *Iter;
		if (nullptr == FoliageMesh) continue;

		auto FoliageComponents = FoliageMesh->K2_GetComponentsByClass(UFoliageInstancedStaticMeshComponent::StaticClass());

		for (auto& Comp : FoliageComponents)
		{
			auto FoliageComponent = Cast<UFoliageInstancedStaticMeshComponent>(Comp);
			
			if (FoliageComponent != nullptr)
			{
				UStaticMesh* Mesh = FoliageComponent->GetStaticMesh();

				auto InstanceCount = FoliageComponent->GetInstanceCount();
				
				for (int i = 0; i < InstanceCount; ++i)
				{
					// FTransform InstanceTransform;
					//
					// FoliageComponent->GetInstanceTransform(i, InstanceTransform, true);
					//
					// // UE_LOG( LogTemp, Display, TEXT("%s[%d] %s"), *Mesh->GetName(), i, *InstanceTransform.ToString() );
					//
					// //카메라 반경 10m 밖에 있는 FoliageActor는 필터링
					// float DistToCam = FVector::Distance(CameraLocation, InstanceTransform.GetLocation());
					// if(DistToCam > 1000) {	continue; }
					//
					// // SG_LOG(Log, "DistToCam %f FoliageLocation %s", DistToCam, *InstanceTransform.GetLocation().ToString());
					//
					// //카메라가 바라보고 있고, 공~카메라 사이에 존재하는 경우 반투명 처리
					// FVector CamToFoliageDir = (InstanceTransform.GetLocation() - CameraLocation).GetSafeNormal();
					// FVector CamToBallDir = (BallLocation - CameraLocation).GetSafeNormal();
					// float DotRes = FVector::DotProduct(CamToFoliageDir, CamToBallDir);
					// if(DotRes < 0)
					// {
					// 	SG_LOG(Log, "InstanceTransform DotRes Issue...");
					// 	continue;
					// }
					//
					// float DistCamToFoliage = FVector::Distance(InstanceTransform.GetLocation(), CameraLocation);
					// float DistCamToBall = FVector::Distance(BallLocation, CameraLocation);
					// if(DistCamToFoliage > DistCamToBall)
					// {
					// 	SG_LOG(Log, "DistCamToFoliage is Bigger than DistCamToBall...");
					// 	continue;
					// }

					FString MeshName = Mesh->GetName();

					// SG_LOG(Log, "Mesh Name : %s, Location : %s", *MeshName, *InstanceTransform.GetLocation().ToString());

					UMaterialInterface* Material = FoliageComponent->GetMaterial(0);
					if (Material)
					{
						// Material을 변경하거나 수정하는 코드 작성
						UMaterialInstanceDynamic* DynamicMaterial = UMaterialInstanceDynamic::Create(Material, this);
						DynamicMaterial->SetScalarParameterValue(FName("Opacity"), 0.0f);
						FoliageComponent->SetMaterial(0, DynamicMaterial);
					}
				}
			}
		}
	}
}
