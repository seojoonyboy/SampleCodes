// Description page of Project Settings.

#include "SGCameraRailActor.h"

#include "SGCamera.h"
#include "SGCourseMode.h"
#include "SGGroundChecker.h"
#include "SGPlayerCharacter.h"
#include "Components/SplineComponent.h"

// Sets default values
ASGCameraRailActor::ASGCameraRailActor()
{
	// Set this actor to call Tick() every frame.  You can turn this off to improve performance if you don't need it.
	PrimaryActorTick.bCanEverTick = false;

	splinePath = CreateDefaultSubobject<USplineComponent>(TEXT("SPLINE_PATH"));
	splinePath->SetupAttachment(RootComponent);

	ClearTracePoint();			// 기본으로 생성된 2개의 점 제거
}

// Called when the game starts or when spawned
void ASGCameraRailActor::BeginPlay()
{
	Super::BeginPlay();
}

void ASGCameraRailActor::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	if(IsValid(GetWorld()))
	{
		GetWorld()->GetTimerManager().ClearTimer(CheckBallTimer);
	}
	
	Super::EndPlay(EndPlayReason);
}

void ASGCameraRailActor::UpdateCameraDirector(ASGCamera* mCameraDirector)
{
	CameraDirector = mCameraDirector;
}

void ASGCameraRailActor::UpdatePlayer(ASGPlayerCharacter* mPlayerCharacter)
{
	Player = mPlayerCharacter;
	PrevBallLocation = Player->GetActorLocation();
}

void ASGCameraRailActor::UpdateImpactedVector(FVector mImpactedVector)
{
	mImpactedVector.Z = 0;
	BallForwardVectorAfterImpact = mImpactedVector;
}

void ASGCameraRailActor::UpdatePredictedApex(FVector mPredictedApexVector)
{
	PredictedApexLocation = mPredictedApexVector;
}

void ASGCameraRailActor::UpdateWindVector(FVector mWindVector)
{
	WindVector = mWindVector;
}

void ASGCameraRailActor::UpdatePlateInfo(bool IsRight)
{
	IsRightPlate = IsRight;
}


void ASGCameraRailActor::SetEnable(bool IsDrivingRange)
{
	IsEnable = false;
	// IsEnable = IsDrivingRange;
}

void ASGCameraRailActor::BeginTrace()
{
	if(IsValid(Player))
	{
		if(IsValid(GetWorld()))
		{
			GetWorld()->GetTimerManager().ClearTimer(CheckBallTimer);
			GetWorld()->GetTimerManager().SetTimer(CheckBallTimer, this, &ASGCameraRailActor::NotifyBallState, CHECK_TICK_TIMER, true);

			PrevLandingCameraDist = 300;
			
			FVector BallLocation = Player->GetActorLocation();
			FVector BackwardVector = -1 * BallForwardVectorAfterImpact;

			// SG_LOG(Log, "TargetCameraWay %s", *TargetCameraWay.ToString());
			float TargetRotateAngle = IsRightPlate ? 90.0f : -90.0f;
			FVector LeftVector = BackwardVector.RotateAngleAxis(TargetRotateAngle, FVector::ZAxisVector).GetSafeNormal() * LeftVectorAmount;

			FVector TargetCameraWay = BallLocation + BackwardVector * PrevLandingCameraDist + LeftVector + FVector(0, 0, 100);
			
			AddTracePoint(TargetCameraWay, true);
		}
	}
}

void ASGCameraRailActor::NotifyBallState()
{
	if(IsValid(Player) && !IsPause && IsValid(CameraDirector))
	{
		bool IsLanding = CameraDirector->GetIsLanding();
		
		FVector BallLocation = Player->GetActorLocation();
		FVector BackwardVector = (PrevBallLocation - BallLocation).GetSafeNormal();
		BackwardVector.Z = 0;
		
		float TargetRotateAngle = IsRightPlate ? 90.0f : -90.0f;
		FVector LeftVector = BackwardVector.RotateAngleAxis(TargetRotateAngle, FVector::ZAxisVector).GetSafeNormal() * LeftVectorAmount;

		// SG_LOG(Log, "LeftVector %s", *LeftVector.ToString());
		
		//상승중일 때, 간격 유지
		if(!IsLanding)
		{
			// SG_LOG(Log, "LeftVectorAmount : %f", LeftVectorAmount);
			
			//상승하면서 점점 Left Vector 크기를 키운다.
			FVector TargetCameraWay = BallLocation + BackwardVector * 200 + LeftVector + FVector(0, 0, 20);
			
			AddTracePoint(TargetCameraWay, true);
		}

		PrevBallLocation = BallLocation;
	}
}

void ASGCameraRailActor::AddTracePoint(FVector newPos, bool bLengthCheck)
{
	int32 beforePointCount = splinePath->GetNumberOfSplinePoints();

	if (bLengthCheck && beforePointCount > 0)
	{
		FVector lastPos = splinePath->GetLocationAtSplinePoint(beforePointCount - 1, ESplineCoordinateSpace::World);
		
		float dist = FVector::Distance(newPos, lastPos);
		
		// SG_LOG(Log, "SkyTraceDrawLandingLineWithFollowCamera newPos %s", *newPos.ToString());
		// SG_LOG(Log, "SkyTraceDrawLandingLineWithFollowCamera dist %f", dist);

		// 직전 위치와 큰 차이가 없다면 궤적 경로에 추가하지 않는다
		if (dist < TRACE_MINIMUM_DIST) { return; }

		//역방향 포인트는 추가하지 않는다
		FVector NewPosDirection = (newPos - lastPos).GetSafeNormal();
		NewPosDirection.Z = 0;
		
		float DotRes = FVector::DotProduct(BallForwardVectorAfterImpact, NewPosDirection);

		// SG_LOG(Log, "SkyTraceDrawLandingLineWithFollowCamera DotRes %f", DotRes);
		// if(PredictedApexLocation != FVector::ZeroVector)
		// {
		// 	// SG_LOG(Log, "PredictApexLoaction %s", *PredictedApexLocation.ToString());
		// 	float Diff = FMath::Abs(PredictedApexLocation.Z - newPos.Z);
		// 	SG_LOG(Log, "Diff : %f", Diff);
		// 	
		// 	// if((Diff <= 100) || (newPos.Z > PredictedApexLocation.Z)) { newPos.Z = PredictedApexLocation.Z; }
		// }
		
		if(DotRes < 0) { return; }
	}

	if(CameraDirector->GetIsLanding()) { GroundCheck(newPos); }
	splinePath->AddSplineWorldPoint(newPos);
	splinePath->UpdateSpline();
}

void ASGCameraRailActor::RemoveLastTracePoint()
{
	int32 beforePointCount = splinePath->GetNumberOfSplinePoints();

	if (beforePointCount > 0)
	{
		splinePath->RemoveSplinePoint(beforePointCount - 1);	
	}
	splinePath->UpdateSpline();
}

void ASGCameraRailActor::AddTracePointAfterImpacted(FVector newPos, bool bLengthCheck)
{
	int32 beforePointCount = splinePath->GetNumberOfSplinePoints();

	if (bLengthCheck && beforePointCount > 0)
	{
		FVector lastPos = splinePath->GetLocationAtSplinePoint(beforePointCount - 1, ESplineCoordinateSpace::World);
		float dist = FVector::Distance(newPos, lastPos);

		// 직전 위치와 큰 차이가 없다면 궤적 경로에 추가하지 않는다
		if (dist < TRACE_MINIMUM_DIST) { return; }

		//역방향 포인트는 추가하지 않는다
		FVector NewPosDirection = (newPos - lastPos).GetSafeNormal();
		NewPosDirection.Z = 0;
		
		float DotRes = FVector::DotProduct(BallForwardVectorAfterImpact, NewPosDirection);
		if(DotRes < 0.5f) { return; }

		//Note. OB가 아닌 경우 급 상승 포인트는 추가하지 않는다.
		//OB인 경우 공이 튕겨서 언덕 위로 넘어가는 상승 포인트가 있을 수 있음
		if(!IsPredictOBLanding)
		{
			float DiffOffset = 50.0f;
			if(lastPos.Z + DiffOffset < newPos.Z){ return; }	
		}
	}

	GroundCheck(newPos);
	// SG_LOG(Log, "AddTracePointAfterImpacted newPos %s", *newPos.ToString());
	// splinePath->SetSplinePointType(beforePointCount - 1, ESplinePointType::Linear);
	splinePath->AddSplineWorldPoint(newPos);
	// splinePath->UpdateSpline();
}

void ASGCameraRailActor::ReFormCamPointToEndPoint(FVector CameraPos, FVector DirCamToBall)
{
	FVector ClosestPoint = splinePath->FindLocationClosestToWorldLocation(CameraPos, ESplineCoordinateSpace::World);
	int32 BeforePointCount = splinePath->GetNumberOfSplinePoints();

	int32 ClosestPointIndex = -1;
	float MinDistance = TNumericLimits<float>::Max();
	
	for(int i = BeforePointCount - 1; i > 0; i--)
	{
		FVector Point = splinePath->GetLocationAtSplinePoint(i, ESplineCoordinateSpace::World);
		float Distance = FVector::Dist(Point, ClosestPoint);
		
		if (Distance < MinDistance)
		{
			MinDistance = Distance;
			ClosestPointIndex = i;
		}
	}

	if(ClosestPointIndex != -1)
	{
		for(int i = BeforePointCount - 1; i > ClosestPointIndex; i--)
		{
			FVector Point = splinePath->GetLocationAtSplinePoint(i, ESplineCoordinateSpace::World);
			FVector Projection = CameraPos + FVector::DotProduct(Point - CameraPos, DirCamToBall) * DirCamToBall;
			Projection.Z = Point.Z;
			GroundCheck(Projection);
			splinePath->SetLocationAtSplinePoint(i, Projection, ESplineCoordinateSpace::World, true);
		}
	}

	splinePath->UpdateSpline();
}


void ASGCameraRailActor::EndTrace()
{
	GetWorld()->GetTimerManager().ClearTimer(CheckBallTimer);
	
	ClearTracePoint();
	
	LimitGroundZ = 500.0f;
	IsPause = false;
	IsPredictOBLanding = false;
}

void ASGCameraRailActor::PauseTrace()
{
	IsPause = true;
}

void ASGCameraRailActor::ClearTracePoint()
{
	splinePath->ClearSplinePoints();
}


FVector ASGCameraRailActor::GetLocationAtDistanceAlongSpline(float Distance, ESplineCoordinateSpace::Type CoordinateSpace)
{
	if(!IsValid(splinePath)) { return FVector::ZeroVector; }
	return splinePath->GetLocationAtDistanceAlongSpline(Distance, CoordinateSpace);
}

FRotator ASGCameraRailActor::GetRotationAtDistanceAlongSpline(float Distance, ESplineCoordinateSpace::Type CoordinateSpace)
{
	if(!IsValid(splinePath)) { return FRotator::ZeroRotator; }
	return splinePath->GetRotationAtDistanceAlongSpline(Distance, CoordinateSpace);
}

float ASGCameraRailActor::GetSplineLength()
{
	if(!IsValid(splinePath)){ return 0; }
	return splinePath->GetSplineLength();
}

void ASGCameraRailActor::AddBezierTraceToEndPoint(FVector startPoint, FVector endPoint)
{
	if(!IsValid(splinePath)) return;

	int32 BeforePointCount = splinePath->GetNumberOfSplinePoints();

	for(int i = BeforePointCount - 1; i > 0; i--)
	{
		FVector Point = splinePath->GetLocationAtSplinePoint(i, ESplineCoordinateSpace::World);
		if(Point.Z < startPoint.Z)
		{
			splinePath->RemoveSplinePoint(i);
		}
		else { break; }
	}

	splinePath->UpdateSpline();

	FVector P0 = startPoint;
	FVector P2 = endPoint;

	FVector P1 = FVector(P0.X, P0.Y, endPoint.Z);
	FVector P1ToP2Dir = (P2 - P1).GetSafeNormal();
	P1 += P1ToP2Dir * 200;

	int32 TotalPointNum = 10;
	
	for (int32 i = 0; i <= TotalPointNum; ++i)
	{
		float t = static_cast<float>(i) / TotalPointNum;

		FVector PointOnCurve = FMath::Pow(1 - t, 2) * P0 +
							   2 * (1 - t) * t * P1 +
							   FMath::Pow(t, 2) * P2;

		AddTracePoint(PointOnCurve, ESplineCoordinateSpace::World);
	}
}

void ASGCameraRailActor::AddBezier4PointTraceToEndPoint(FVector endPoint)
{
	if(!IsValid(splinePath)) return;
	
	int32 BeforePointCount = splinePath->GetNumberOfSplinePoints();
	
	FVector ClosestPoint = splinePath->FindLocationClosestToWorldLocation(endPoint, ESplineCoordinateSpace::World);
	
	FVector startPoint = splinePath->GetLocationAtSplinePoint(BeforePointCount - 1, ESplineCoordinateSpace::World);
	for(int i = BeforePointCount - 1; i > 0; i--)
	{
		FVector Point = splinePath->GetLocationAtSplinePoint(i, ESplineCoordinateSpace::World);
		if(FVector::Dist(Point, ClosestPoint) < 0.01f) break;

		float DotRes = FVector::DotProduct(BallForwardVectorAfterImpact, ClosestPoint);
		if(DotRes > 0) { splinePath->RemoveSplinePoint(i); }
	}

	FVector ForwardVector = BallForwardVectorAfterImpact;
	if(BeforePointCount > 1)
	{
		FVector LastPoint = startPoint;
		FVector SecondPrevPoint = splinePath->GetLocationAtSplinePoint(BeforePointCount - 2, ESplineCoordinateSpace::World);

		FVector TargetDir = (LastPoint - SecondPrevPoint).GetSafeNormal();
		ForwardVector = TargetDir;
	}

	FVector P0Location = startPoint;
	FVector P1Location = startPoint + ForwardVector * FVector::Dist(startPoint, endPoint) * 0.5f;
	FVector P3Location = FVector(P0Location.X + (endPoint.X - P0Location.X) * 0.4f, P0Location.Y + (endPoint.Y - P0Location.Y) * 0.4f, endPoint.Z);
	FVector P2Location = (P1Location + P3Location) * 0.5f;
	FVector P4Location = endPoint;

	int32 TotalPointNum = 15;
	
	for (int32 i = 0; i <= TotalPointNum; ++i)
	{
		float t = static_cast<float>(i) / TotalPointNum;

		FVector PointOnCurve = FMath::Pow(1 - t, 4) * P0Location +
							   4 * t * FMath::Pow(1 - t, 3) * P1Location +
							   6 * FMath::Pow(t, 2) * FMath::Pow(1 - t, 2) * P2Location +
							   4 * FMath::Pow(t, 3) * (1 - t) * P3Location +
							   FMath::Pow(t, 4) * P4Location;
		
		AddTracePoint(PointOnCurve, ESplineCoordinateSpace::World);
	}
	
	splinePath->SetSplinePointType(TotalPointNum, ESplinePointType::Linear);
}

FVector ASGCameraRailActor::GetLastPoint()
{
	if(!IsValid(splinePath)) return FVector::ZeroVector;
	
	int32 BeforePointCount = splinePath->GetNumberOfSplinePoints();
	FVector endPoint = splinePath->GetLocationAtSplinePoint(BeforePointCount - 1, ESplineCoordinateSpace::World);
	
	return endPoint;
	
}

void ASGCameraRailActor::GroundCheck(FVector& ResultLoc)
{
	FHitResult HitResult;

	FVector Start = ResultLoc + FVector(0, 0, 100);
	FVector End = ResultLoc - FVector(0, 0, LimitGroundZ);

	TArray<TEnumAsByte<EObjectTypeQuery>> ObjectTypes;
	ObjectTypes.Add(UEngineTypes::ConvertToObjectType(ECC_GameTraceChannel1));
	TArray<AActor*> IgnoreActors;
	
	bool bIsGround = UKismetSystemLibrary::LineTraceSingleForObjects(GetWorld(), Start, End, ObjectTypes, false, IgnoreActors,  EDrawDebugTrace::None, HitResult, true);
	if (bIsGround)
	{
		// SG_LOG(Log, "SGCameraRailActor.........GroundCheck");
		
		float ResultZ = HitResult.ImpactPoint.Z + LimitGroundZ;
		ResultLoc.Z = ResultZ;
	}
}

void ASGCameraRailActor::UpdateLimitGroundZ(float limitGroundZ)
{
	LimitGroundZ = limitGroundZ;
}

