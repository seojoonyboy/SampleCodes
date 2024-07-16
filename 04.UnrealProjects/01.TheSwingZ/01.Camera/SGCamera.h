#pragma once

#include "CoreMinimal.h"
#include "SGCommon.h"
#include "GameFramework/Actor.h"
#include "SGIPFinder.h"
#include "SGShotCameraTable.h"
#include "SGCamera.generated.h"

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

private:
	void SpawnCamera();	// 카메라 초기화

	UFirstCameraRecord* GetFirstCameraRecordByIndex(int32 ID);
	TArray<UFirstCameraRecord*> GetFirstCameraRecordsByPriority(int32 ID);
	UFirstCameraRecord* GetTargetFirstCameraPriority(int32 Priority, ASGPlayerCharacter* Player);

	TArray<USecondCameraRecord*> GetSecondCameraRecordsByPriority(int32 ID);
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
	void InitFixedCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitReverseFixedCameraWork(APlayerController* Controller, FVector BallLocation);
	void InitSideCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitSideBellowCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitReverseTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);

	void FixedCameraWork(ASGPlayerCharacter* Player);
	
	void TraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);
	void DrivingModeTraceCameraSubWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);
	void CourseModeTraceCameraSubWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);

	void PuttingZoomCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void InitPuttingTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player);
	void PuttingTraceCameraWork(APlayerController* Controller, ASGPlayerCharacter* Player, float DeltaTime);
	
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
	
	void GetLeftRightFairWayPos(FVector MoveDir, FVector CurrentLocation, FVector &LeftLoc, FVector &RightLoc);		//사이드 카메라 위치 기준 좌우 Fairway 지점 찾기
	bool IsFineConditionToSideCamera(float SideSpinRate, float SideAngleInDegree);
	
	void GetFairWayPosFromOBToApex(FVector From, FVector To, FVector &ResultLoc);

	bool IsOBZPos(FVector CurrentLocation);

	void TraceCamZPosFix(FVector& CameraLoc);											//정방향 추적 카메라 지면 아래 검사
	void TraceCamZPosFix(FVector& SpringArmLoc, FVector& CameraLoc);					//정방향 추적 카메라 지면 아래 검사 [테스트 버전]
	void StartFixedCamZPosFix(FVector& CameraLoc);										//지면 고정 카메라 지면 아래 검사
	void PuttingTraceCamZPosFix(FVector& CameraLoc);									//퍼팅 추적 카메라 지면 아래 검사
	
	bool IsSafeSurface(EPhysicalSurface Surface);
	
	void PredictLandingPosition(FVector BallLocation, FVector BallVelocity, FVector AngularVelocity, FVector BowlerFreeViewLastPosition, FVector TargetWindForceVector, int MagnusForceCount);

	bool IsPredictWrongLandingZone();
	bool GetIsUnder100(ASGPlayerCharacter* Player, FVector HoleCupLoc);

	bool IsAvailableThirdCamera(APlayerController* Controller, ASGPlayerCharacter* Player);
	bool CheckUnSafeScreenArea(int32 CurrentScreenY, bool& UpperUnSafe, bool& BottomUnsafe);

private:
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 BeforeApexCamNum = 0; // 1 : 시작 고정 카메라, 2 : 추적 카메라 (정방향)
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 AfterApexCamNum = 0; // 1 : 지면 고정 카메라, 2 : 사이드 카메라, 3 : 추적 카메라 (정방향)
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 PuttingCamNum = 1; // 1 : 퍼팅 추적 카메라 (정방향), 2 : 퍼팅 고정 카메라 (역방향)
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 DebugAfterApexCamNum = -1; // -1 : 강제 ApexCam 사용하지 않음. 그 외에 값으로 강제 세팅

	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float TraceCameraTargetArmLength = 300; // 추적 카메라 지정 Arm Length
	
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float BackTraceHeightRate = 0.66f; // 추적 카메라 높이 비율

	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideCameraLocationZRatio = 1.0; // 사이드 카메라 Z 위치가 APEX의 몇% 지점에 위치할 것인지. [1에 가까울수록 예상 낙구 지점]
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideCameraLocationXYRatio = 0.5; // 사이드 카메라 X 위치가 APEX의 몇% 지점에 위치할 것인지. [1에 가까울수록 예상 낙구 지점]
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideCameraDist = 1000; // 사이드 카메라가 얼마나 공과 옆으로 떨어져 있을지

	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideBellowCameraLocationXYRatio = 0.5; // 45도 측면 사이드 카메라 X 위치가 APEX의 몇% 지점에 위치할 것인지. [1에 가까울수록 예상 낙구 지점]
	
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float ReverseFixedCameraHeight = 300; // 지면 (역방향) 고정 카메라 지면으로부터 높이
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float ReverseFixedCameraSideDist = 1000; // 지면 (역방향) 고정 카메라와 낙구 예측 지점 사이의 거리

	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float PuttingCameraZoomInSpeed = 3.5;		//퍼팅 줌인 속도
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float PuttingCameraZoomOutSpeed = 0;		//퍼팅 줌아웃 속도
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float PuttingCameraZoomInMin = 30;		//퍼팅 줌인 최대값

private:
	FVector holecupLocation = FVector::ZeroVector;
	
	UPROPERTY() class ASGCameraMode* TraceCamera					= nullptr;
	UPROPERTY() class ASGCameraMode* EndShotWaitCamera				= nullptr;
	UPROPERTY() class ASGCameraMode* StartFixedCamera				= nullptr;		//시작 지면 고정 카메라
	UPROPERTY() class ASGCameraMode* ReverseCamera					= nullptr;
	UPROPERTY() class ASGCameraMode* ReverseLandFixedCamera			= nullptr;		//지면 고정 역방향 카메라
	UPROPERTY() class ASGCameraMode* GreenCamera					= nullptr;
	UPROPERTY() class ASGCameraMode* SideCamera						= nullptr;
	UPROPERTY() class ASGCameraMode* BadPlaceHitFixedCamera			= nullptr;
	UPROPERTY() class ASGCameraMode* PuttingZoomCamera				= nullptr;		//퍼팅 줌인 카메라
	UPROPERTY() class ASGCameraMode* PuttingTraceCamera				= nullptr;		//퍼팅 추적 카메라[일반]
	
	UPROPERTY() TObjectPtr<UCameraComponent> CurFollowCam = nullptr;

private:
	bool CanTickCameraWork		= false;
	bool IsLanding		= false;
	bool IsPutting		= false;
	bool IsDrivingMode	= false;
	bool IsPenaltyArea	= false;
	bool IsNotChange	= false;
	bool IsTeeShot		= true;
	bool IsHitBadPlace	= false;
	bool IsHoleIn		= false;	// Hole에 공이 들어갔다고 판단

	bool IsBadImpacted = false;				//잘못 맞은 경우
	
	float flyingTime	= 0;
	
	float ballPower					= 0;
	float launchAngle				= 0;
	float sideSpinRate				= 0;
	float launchSideAngleInDegree	= 0;	//초기 바라보는 방향에서 좌우로 얼마나 회전하여 쳤는가? [라디안 값]
	
	float sideCameraBeginTime = 0;			//사이드 카메라 시작 시간
	float sideCameraPassTime = 0;			//사이드 카메라로 보여주고 흘러간 시간 

	float traceCameraBreakTime = 0;

	float PuttingPassTime = 0;				//퍼팅 진행 시간
	
	bool FlagCameraToReverseCamera = true;
	bool IsBadPlaceHitCameraLocated = false;

	bool IsUpperOutSafeAreaDetected = false;		//윗쪽으로 SafeArea를 벗어난 경우가 있었는가?
	bool IsBottomOutSafeAreaDetected = false;		//아랫쪽으로 SafeArea를 벗어난 경우가 있었는가?

	bool IsBeforeApexCamNumDecided = false;
	bool IsAfterApexCamNumDecided = false;		//Camera 두번째 Step 의 카메라 형태가 결정되었는가?

	bool IsDecalRequestAvailable = false;
	bool IsInitTraceCamera = false;

	FVector BeginForwardDir = FVector::ForwardVector;		//시작 직후 전방 벡터
	
	int32 CameraStep = 0; 

	FVector oldLocation = FVector::ZeroVector;
	FVector startLocation = FVector::ZeroVector;

	FVector sideCameraLocation = FVector::ZeroVector;
	FVector predictLandingPosition = FVector::ZeroVector;
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
	
	FVector2d ScreenSize = FVector2d(1920, 1080);

	bool IsBallImpacted = false;
	FVector BallImpactedPosition = FVector::ZeroVector;							//공 지면 충돌 직후 공 위치
	FVector TraceCameraPrevLocationAfterBallImpacted = FVector::ZeroVector;		//공 지면 충돌 직후 추적 카메라 위치
	
	FVector BallForwardVectorAfterImpact = FVector::ZeroVector;
	
	ASGIPFinder* IPFinder = nullptr;

	UFirstCameraRecord* SelectedFirstCameraRecord = nullptr;
	USecondCameraRecord* SelectedSecondCameraRecord = nullptr;
	UPuttCameraRecord* SelectedPuttingCameraRecord = nullptr;
	
	FTimerHandle TreeHitHandle;
	FTimerHandle PuttingTraceCameraWaitHandle;
	
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
};
