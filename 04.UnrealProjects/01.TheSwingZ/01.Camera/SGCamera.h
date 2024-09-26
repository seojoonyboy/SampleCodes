#pragma once

#include "CoreMinimal.h"
#include "SGCommon.h"
#include "SGGameInstance.h"
#include "GameFramework/Actor.h"
#include "SGIPFinder.h"
#include "SGShotCameraTable.h"
#include "SGCamera.generated.h"

class ASGCameraRailActor;
class ASGPlayerCharacter;

UCLASS()
class SG_API ASGCamera : public AActor
{
	GENERATED_BODY()
	
public:	
	ASGCamera();

protected:
	void BeginPlay() override;
	void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
	void Tick(float DeltaTime) override;

public:
	void ImpactCameraSetting(ERenderType::Type RenderType, bool IsTeeShot);	// Shot 이벤트
	
	void SetCurFollowCam();
	void SetStartPlayerRot(FRotator rot) { StartPlayerRot = rot; }
	void EndShot(); // 이펙트가 꺼지면 플레이어 카메라로 전환

	bool CanTraceCamera = true;
	bool CanRotateTraceCamera = true;
	bool CanGreenCameraLookHoleCup = true;
	bool EndShotWaitCameraNeed = false;		//샷 이후 다음 플레이어 준비 전까지 대기하는 카메라가 필요한가?
	void ActiveEndShotWaitCamera();
	
	bool HoleIn = true;

	void ChangeCameraShiftSettings(float TargetShiftRotateAngle = 0);

	TArray<UReadyCameraRecord*> GetAllReadyCameraRecords();
	UReadyCameraRecord* GetLastReadyCameraRecord();
	UReadyCameraRecord* GetTargetReadyCameraRecord(ASGPlayerCharacter* Player, bool mIsTeeShot);

	const float MAX_TRACE_CAMERA_DISTANCE = 1000;
	
	bool GetIsLanding() { return IsLanding; };
	bool GetIsBallImpacted() { return IsBallImpacted; }
	float GetGroundCheckUpperAmount() { return GroundCheckUpperAmount; }
	FVector GetApexPosition() { return apexPosition; }
	float GetFlyingTime() { return flyingTime; }
private:
	void SpawnCamera();	// 카메라 초기화

	UFirstCameraRecord* GetFirstCameraRecordByIndex(int32 ID);
	TArray<UFirstCameraRecord*> GetFirstCameraRecordsByPriority(int32 ID);
	UFirstCameraRecord* GetTargetFirstCameraPriority(int32 Priority, ASGPlayerCharacter* Player);

	TArray<USecondCameraRecord*> GetSecondCameraRecordsByPriority(int32 ID);
	TArray<USecondCameraRecord*> GetSecondCameraRecordsByConditions(FString Name, float BallPower, float LaunchDegree);
	USecondCameraRecord* GetSecondCameraRecordByIndex(int32 ID);
	USecondCameraRecord* GetLastSecondCameraRecord();
	USecondCameraRecord* GetTargetSecondCameraPriority(int32 Priority, ASGPlayerCharacter* Player);

	TArray<UPuttCameraRecord*> GetPuttingCameraRecordsByPriority(int32 ID);
	UPuttCameraRecord* GetLastPuttingCameraRecord();
	UPuttCameraRecord* GetTargetPuttingCameraRecordByPriority(int32 Priority, ASGPlayerCharacter* Player);
	UPuttCameraRecord* GetTargetPuttingCameraRecordByIndex(int32 Index);
	
	int32 GetBeforeApexCamNumFromStringValue(FString TargetValue);
	int32 GetAfterApexCamNumFromStringValue(FString TargetValue);
	int32 GetPuttingCamNumFromStringValue(FString TargetValue);
	
	void TickCameraWork(float DeltaTime);

	void DecideAfterCamNum();

	void NewImpactCameraSettings(ERenderType::Type RenderType);

	void InitBadPlaceHitCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitFixedCameraWithNoRotateWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitFixedCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitReverseFixedCameraWork(APlayerController* Controller, FVector BallLocation);
	void InitSideCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitSideBellowCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitReverseTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitSkyTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	
	void FixedCameraWithNoRotateWork(ASGPlayerCharacter* Player);
	void FixedCameraWork(ASGPlayerCharacter* Player);
	
	void TraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);
	void DrivingModeTraceCameraSubWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);
	void CourseModeTraceCameraSubWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);
	void SkyTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);

	void SkyTraceDrawLandingLineWithBezierCurve(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);
	void SkyTraceDrawLandingLineWithFollowCamera(APlayerController* Controller, ASGPlayerCharacter* Player, float BackwardAmount, FVector UpVector);
	void SkyTraceDrawAfterImpactLine(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);
	void SkyTraceDrawLandingLineApexLocation(ASGPlayerCharacter* Player);
	
	void PuttingZoomCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);
	void InitPuttingTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void PuttingTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);
	void InitPuttingFixedCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void PuttingFixedCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);
	
	void ReverseFixedCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void SideCameraWork(ASGPlayerCharacter* Player);
	void SideBellowCameraWork(ASGPlayerCharacter* Player);
	void BadPlaceHitCameraWork(ASGPlayerCharacter* Player);
	void ReverseTraceCameraWork();
	
	void RequestClosestDistDecalScaleChange(float Percentage, float MinPercentage, FVector FromLocation = FVector::ZeroVector);
	bool CanChangeDecalScaleHeight(FVector FromLocation);
	
	void LandingCheck(float DeltaTime);
	void InitCameraRelativeRotation();
	void InitCameraLag();
	bool CheckCurrentHole(FVector InStart, FVector InEnd, FHitResult& OutHitResult, EStimpMeterType::Type& OutSurfaceType);

	bool IsCloseToGround(ASGPlayerCharacter* Player, float TargetHeight);

	void GroundCheck(FVector CameraLoc, FVector& ResultLoc);			//현재 위치 기준 좌우 OB 지점 찾기
	FVector GetVerticalGroundPos(FVector From);
	
	void GetLeftRightFairWayPos(FVector MoveDir, FVector CurrentLocation, FVector &LeftLoc, FVector &RightLoc);		//사이드 카메라 위치 기준 좌우 Fairway 지점 찾기
	bool IsFineConditionToSideCamera(float SideSpinRate, float SideAngleInDegree);
	
	void GetFairWayPosFromOBToApex(FVector From, FVector To, FVector &ResultLoc);

	bool IsOBZPos(FVector CurrentLocation);
	bool IsOnGreen(FVector Pos);

	void TraceCamZPosFix(FVector& CameraLoc);											//정방향 추적 카메라 지면 아래 검사
	void TraceCamZPosFix(FVector& SpringArmLoc, FVector& CameraLoc);					//정방향 추적 카메라 지면 아래 검사 [테스트 버전]
	void StartFixedCamZPosFix(FVector& CameraLoc);										//지면 고정 카메라 지면 아래 검사
	void PuttingTraceCamZPosFix(FVector& CameraLoc);									//퍼팅 추적 카메라 지면 아래 검사
	
	bool IsSafeSurface(EPhysicalSurface Surface);
	
	void PredictLandingPosition(FVector BallLocation, FVector BallVelocity, FVector AngularVelocity, FVector BowlerFreeViewLastPosition, FVector TargetWindForceVector, int MagnusForceCount);
	void PredictApexPosition(FVector BallLocation, FVector BallVelocity, FVector MagnusVector);
	void GetGroundFromPosition(FVector StartLoc, FVector Dir, FVector& ResultLoc);
	
	bool IsPredictWrongLandingZone();
	bool GetIsUnder100(ASGPlayerCharacter* Player, FVector HoleCupLoc);

	bool IsAvailableThirdCamera(APlayerController* Controller, ASGPlayerCharacter* Player);
	bool CheckUnSafeScreenArea(int32 CurrentScreenY, bool& UpperUnSafe, bool& BottomUnsafe);
	bool CheckSkyTraceVerticalUnSafeScreenArea(int32 CurrentScreenY, bool& UpperUnSafe, bool& BottomUnsafe);
	bool CheckSkyTraceHorizontalUnSafeScreenArea(int32 CurrentScreenX, bool& LeftUnSafe, bool& RightUnsafe);
	bool CheckSkyTraceOutOfScreen(APlayerController* Controller, FVector2d CurrentScreenPosition);
	
	float EaseIn(float t);

	void SetFoliageOpacity();
	
private:
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 BeforeApexCamNum = 0; // 1 : 시작 고정 카메라, 2 : 추적 카메라 (정방향), 3 : 신규 추적 카메라 (정방향) SkyTrace
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 AfterApexCamNum = 0; // 1 : 지면 고정 카메라, 2 : 사이드 카메라, 3 : 추적 카메라 (정방향), 5 : BadPlace 부딪힘 카메라, 6 : 신규 추적 카메라 (정방향) SkyTrace, 7 : 시작 고정 카메라
	
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 PuttingCamNum = 1; // 1 : 퍼팅 추적 카메라 (정방향), 2 : 퍼팅 고정 카메라 (역방향)
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 DebugAfterApexCamNum = -1; // -1 : 강제 ApexCam 사용하지 않음. 그 외에 값으로 강제 세팅

	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float TraceCameraTargetArmLength = 300; // 추적 카메라 지정 Arm Length
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceStopDist = 700; //Sky 추적 카메라 정지 거리
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceLowApexStopDist = 700; //Sky LowApex인 경우 추적 카메라 정지 거리
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceAscendVelocityRatio = 1.0f; //상승 속도
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceDescendVelocityRatio = 1.0f; //하강 속도
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceBezierCurveLength = 200.0f;  //하강 경로 베지어 커브 폭
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceLeftVectorSpeed = 1.0f;	  //Sky 추적 카메라 상승시 좌측으로 경로를 얼마나 이동시킬지
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float WindVectorLandingPosOffset = 500;		//예상 낙구 지점 맞바람인 경우 보간 값 [얼마나 예상 지점을 앞으로 당길 것인가?]
	
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float GroundCheckUpperAmount = 100; //지면 아래 보정 수치 값 [얼마나 위로 올린 값을 줄 것인가?]
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float BackTraceHeightRate = 0.66f; // 추적 카메라 높이 비율

	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideCameraLocationZRatio = 1.0; // 사이드 카메라 Z 위치가 APEX의 몇% 지점에 위치할 것인지. [1에 가까울수록 예상 낙구 지점]
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideCameraLocationXYRatio = 0.5; // 사이드 카메라 X 위치가 APEX의 몇% 지점에 위치할 것인지. [1에 가까울수록 예상 낙구 지점]
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideCameraDist = 1000; // 사이드 카메라가 얼마나 공과 옆으로 떨어져 있을지

	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideBellowCameraLocationXYRatio = 0.5; // 45도 측면 사이드 카메라 X 위치가 APEX의 몇% 지점에 위치할 것인지. [1에 가까울수록 예상 낙구 지점]
	
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float ReverseFixedCameraHeight = 300; // 지면 (역방향) 고정 카메라 지면으로부터 높이
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float ReverseFixedCameraSideDist = 1000; // 지면 (역방향) 고정 카메라와 낙구 예측 지점 사이의 거리
		//퍼팅 줌인 속도
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float PuttingCameraZoomOutSpeed = 0;		//퍼팅 줌아웃 속도
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float PuttingCameraZoomInMin = 30;		//퍼팅 줌인 최대값

	UPROPERTY(EditAnywhere, Category = "SG Cam") TSoftObjectPtr<class UMaterialParameterCollection> OcclusionMPC;
	UPROPERTY(Transient) TSoftObjectPtr<class UMaterialParameterCollectionInstance> OcclusionMPCInstance;
private:
	FVector holecupLocation = FVector::ZeroVector;
	
	UPROPERTY() class ASGCameraMode* TraceCamera					= nullptr;
	UPROPERTY() class ASGCameraMode* EndShotWaitCamera				= nullptr;
	UPROPERTY() class ASGCameraMode* StartFixedCamera				= nullptr;		//시작 지면 고정 카메라
	UPROPERTY() class ASGCameraMode* ReverseCamera					= nullptr;
	UPROPERTY() class ASGCameraMode* ReverseLandFixedCamera			= nullptr;		//지면 고정 역방향 카메라
	UPROPERTY() class ASGCameraMode* PuttingFixedCamera					= nullptr;
	UPROPERTY() class ASGCameraMode* SideCamera						= nullptr;
	UPROPERTY() class ASGCameraMode* BadPlaceHitFixedCamera			= nullptr;
	UPROPERTY() class ASGCameraMode* PuttingZoomCamera				= nullptr;		//퍼팅 줌인 카메라
	UPROPERTY() class ASGCameraMode* PuttingTraceCamera				= nullptr;		//퍼팅 추적 카메라[일반]
	UPROPERTY() class ASGCameraMode* SkyTraceCamera					= nullptr;		//하늘이 더 보이는 특수 추적 카메라
	
	UPROPERTY() TObjectPtr<UCameraComponent> CurFollowCam = nullptr;
	
	UPROPERTY() USGGameInstance* GameInst = nullptr;
	UPROPERTY() APlayerController* PlayerController = nullptr;
	UPROPERTY() ASGPlayerCharacter* PlayerChar = nullptr;
	UPROPERTY() ASGCourseMode* CourseMode = nullptr;
private:
	bool CanTickCameraWork		= false;
	bool IsLanding		= false;
	bool IsPutting		= false;
	bool IsDrivingMode	= false;
	bool IsPenaltyArea	= false;
	bool IsNotChange	= false;
	bool IsTeeShot		= true;
	bool IsHitBadPlace	= false;
	bool IsHoleIn		= false;			// Hole에 공이 들어갔다고 판단
	bool IsPredictGreenLanding = false;

	bool IsBadImpacted = false;				//잘못 맞은 경우
	bool IsTooCloseCalled = false;
	
	float flyingTime	= 0;
	const float LandingWaitTime = 1.5f;

	float TargetHorizontalSafeAreaRatio = 0.6f;
	float DefaultTargetHorizontalSafeAreaRatio = 0.6f;
	bool IsChangedSafeAreaRatioAfterImpacted = false;
	
	float ballPower					= 0;
	float launchAngle				= 0;
	float sideSpinRate				= 0;
	float launchSideAngleInDegree	= 0;	//초기 바라보는 방향에서 좌우로 얼마나 회전하여 쳤는가? [라디안 값]
	
	float sideCameraBeginTime = 0;			//사이드 카메라 시작 시간
	float sideCameraPassTime = 0;			//사이드 카메라로 보여주고 흘러간 시간 

	float traceCameraBreakTime = 0;

	float PuttingPassTime = 0;				//퍼팅 진행 시간
	float LandingTime = 0;					//하강 진행 시간
	float ImpactedTime = 0;

	bool bIsTraceCameraSocketChangeLerping = true;					//(구)추적 카메라 Socket 변동 Lerp 중인가? 
	float TraceCameraSocketChangeLerpTime = 1.0f;					//Lerp 소요 시간
	float TraceCameraSocketChangeTime = 0;							//Lerp 진행도

	bool bIsTraceCameraLagChangeLerping = true;						//(구)추적 카메라 Apex 근처에서 당기는 연출을 위한 LagSpeed 변동 Lerp 2 중인가?
	float TraceCameraLagLerpTime = 2.0f;							//Lerp 소요 시간
	float TraceCameraLagChangeTime = 0;								//Lerp 진행도

	bool bIsTraceCameraLagChangeLerping2 = true;					//(구)추적 카메라 하강시 더 바짝 쫓아가는 연출을 위한 LagSpeed 변동 Lerp
	float TraceCameraLagLerpTime2 = 2.0f;							//Lerp 소요 시간
	float TraceCameraLagChangeTime2 = 0;							//Lerp 진행도

	bool bIsTraceCameraRotateChangeAfterImpactedLerping = true;
	float TraceCameraRotateChangeAfterImpactedTime = 5.0f;
	float TraceCameraRotateTimeChangeAfterImpactedTime = 0;

	bool bIsTraceCameraSocketChangeLerping2 = true;					//(구)추적 카메라 공이 지면에 닿은 직후 회전 느리게 하기 위한 Lerp 중인가?
	float TraceCameraSocketChangeLerpTime2 = 2.0f;					//Lerp 소요 시간
	float TraceCameraSocketChangeTime2 = 0;							//Lerp 진행도

	bool bIsZoomInLerping = true;
	float ZoomInLerpTime = 3.5f;
	float ZoomInPassTime = 0;

	float SkyTraceCameraDownVectorAfterLandingLerpTime = 0.5f;			//Lerp 소요 시간
	float SkyTraceCameraDownVectorAfterLandingPassTime = 0;				//Lerp 진행도
	
	bool FlagCameraToReverseCamera = true;
	bool IsBadPlaceHitCameraLocated = false;

	bool IsUpperOutSafeAreaDetected = false;		//윗쪽으로 SafeArea를 벗어난 경우가 있었는가?
	bool IsBottomOutSafeAreaDetected = false;		//아랫쪽으로 SafeArea를 벗어난 경우가 있었는가?

	bool IsBeforeApexCamNumDecided = false;			//Camera 첫번째 Step 의 카메라 형태가 결정되었는가?
	bool IsAfterApexCamNumDecided = false;			//Camera 두번째 Step 의 카메라 형태가 결정되었는가?

	bool IsDecalRequestAvailable = false;
	bool IsInitTraceCamera = false;

	bool IsTooClosePutting = false;
	
	bool IsApexPositionSet = false;				//Apex지점을 저장했는가?
	bool IsLowApexInitialized = false;			//LowApex인지 이미 확인했는가?
	bool IsLowApex = false;						//LowApex인가?
	bool IsApexUnderGroundFix = false;			//카메라 경로 지면 보정 기준 높이보다 Apex가 아래인가?

	bool IsTraceCameraPrevAlreadyStopped = false;
	bool IsPassedCamBrakePosition = false;
	bool IsRemoveCamPointToEndPoint = false;

	bool IsAlreadyPauseTrace = false;

	bool IsDebugSkyTraceState = false;
	bool IsPredictApexSet = false;

	const float GreenTrailLimitHeight = 200.0f;
	const float NoneGreenTrailLimitHeight = 300.0f;

	const float OcclusionGrassCheckTimeInterval = 1.0f;
	float OcclusionCheckGrassPassTime = 0.0f;

	bool IsFoliageTimerInitialized = false;
	
	FVector BeginForwardDir = FVector::ForwardVector;		//시작 직후 전방 벡터
	
	int32 CameraStep = 0;

	FVector apexPosition = FVector::ZeroVector;
	FVector oldLocation = FVector::ZeroVector;
	FVector startLocation = FVector::ZeroVector;

	FVector sideCameraLocation = FVector::ZeroVector;
	FVector predictLandingPosition = FVector::ZeroVector;
	FVector predictLandingPositionForSkyTrace = FVector::ZeroVector;
	FVector predictPuttingPosition = FVector::ZeroVector;
	FVector predictApexPosition = FVector::ZeroVector;
	FVector reverseLandFixedCameraLocation = FVector::ZeroVector;
	
	FRotator StartPlayerRot = FRotator::ZeroRotator;

	FVector2D BackTraceDir2D = FVector2D::ZeroVector; // 추적카메라 (역방향) 의 방향

	float PrevBallToHoleDist = 10000;
	
	FVector PuttingTraceCameraDir = FVector::ZeroVector;
	float PuttingTraceCameraLastCameraDist = 300;			//정방향에서 공이 방향이 꺾인 직후 카메라와 공 사이 거리
	bool IsPuttingBallForwardMove = true;
	bool IsPuttingZoomCameraSetLocationRotation = false;

	FVector BallVelocityBeforeImpacted = FVector::ZeroVector;
	FVector WindVector = FVector::ZeroVector;
	float WindPower = 0.0f;
	
	FVector2d ScreenSize = FVector2d(1920, 1080);

	bool IsBallImpacted = false;
	bool IsBallAlmostImpacted = false;
	bool PrevBallImpacted = false;
	bool IsUnder100Shot = false;

	bool IsSkyTraceImpactedPathAdded = false;
	bool CanAddLandingPathLine = true;		//추가 라인을 그릴 수 있는 상태인가?
	bool IsAlreadyAddBezierLandingLine = false;		//이미 Bezier 커브 라인을 그린 상태인가?
	
	FVector BallImpactedPosition = FVector::ZeroVector;							//공 지면 충돌 직후 공 위치
	FVector TraceCameraPrevLocationAfterBallImpacted = FVector::ZeroVector;		//공 지면 충돌 직후 추적 카메라 위치
	
	FVector BallForwardVectorAfterImpact = FVector::ZeroVector;
	
	ASGIPFinder* IPFinder = nullptr;

	UFirstCameraRecord* SelectedFirstCameraRecord = nullptr;
	TArray<USecondCameraRecord*> SecondCameraRecordPool;
	USecondCameraRecord* SelectedSecondCameraRecord = nullptr;
	UPuttCameraRecord* SelectedPuttingCameraRecord = nullptr;
	
	FTimerHandle TreeHitHandle;
	FTimerHandle PuttingTraceCameraWaitHandle;
	FTimerHandle SkyTraceHandle;
	
	class PercentageData
	{
	public:
		int Min;
		int Max;

		PercentageData(int32 Min, int32 Max);
	};
	
	float ShiftRotateAngle = 0;
	float ShiftSecondShotOffSetAngle = -1.6f;		//FOV 차이에 따른 세컨샷 OffSet 회전값
	float ShiftPuttingShotOffSetAngle = 1.0f;		//FOV 차이에 따른 퍼팅 OffSet 회전값
	
	float ShiftLeftAmount = 0;
	float ShiftOffsetRotate = 0.4f;

	float DistanceAlongSpline = 0.0f;
	float CameraSpeed = 100.0f;

	ASGCameraRailActor* CameraRailActor;
	FTimerHandle FoliageFindTimer;					//FoliageActor를 찾는 함수를 주기적으로 호출하기 위함
};
