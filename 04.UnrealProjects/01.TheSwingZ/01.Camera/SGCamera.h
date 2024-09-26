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
	void ImpactCameraSetting(ERenderType::Type RenderType, bool IsTeeShot);	// Shot �̺�Ʈ
	
	void SetCurFollowCam();
	void SetStartPlayerRot(FRotator rot) { StartPlayerRot = rot; }
	void EndShot(); // ����Ʈ�� ������ �÷��̾� ī�޶�� ��ȯ

	bool CanTraceCamera = true;
	bool CanRotateTraceCamera = true;
	bool CanGreenCameraLookHoleCup = true;
	bool EndShotWaitCameraNeed = false;		//�� ���� ���� �÷��̾� �غ� ������ ����ϴ� ī�޶� �ʿ��Ѱ�?
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
	void SpawnCamera();	// ī�޶� �ʱ�ȭ

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

	void GroundCheck(FVector CameraLoc, FVector& ResultLoc);			//���� ��ġ ���� �¿� OB ���� ã��
	FVector GetVerticalGroundPos(FVector From);
	
	void GetLeftRightFairWayPos(FVector MoveDir, FVector CurrentLocation, FVector &LeftLoc, FVector &RightLoc);		//���̵� ī�޶� ��ġ ���� �¿� Fairway ���� ã��
	bool IsFineConditionToSideCamera(float SideSpinRate, float SideAngleInDegree);
	
	void GetFairWayPosFromOBToApex(FVector From, FVector To, FVector &ResultLoc);

	bool IsOBZPos(FVector CurrentLocation);
	bool IsOnGreen(FVector Pos);

	void TraceCamZPosFix(FVector& CameraLoc);											//������ ���� ī�޶� ���� �Ʒ� �˻�
	void TraceCamZPosFix(FVector& SpringArmLoc, FVector& CameraLoc);					//������ ���� ī�޶� ���� �Ʒ� �˻� [�׽�Ʈ ����]
	void StartFixedCamZPosFix(FVector& CameraLoc);										//���� ���� ī�޶� ���� �Ʒ� �˻�
	void PuttingTraceCamZPosFix(FVector& CameraLoc);									//���� ���� ī�޶� ���� �Ʒ� �˻�
	
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
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 BeforeApexCamNum = 0; // 1 : ���� ���� ī�޶�, 2 : ���� ī�޶� (������), 3 : �ű� ���� ī�޶� (������) SkyTrace
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 AfterApexCamNum = 0; // 1 : ���� ���� ī�޶�, 2 : ���̵� ī�޶�, 3 : ���� ī�޶� (������), 5 : BadPlace �ε��� ī�޶�, 6 : �ű� ���� ī�޶� (������) SkyTrace, 7 : ���� ���� ī�޶�
	
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 PuttingCamNum = 1; // 1 : ���� ���� ī�޶� (������), 2 : ���� ���� ī�޶� (������)
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) int32 DebugAfterApexCamNum = -1; // -1 : ���� ApexCam ������� ����. �� �ܿ� ������ ���� ����

	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float TraceCameraTargetArmLength = 300; // ���� ī�޶� ���� Arm Length
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceStopDist = 700; //Sky ���� ī�޶� ���� �Ÿ�
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceLowApexStopDist = 700; //Sky LowApex�� ��� ���� ī�޶� ���� �Ÿ�
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceAscendVelocityRatio = 1.0f; //��� �ӵ�
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceDescendVelocityRatio = 1.0f; //�ϰ� �ӵ�
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceBezierCurveLength = 200.0f;  //�ϰ� ��� ������ Ŀ�� ��
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SkyTraceLeftVectorSpeed = 1.0f;	  //Sky ���� ī�޶� ��½� �������� ��θ� �󸶳� �̵���ų��
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float WindVectorLandingPosOffset = 500;		//���� ���� ���� �¹ٶ��� ��� ���� �� [�󸶳� ���� ������ ������ ��� ���ΰ�?]
	
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float GroundCheckUpperAmount = 100; //���� �Ʒ� ���� ��ġ �� [�󸶳� ���� �ø� ���� �� ���ΰ�?]
	UPROPERTY(BlueprintReadOnly, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float BackTraceHeightRate = 0.66f; // ���� ī�޶� ���� ����

	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideCameraLocationZRatio = 1.0; // ���̵� ī�޶� Z ��ġ�� APEX�� ��% ������ ��ġ�� ������. [1�� �������� ���� ���� ����]
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideCameraLocationXYRatio = 0.5; // ���̵� ī�޶� X ��ġ�� APEX�� ��% ������ ��ġ�� ������. [1�� �������� ���� ���� ����]
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideCameraDist = 1000; // ���̵� ī�޶� �󸶳� ���� ������ ������ ������

	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float SideBellowCameraLocationXYRatio = 0.5; // 45�� ���� ���̵� ī�޶� X ��ġ�� APEX�� ��% ������ ��ġ�� ������. [1�� �������� ���� ���� ����]
	
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float ReverseFixedCameraHeight = 300; // ���� (������) ���� ī�޶� �������κ��� ����
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float ReverseFixedCameraSideDist = 1000; // ���� (������) ���� ī�޶�� ���� ���� ���� ������ �Ÿ�
		//���� ���� �ӵ�
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float PuttingCameraZoomOutSpeed = 0;		//���� �ܾƿ� �ӵ�
	UPROPERTY(EditAnywhere, Category = "SG Cam", meta = (AllowPrivateAccess = "true")) float PuttingCameraZoomInMin = 30;		//���� ���� �ִ밪

	UPROPERTY(EditAnywhere, Category = "SG Cam") TSoftObjectPtr<class UMaterialParameterCollection> OcclusionMPC;
	UPROPERTY(Transient) TSoftObjectPtr<class UMaterialParameterCollectionInstance> OcclusionMPCInstance;
private:
	FVector holecupLocation = FVector::ZeroVector;
	
	UPROPERTY() class ASGCameraMode* TraceCamera					= nullptr;
	UPROPERTY() class ASGCameraMode* EndShotWaitCamera				= nullptr;
	UPROPERTY() class ASGCameraMode* StartFixedCamera				= nullptr;		//���� ���� ���� ī�޶�
	UPROPERTY() class ASGCameraMode* ReverseCamera					= nullptr;
	UPROPERTY() class ASGCameraMode* ReverseLandFixedCamera			= nullptr;		//���� ���� ������ ī�޶�
	UPROPERTY() class ASGCameraMode* PuttingFixedCamera					= nullptr;
	UPROPERTY() class ASGCameraMode* SideCamera						= nullptr;
	UPROPERTY() class ASGCameraMode* BadPlaceHitFixedCamera			= nullptr;
	UPROPERTY() class ASGCameraMode* PuttingZoomCamera				= nullptr;		//���� ���� ī�޶�
	UPROPERTY() class ASGCameraMode* PuttingTraceCamera				= nullptr;		//���� ���� ī�޶�[�Ϲ�]
	UPROPERTY() class ASGCameraMode* SkyTraceCamera					= nullptr;		//�ϴ��� �� ���̴� Ư�� ���� ī�޶�
	
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
	bool IsHoleIn		= false;			// Hole�� ���� ���ٰ� �Ǵ�
	bool IsPredictGreenLanding = false;

	bool IsBadImpacted = false;				//�߸� ���� ���
	bool IsTooCloseCalled = false;
	
	float flyingTime	= 0;
	const float LandingWaitTime = 1.5f;

	float TargetHorizontalSafeAreaRatio = 0.6f;
	float DefaultTargetHorizontalSafeAreaRatio = 0.6f;
	bool IsChangedSafeAreaRatioAfterImpacted = false;
	
	float ballPower					= 0;
	float launchAngle				= 0;
	float sideSpinRate				= 0;
	float launchSideAngleInDegree	= 0;	//�ʱ� �ٶ󺸴� ���⿡�� �¿�� �󸶳� ȸ���Ͽ� �ƴ°�? [���� ��]
	
	float sideCameraBeginTime = 0;			//���̵� ī�޶� ���� �ð�
	float sideCameraPassTime = 0;			//���̵� ī�޶�� �����ְ� �귯�� �ð� 

	float traceCameraBreakTime = 0;

	float PuttingPassTime = 0;				//���� ���� �ð�
	float LandingTime = 0;					//�ϰ� ���� �ð�
	float ImpactedTime = 0;

	bool bIsTraceCameraSocketChangeLerping = true;					//(��)���� ī�޶� Socket ���� Lerp ���ΰ�? 
	float TraceCameraSocketChangeLerpTime = 1.0f;					//Lerp �ҿ� �ð�
	float TraceCameraSocketChangeTime = 0;							//Lerp ���൵

	bool bIsTraceCameraLagChangeLerping = true;						//(��)���� ī�޶� Apex ��ó���� ���� ������ ���� LagSpeed ���� Lerp 2 ���ΰ�?
	float TraceCameraLagLerpTime = 2.0f;							//Lerp �ҿ� �ð�
	float TraceCameraLagChangeTime = 0;								//Lerp ���൵

	bool bIsTraceCameraLagChangeLerping2 = true;					//(��)���� ī�޶� �ϰ��� �� ��¦ �Ѿư��� ������ ���� LagSpeed ���� Lerp
	float TraceCameraLagLerpTime2 = 2.0f;							//Lerp �ҿ� �ð�
	float TraceCameraLagChangeTime2 = 0;							//Lerp ���൵

	bool bIsTraceCameraRotateChangeAfterImpactedLerping = true;
	float TraceCameraRotateChangeAfterImpactedTime = 5.0f;
	float TraceCameraRotateTimeChangeAfterImpactedTime = 0;

	bool bIsTraceCameraSocketChangeLerping2 = true;					//(��)���� ī�޶� ���� ���鿡 ���� ���� ȸ�� ������ �ϱ� ���� Lerp ���ΰ�?
	float TraceCameraSocketChangeLerpTime2 = 2.0f;					//Lerp �ҿ� �ð�
	float TraceCameraSocketChangeTime2 = 0;							//Lerp ���൵

	bool bIsZoomInLerping = true;
	float ZoomInLerpTime = 3.5f;
	float ZoomInPassTime = 0;

	float SkyTraceCameraDownVectorAfterLandingLerpTime = 0.5f;			//Lerp �ҿ� �ð�
	float SkyTraceCameraDownVectorAfterLandingPassTime = 0;				//Lerp ���൵
	
	bool FlagCameraToReverseCamera = true;
	bool IsBadPlaceHitCameraLocated = false;

	bool IsUpperOutSafeAreaDetected = false;		//�������� SafeArea�� ��� ��찡 �־��°�?
	bool IsBottomOutSafeAreaDetected = false;		//�Ʒ������� SafeArea�� ��� ��찡 �־��°�?

	bool IsBeforeApexCamNumDecided = false;			//Camera ù��° Step �� ī�޶� ���°� �����Ǿ��°�?
	bool IsAfterApexCamNumDecided = false;			//Camera �ι�° Step �� ī�޶� ���°� �����Ǿ��°�?

	bool IsDecalRequestAvailable = false;
	bool IsInitTraceCamera = false;

	bool IsTooClosePutting = false;
	
	bool IsApexPositionSet = false;				//Apex������ �����ߴ°�?
	bool IsLowApexInitialized = false;			//LowApex���� �̹� Ȯ���ߴ°�?
	bool IsLowApex = false;						//LowApex�ΰ�?
	bool IsApexUnderGroundFix = false;			//ī�޶� ��� ���� ���� ���� ���̺��� Apex�� �Ʒ��ΰ�?

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
	
	FVector BeginForwardDir = FVector::ForwardVector;		//���� ���� ���� ����
	
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

	FVector2D BackTraceDir2D = FVector2D::ZeroVector; // ����ī�޶� (������) �� ����

	float PrevBallToHoleDist = 10000;
	
	FVector PuttingTraceCameraDir = FVector::ZeroVector;
	float PuttingTraceCameraLastCameraDist = 300;			//�����⿡�� ���� ������ ���� ���� ī�޶�� �� ���� �Ÿ�
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
	bool CanAddLandingPathLine = true;		//�߰� ������ �׸� �� �ִ� �����ΰ�?
	bool IsAlreadyAddBezierLandingLine = false;		//�̹� Bezier Ŀ�� ������ �׸� �����ΰ�?
	
	FVector BallImpactedPosition = FVector::ZeroVector;							//�� ���� �浹 ���� �� ��ġ
	FVector TraceCameraPrevLocationAfterBallImpacted = FVector::ZeroVector;		//�� ���� �浹 ���� ���� ī�޶� ��ġ
	
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
	float ShiftSecondShotOffSetAngle = -1.6f;		//FOV ���̿� ���� ������ OffSet ȸ����
	float ShiftPuttingShotOffSetAngle = 1.0f;		//FOV ���̿� ���� ���� OffSet ȸ����
	
	float ShiftLeftAmount = 0;
	float ShiftOffsetRotate = 0.4f;

	float DistanceAlongSpline = 0.0f;
	float CameraSpeed = 100.0f;

	ASGCameraRailActor* CameraRailActor;
	FTimerHandle FoliageFindTimer;					//FoliageActor�� ã�� �Լ��� �ֱ������� ȣ���ϱ� ����
};
