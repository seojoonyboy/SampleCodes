#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "SGGrid.generated.h"

UCLASS()
class SG_API ASGGrid : public AActor
{
	GENERATED_BODY()

public:
	ASGGrid();
	void BeginPlay() override;
	void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
	void Tick(float DeltaTime) override;

	void ShowGrid();
	void HideGrid();

	void ShowDebugGrid();
	void HideDebugGrid();
	
	//void UpdateGrid();

	UFUNCTION() void ChangeGridThickness();
	void ChangeGridSize(const bool bIsHorizontal);
	void SetSpeedRatio(const float newRatio) { AddSpeedRatio = newRatio; }
	void SetHorizontalSize(const float size) { HorizontalSize = size; }
	void SetVerticalSize(const float size) { VerticalSize = size; }

	void SetShiftRotateAngle(float ShiftRotateAngle, bool IsGridOn);
	
private:
	void CreateGrid();
	void RemoveGrid();

	void UpdateGridPoints(class ASGPlayerCharacter* const InPlayer, const FVector& InVerticalDir, const FVector& InHorizonDir);

public:
	UPROPERTY(VisibleAnywhere) class UMaterialInterface* material = nullptr;
	UPROPERTY(VisibleAnywhere) class USceneComponent* sceneComp = nullptr;
	UPROPERTY(VisibleAnywhere) TArray<class UDecalComponent*> decalComps;
	UPROPERTY(VisibleAnywhere) TArray<class UMaterialInstanceDynamic*> MIDs;

private:
	bool IsShow = false;

	UPROPERTY(VisibleAnywhere) TArray<FVector> DecalHPoints;
	UPROPERTY(VisibleAnywhere) TArray<FVector> DecalVPoints;
	UPROPERTY(VisibleAnywhere) TArray<float>   DecalHSpeed;
	UPROPERTY(VisibleAnywhere) TArray<float>   DecalVSpeed;
	UPROPERTY(VisibleAnywhere) TArray<float>   DecalHTime;
	UPROPERTY(VisibleAnywhere) TArray<float>   DecalVTime;
	int32 CurrentVPointCount = 0;

	int32 MaxVGridCount = 20;
	int32 MaxHGridCount = 11;

	bool IsDebugGrid = false;

	FVector HoleLoc = FVector::ZeroVector;
	float AddSpeedRatio = 0.5f;

	float HorizontalSize = 0.5f;
	float VerticalSize = 0;

	FRotator HorizontalGridRelativeRot = FRotator(90.0f, 90.0f, 0.0f);
	float ShiftRotateAngle = 0;
};
