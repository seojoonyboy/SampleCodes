#include "SGGrid.h"
#include "SGGameInstance.h"
#include "SGCourseMode.h"
#include "SGDelegateManager.h"
#include "SGScoreManager.h"
#include "SGPlayerCharacter.h"
#include "SGGroundChecker.h"
#include "SGDataCenter.h"
#include "Kismet/KismetMaterialLibrary.h"

ASGGrid::ASGGrid()
{
	PrimaryActorTick.bCanEverTick = true;

	ConstructorHelpers::FObjectFinder<UMaterialInstance> Mat(TEXT("/Game/Game/Componet/MT_Line_Inst.MT_Line_Inst"));
	if (Mat.Succeeded())
	{
		material = Mat.Object;
	}

	sceneComp = CreateDefaultSubobject<USceneComponent>(TEXT("SCENE COMPONENT"));
	sceneComp->SetupAttachment(RootComponent);
}

void ASGGrid::BeginPlay()
{
	Super::BeginPlay();

	//방어 코드
	{
		MaxVGridCount = 20;
		MaxHGridCount = 11;

		IsDebugGrid = false;
	}

	AddSpeedRatio = 0.5f;

	CreateGrid();
}

void ASGGrid::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	RemoveGrid();

	Super::EndPlay(EndPlayReason);
}

void ASGGrid::Tick(float DeltaTime)
{
	Super::Tick(DeltaTime);

	if (IsShow)
	{
		//UpdateGrid();

		for (int32 i = 0; i < DecalHPoints.Num(); ++i)
		{
			if (DecalHPoints.IsValidIndex(i) && DecalHSpeed.IsValidIndex(i) && DecalHTime.IsValidIndex(i))
			{
				if(IsDebugGrid)
				{
					DecalHTime[i] += DecalHSpeed[i] * DeltaTime * 2.0f;
				}
				else
				{
					DecalHTime[i] += DecalHSpeed[i] * DeltaTime;
				}
				
				if (1.f < DecalHTime[i])
				{
					DecalHTime[i] -= 2.f;
				}

				if (MIDs.IsValidIndex(i))
				{
					MIDs[i]->SetScalarParameterValue(TEXT("TimeParam"), DecalHTime.IsValidIndex(i) ? DecalHTime[i] * AddSpeedRatio : 0.5f);
				}
			}
		}
		for (int32 i = 0; i < DecalVPoints.Num(); ++i)
		{
			if (DecalVPoints.IsValidIndex(i) && DecalVSpeed.IsValidIndex(i) && DecalVTime.IsValidIndex(i))
			{
				if(IsDebugGrid)
				{
					DecalVTime[i] += DecalVSpeed[i] * DeltaTime * 2.0f;
				}
				else
				{
					DecalVTime[i] += DecalVSpeed[i] * DeltaTime;
				}
				
				if (1.f < DecalVTime[i])
				{
					DecalVTime[i] -= 2.f;
				}

				if (MIDs.IsValidIndex(i + DecalHPoints.Num()))
				{
					MIDs[i + DecalHPoints.Num()]->SetScalarParameterValue(TEXT("TimeParam"), DecalVTime.IsValidIndex(i) ? DecalVTime[i] * AddSpeedRatio : 0.5f);
				}
			}
		}
	}
}

void ASGGrid::ShowGrid()
{
	IsShow = true;

	USGGameInstance* const GameInst = GAME_INSTANCE;
	IS_VALID_RETURN(GameInst);
	USGDataCenter* const DataCenter = GameInst->GetDataCenter();
	IS_VALID_RETURN(DataCenter);
	USGScoreManager* const ScoreManager = GameInst->GetScoreManager();
	IS_VALID_RETURN(ScoreManager);
	ASGCourseMode* const CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();
	IS_VALID_RETURN(CourseMode);
	AActor* const HoleCup = CourseMode->GetHoleCup(ScoreManager->GetHoleNum());
	IS_VALID_RETURN(HoleCup);
	APlayerController* Controller = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
	IS_VALID_RETURN(Controller);
	ASGPlayerCharacter* const PlayerChar = Cast<ASGPlayerCharacter>(CourseMode->FindPlayerCharacterCurrentTurn());
	IS_VALID_RETURN(PlayerChar);

	if (PlayerChar->GetBallMoving())
	{
		IsShow = false;

		return;
	}

	const bool IsOnGreen = PlayerChar->GetStimpMeterType() == EStimpMeterType::Green || PlayerChar->GetStimpMeterType() == EStimpMeterType::Apron;
	const bool IsWrongGreen = PlayerChar->GetShotArrivalCondition() == EShotJudgeArrival::WrongGreen;

	// 그리드 설정이 비활성화여도 그린 혹은 에이프런에 공이 도착했다면 강제 활성화
	if (DataCenter->GridOn == false && IsOnGreen && IsWrongGreen == false)
	{
		DataCenter->GridOn = true;

		CourseMode->ForceSetGridOn(true);
	}

	if (DataCenter->GridOn == false)
	{
		IsShow = false;

		return;
	}

	HoleLoc = HoleCup->GetActorLocation();

	// 그리드 업데이트
	const float GridThickness = DataCenter->GetGridThicknessValue();

	if (IsOnGreen && IsWrongGreen == false)
	{
		float distPH = IsDebugGrid ?
			FVector::Dist2D(PlayerChar->GetActorLocation(), HoleLoc) / 100.0f :
			FVector::Dist2D(PlayerChar->GetActorLocation(), HoleLoc) / 20.0f;
		
		int32 gridDist = FMath::CeilToInt(distPH) + 5;

		if(IsDebugGrid)
		{
			if (gridDist > 100) gridDist = 100;	// 최대 거리 20m로 제한
		}
		else
		{
			if (gridDist > 20) gridDist = 20;	// 최대 거리 20m로 제한
		}

		CurrentVPointCount = distPH < 6 ? 11 : gridDist;
	}
	else
	{
		CurrentVPointCount = 11;
	}

	// 홀컵과 상관없이 카메라가 바라보는 방향으로 그리드 생성
	//FVector fixedDirection = (HoleLoc - PlayerChar->GetActorLocation()).GetSafeNormal();
	//
	//SetActorRotation(fixedDirection.Rotation());
	//const FVector verticalDir = FVector((PlayerChar->GetActorLocation() - HoleLoc).X, (PlayerChar->GetActorLocation() - HoleLoc).Y, 0.0f).GetSafeNormal();
	//const FVector horizonDir = verticalDir.RotateAngleAxis(-90.0f, FVector::ZAxisVector);

	FRotator ControlRotation = Controller->GetControlRotation();
	ControlRotation.Pitch = 0;

	SetActorRotation(ControlRotation);

	const FVector verticalDir = -ControlRotation.Vector();
	const FVector horizonDir = verticalDir.RotateAngleAxis(-90.0f + ShiftRotateAngle, FVector::ZAxisVector);

	UpdateGridPoints(PlayerChar, verticalDir, horizonDir);

	// 가로 그리드 업데이트
	const int32 HLine = MaxHGridCount - 1;
	for (int32 i = 0; i < DecalHPoints.Num(); ++i)
	{
		if (decalComps.IsValidIndex(i) && IsValid(decalComps[i]))
		{
			if(IsDebugGrid)
			{
				decalComps[i]->DecalSize = FVector(400.0f, GridThickness, 50.0f);
				decalComps[i]->SetWorldLocation(DecalHPoints[i]);

				decalComps[i]->SetHiddenInGame(false);
			}
			else
			{
				float addThickness = (MaxVGridCount - (i / HLine) - 1) * HorizontalSize; // 멀리 떨어진 가로 그리드 굵기 조절
				float FinalThickness = addThickness > 0 && GridThickness > 0.0f ? GridThickness + addThickness : GridThickness;
				
				decalComps[i]->DecalSize = FVector(400.0f, FinalThickness, 50.0f);
				decalComps[i]->SetWorldLocation(DecalHPoints[i]);
				decalComps[i]->SetRelativeRotation(HorizontalGridRelativeRot + FRotator(0, ShiftRotateAngle,0));
				
				// DrawDebugSphere(
				// 	GetWorld(),
				// 	DecalHPoints[i],
				// 	5,
				// 	24,
				// 	FColor::Red,
				// 	true
				// );
				
				decalComps[i]->SetHiddenInGame((MaxVGridCount - (i / HLine) - 1) > CurrentVPointCount);
			}
		}
	}
	// 세로 그리드 업데이트
	const int32 VLine = MaxVGridCount - 1;
	for (int32 i = 0; i < DecalVPoints.Num(); ++i)
	{
		if (decalComps.IsValidIndex(i + DecalHPoints.Num()) && IsValid(decalComps[i + DecalHPoints.Num()]))
		{
			if(IsDebugGrid)
			{
				decalComps[i + DecalHPoints.Num()]->DecalSize = FVector(400.0f, GridThickness, 50.0f);
				decalComps[i + DecalHPoints.Num()]->SetWorldLocation(DecalVPoints[i]);

				decalComps[i + DecalHPoints.Num()]->SetHiddenInGame(false);
			}
			else
			{
				float addThickness = (MaxVGridCount - (i % VLine) - 2) * VerticalSize; // 멀리 떨어진 세로 그리드 굵기 조절
				float FinalThickness = addThickness > 0 && GridThickness > 0.0f ? GridThickness + addThickness : GridThickness;
				
				decalComps[i + DecalHPoints.Num()]->DecalSize = FVector(400.0f, FinalThickness, 50.0f);
				decalComps[i + DecalHPoints.Num()]->SetWorldLocation(DecalVPoints[i]);

				decalComps[i + DecalHPoints.Num()]->SetHiddenInGame((MaxVGridCount - (i % VLine) - 1) > CurrentVPointCount);
			}
		}
	}

	//IsCreated = decalComps.Num() > 0;

	for (UDecalComponent* const gridDecal : decalComps)
	{
		if (IsValid(gridDecal))
		{
			gridDecal->SetFadeScreenSize(0.0f);
		}
	}

	// 그리드 가이드라인
	if (false == IsOnGreen && GridThickness > 0.0f)
	{
		bool    IsDetectObtacle = false;
		FVector ObstaclePos     = FVector::ZeroVector;

		for (int32 i = 0; i < DecalHPoints.Num(); ++i)
		{
			FVector TargetPos = DecalHPoints[i];

			FHitResult HitResult;

			bool FieldCheck = USGGroundChecker::LineTraceFieldCheck(HitResult, TargetPos + FVector(0, 0, 10), TargetPos - FVector(0, 0, 10), this);

			if (FieldCheck)
			{
				EPhysicalSurface CheckedSurface = UGameplayStatics::GetSurfaceType(HitResult);

				switch (CheckedSurface)
				{
					case SurfaceType14: // Tree
					case SurfaceType15: // Rock
					case SurfaceType16: // Metal
						{
							IsDetectObtacle = true;
							ObstaclePos     = HitResult.ImpactPoint;
						}
						break;
				}
			}

			if (IsDetectObtacle)
			{
				break;
			}
		}

		CourseMode->SetVisibleGridGuide(true, IsDetectObtacle, ObstaclePos);
	}
	// ~그리드 가이드라인
}

void ASGGrid::HideGrid()
{
	IsShow = false;

	ASGCourseMode* const CourseMode = GetWorld()->GetAuthGameMode<ASGCourseMode>();

	// 그리드 설정 강제 비활성화
	USGGameInstance* const GameInst = GAME_INSTANCE;
	if (IsValid(GameInst))
	{
		USGDataCenter* DataCenter = GameInst->GetDataCenter();
		if (IsValid(DataCenter))
		{
			if (DataCenter->GridOn)
			{
				DataCenter->GridOn = false;

				if (IsValid(CourseMode))
					CourseMode->ForceSetGridOn(false);
			}
		}
	}

	for (UDecalComponent* const decal : decalComps)
	{
		if (IsValid(decal))
		{
			decal->SetHiddenInGame(true);
		}
	}

	// 그리드 가이드라인
	if (IsValid(CourseMode))
	{
		CourseMode->SetVisibleGridGuide(false, false, FVector::ZeroVector);
	}
}

void ASGGrid::UpdateGridPoints(ASGPlayerCharacter* const InPlayer, const FVector& InVerticalDir, const FVector& InHorizonDir)
{
	TArray<FVector> points;

	for (int32 y = 0; y < MaxVGridCount; ++y)
	{
		const FVector p = IsDebugGrid ?
			InPlayer->GetActorLocation() - InVerticalDir * (ONE_METER / 4) * (MaxVGridCount - 2) + InVerticalDir * (ONE_METER / 4) * y :
			InPlayer->GetActorLocation() - InVerticalDir * ONE_METER * (MaxVGridCount - 2) + InVerticalDir * ONE_METER * y;
		
		for (int32 x = 0; x < MaxHGridCount; ++x)
		{
			FVector point = IsDebugGrid ?
				p - InHorizonDir * 500.0f + InHorizonDir * x * (ONE_METER / 4) :
				p - InHorizonDir * 500.0f + InHorizonDir * x * ONE_METER;

			// SG_LOG(Log, "SJW 222 point : %s", *point.ToString());
			
			if (SG_HOLE_CUP_DIAMETER >= FVector::Dist2D(HoleLoc, point))
			{
				point.Z = HoleLoc.Z;
				points.Add(point);
			}
			else
			{
				const FVector StartLoc = point + FVector(0.0f, 0.0f, 10000.0f);
				const FVector EndLoc = point - FVector(0.0f, 0.0f, 10000.0f);

				FHitResult HitResult;
				const bool groundCheck = USGGroundChecker::LineTraceGroundCheck(HitResult, StartLoc, EndLoc, InPlayer);
				if (true == groundCheck)
				{
					point.Z = HitResult.ImpactPoint.Z;
					points.Add(point);
				}
			}
		}
	}

	DecalHPoints.Empty();
	DecalVPoints.Empty();
	DecalHSpeed.Empty();
	DecalVSpeed.Empty();
	DecalHTime.Empty();
	DecalVTime.Empty();

	// 가로 그리드 좌표 설정
	for (int32 y = 0; y < MaxVGridCount; ++y)
	{
		for (int32 x = 0; x < MaxHGridCount - 1; ++x)
		{
			int32 currHIndex = MaxHGridCount * y + x;
			if (points.IsValidIndex(currHIndex) && points.IsValidIndex(currHIndex + 1))
			{
				FVector DecalHRes = IsDebugGrid ?
					points[currHIndex] + InHorizonDir * 20.0f :
					points[currHIndex] + InHorizonDir * 50.0f;
				
				DecalHPoints.Add(DecalHRes);

				//SG_LOG(Log, "SJW 222 DecalHPoints : %s", *DecalHRes.ToString());
				
				DecalHSpeed.Add((points[currHIndex].Z - points[currHIndex + 1].Z) * 0.1f);
				DecalHTime.Add(0);
			}
		}
	}
	// 세로 그리드 좌표 설정
	for (int32 x = 0; x < MaxHGridCount; ++x)
	{
		for (int32 y = 0; y < MaxVGridCount - 1; ++y)
		{
			int32 currIndex = MaxHGridCount * y + x;
			if (points.IsValidIndex(currIndex) && points.IsValidIndex(currIndex + MaxHGridCount))
			{
				FVector DecalVRes = IsDebugGrid ?
					points[currIndex] + InVerticalDir * 20.0f :
					points[currIndex] + InVerticalDir * 50.0f;
				
				DecalVPoints.Add(DecalVRes);

				//SG_LOG(Log, "SJW 222 DecalVPoints : %s", *DecalVRes.ToString());
				
				DecalVSpeed.Add((points[currIndex].Z - points[currIndex + MaxHGridCount].Z) * 0.1f);
				DecalVTime.Add(0);
			}
		}
	}
}

//void ASGGrid::UpdateGrid()
//{
//	if (IsShow == false)
//		return;
//
//	USGGameInstance* GameInst = GAME_INSTANCE;
//	IS_VALID_RETURN(GameInst);
//
//	APlayerController* Controller = Cast<APlayerController>(GameInst->GetFirstLocalPlayerController());
//	IS_VALID_RETURN(Controller);
//
//	ASGPlayerCharacter* PlayerChar = Cast<ASGPlayerCharacter>(Controller->GetPawn());
//	IS_VALID_RETURN(PlayerChar);
//
//	FRotator ControlRotation = Controller->GetControlRotation();
//	ControlRotation.Pitch = 0;
//	FVector ControlForward = ControlRotation.Vector();
//
//	SetActorRotation(ControlRotation);
//
//	FVector verticalDir = -ControlForward;
//	FVector horizonDir = verticalDir.RotateAngleAxis(-90.0f, FVector::ZAxisVector);
//
//	UpdateGridPoints(PlayerChar, verticalDir, horizonDir);
//
//	// 가로 그리드 그리기
//	for (int32 i = 0; i < DecalHPoints.Num(); ++i)
//	{
//		if (IsValid(material))
//		{
//			//UMaterialInstanceDynamic* materialDynamic = UKismetMaterialLibrary::CreateDynamicMaterialInstance(GetWorld(), material);
//			//if (IsValid(materialDynamic))
//			//{
//			//	materialDynamic->SetScalarParameterValue(TEXT("GridSpeed"), DecalHSpeed.IsValidIndex(i) ? DecalHSpeed[i] * AddSpeedRatio : 0.5f);
//
//				if (decalComps.IsValidIndex(i))
//				{
//					decalComps[i]->SetWorldLocation(DecalHPoints[i]);
//					decalComps[i]->SetRelativeRotation(FRotator(90.0f, 90.0f, 0.0f));
//					//decalComps[i]->SetDecalMaterial(materialDynamic);
//				}
//			//}
//		}
//	}
//
//	// 세로 그리드 그리기
//	for (int32 i = 0; i < DecalVPoints.Num(); ++i)
//	{
//		if (IsValid(material))
//		{
//			//UMaterialInstanceDynamic* materialDynamic = UKismetMaterialLibrary::CreateDynamicMaterialInstance(GetWorld(), material);
//			//if (IsValid(materialDynamic))
//			//{
//			//	materialDynamic->SetScalarParameterValue(TEXT("GridSpeed"), DecalVSpeed.IsValidIndex(i) ? DecalVSpeed[i] * AddSpeedRatio : 0.5f);
//
//				if (decalComps.IsValidIndex(i + DecalHPoints.Num()))
//				{
//					decalComps[i + DecalHPoints.Num()]->SetWorldLocation(DecalVPoints[i]);
//					decalComps[i + DecalHPoints.Num()]->SetRelativeRotation(FRotator(90.0f, 180.0f, 0.0f));
//					//decalComps[i + DecalHPoints.Num()]->SetDecalMaterial(materialDynamic);
//				}
//			//}
//		}
//	}
//}

void ASGGrid::ChangeGridThickness()
{
	if (IsShow == false)
		return;

	ChangeGridSize(true);
	ChangeGridSize(false);
}

void ASGGrid::ChangeGridSize(const bool bIsHorizontal)
{
	USGGameInstance* GameInst = GAME_INSTANCE;
	IS_VALID_RETURN(GameInst);

	float GridThickness = GameInst->GetDataCenter()->GetGridThicknessValue();
	int32 horizonLineNum = 10 * MaxVGridCount;

	// 가로 그리드 그리기
	if (bIsHorizontal)
	{
		int32 HLine = MaxHGridCount - 1;
		for (int32 i = 0; i < horizonLineNum; ++i)
		{
			if (decalComps.IsValidIndex(i))
			{
				float addThickness = (MaxVGridCount - (i / HLine) - 1) * HorizontalSize; // 멀리 떨어진 가로 그리드 굵기 조절
				float FinalThickness = addThickness > 0 && GridThickness > 0.0f ? GridThickness + addThickness : GridThickness;

				decalComps[i]->DecalSize = FVector(400.0f, FinalThickness, 50.0f);
				decalComps[i]->UpdateComponentToWorld();
			}
		}
	}
	// 세로 그리드 그리기
	else
	{
		int32 VLine = MaxVGridCount - 1;
		for (int32 i = horizonLineNum; i < decalComps.Num(); ++i)
		{
			if (decalComps.IsValidIndex(i))
			{
				float addThickness = (MaxVGridCount - ((i - horizonLineNum) % VLine) - 2) * VerticalSize; // 멀리 떨어진 세로 그리드 굵기 조절
				float FinalThickness = addThickness > 0 && GridThickness > 0.0f ? GridThickness + addThickness : GridThickness;

				decalComps[i]->DecalSize = FVector(400.0f, FinalThickness, 50.0f);
				decalComps[i]->UpdateComponentToWorld();
			}
		}
	}
}

void ASGGrid::CreateGrid()
{
	IS_VALID_RETURN(material);

	USGGameInstance* const GameInst = GAME_INSTANCE;
	IS_VALID_RETURN(GameInst);
	USGDataCenter* const DataCenter = GameInst->GetDataCenter();
	IS_VALID_RETURN(DataCenter);

	// 그리드 생성
	const float GridThickness = DataCenter->GetGridThicknessValue();

	int32 sortOrder = 0;

	// 가로 그리드 만들기
	const int32 HLine = MaxHGridCount - 1;

	for (int32 i = 0; i < (MaxHGridCount - 1) * MaxVGridCount; ++i)
	{
		UDecalComponent* const decal = NewObject<UDecalComponent>(this, UDecalComponent::StaticClass());
		UMaterialInstanceDynamic* const materialDynamic = UKismetMaterialLibrary::CreateDynamicMaterialInstance(GetWorld(), material);

		if (IsValid(decal) && IsValid(materialDynamic))
		{
			const float addThickness = (MaxVGridCount - (i / HLine) - 1) * HorizontalSize; // 멀리 떨어진 가로 그리드 굵기 조절
			float FinalThickness = addThickness > 0 && GridThickness > 0.0f ? GridThickness + addThickness : GridThickness;

			decal->RegisterComponentWithWorld(GetWorld());
			decal->AttachToComponent(sceneComp, FAttachmentTransformRules::KeepRelativeTransform);
			decal->SetRelativeRotation(HorizontalGridRelativeRot);
			decal->SetDecalMaterial(materialDynamic);
			decal->DecalSize = FVector(400.0f, FinalThickness, 50.0f);
			decal->SortOrder = sortOrder;

			decalComps.Add(decal);
			MIDs.Add(materialDynamic);
		}
		sortOrder++;
	}

	// 세로 그리드 만들기
	const int32 VLine = MaxHGridCount - 1;

	for (int32 i = 0; i < MaxHGridCount * (MaxVGridCount - 1); ++i)
	{
		UDecalComponent* const decal = NewObject<UDecalComponent>(this, UDecalComponent::StaticClass());
		UMaterialInstanceDynamic* const materialDynamic = UKismetMaterialLibrary::CreateDynamicMaterialInstance(GetWorld(), material);

		if (IsValid(decal) && IsValid(materialDynamic))
		{
			const float addThickness = (MaxVGridCount - (i % VLine) - 2) * VerticalSize; // 멀리 떨어진 세로 그리드 굵기 조절
			float FinalThickness = addThickness > 0 && GridThickness > 0.0f ? GridThickness + addThickness : GridThickness;

			decal->RegisterComponentWithWorld(GetWorld());
			decal->AttachToComponent(sceneComp, FAttachmentTransformRules::KeepRelativeTransform);
			decal->SetRelativeRotation(FRotator(90.0f, 180.0f, 0.0f));
			decal->SetDecalMaterial(materialDynamic);
			decal->DecalSize = FVector(400.0f, FinalThickness, 50.0f);
			decal->SortOrder = sortOrder;

			decalComps.Add(decal);
			MIDs.Add(materialDynamic);
		}
		sortOrder++;
	}
}

void ASGGrid::RemoveGrid()
{
	for (int32 i = 0; i < decalComps.Num(); ++i)
	{
		if (decalComps[i] && IsValid(decalComps[i]))
		{
			decalComps[i]->RemoveFromRoot();
			decalComps[i]->DestroyComponent();
		}
	}

	decalComps.Empty();
	MIDs.Empty();
}


void ASGGrid::ShowDebugGrid()
{
	RemoveGrid();
	
	MaxVGridCount = 60;
	MaxHGridCount = 41;

	IsDebugGrid = true;

	CreateGrid();
	
	ShowGrid();
}

void ASGGrid::HideDebugGrid()
{
	RemoveGrid();
	
	MaxVGridCount = 20;
	MaxHGridCount = 11;

	IsDebugGrid = false;
	
	CreateGrid();
	ShowGrid();
}

void ASGGrid::SetShiftRotateAngle(float mShiftRotateAngle, bool IsGridOn)
{
	ShiftRotateAngle = mShiftRotateAngle;

	if(IsGridOn)
	{
		HideGrid();
		ShowGrid();
	}
}

