# StageMaker

3-Match 퍼즐 게임의 **스테이지(맵) 제작 툴**입니다. 9×9 Grid 위에 블록 이미지를 배치하고, 그 결과를 게임 클라이언트가 그대로 읽는 `Stages/*.json` 포맷으로 Export합니다.

- 기획자/QA가 코드를 몰라도 스테이지를 만들 수 있게 하는 **내부 개발 도구(Internal Tool)** 관점에서 설계했습니다.
- 개인 사이드 프로젝트로, WPF 커스텀 컨트롤 설계와 도메인 모델 ↔ 파일 포맷 분리에 초점을 맞췄습니다.

## 목차

- [주요 기능](#주요-기능)
- [개발 환경](#개발-환경)
- [아키텍처](#아키텍처)
- [설계 포인트](#설계-포인트)
- [Export 포맷](#export-포맷)
- [실행 방법](#실행-방법)
- [남은 과제](#남은-과제)

## 주요 기능

| 기능 | 설명 |
|---|---|
| 그리드 크기 조절 | Row/Col 값을 직접 입력해 N×M 그리드로 즉시 재구성 |
| 블록 배치 | 셀 클릭 → `Blocks/` 폴더의 모든 이미지를 썸네일 드롭다운으로 표시 → 클릭 한 번으로 배치 |
| Random Fill 팔레트 | 별도 팔레트에 후보 블록을 등록해두면, 빈 셀을 그 후보 중 무작위로 자동 채움 |
| Export | 현재 그리드 상태를 `Stages/*.json` 포맷 그대로 저장 (기존 스테이지 파일들과 바이트 단위 호환) |

## 개발 환경

| 항목 | 내용 |
|---|---|
| 언어/런타임 | C# / .NET 8.0 (`net8.0-windows`) |
| UI 프레임워크 | WPF (`UseWPF=true`), Code-First 커스텀 컨트롤 |
| 직렬화 | `System.Text.Json` + 커스텀 `JsonConverter` |
| 빌드 | [StageMaker.sln](StageMaker.sln) → [src/StageMaker/StageMaker.csproj](src/StageMaker/StageMaker.csproj) |

## 아키텍처

```
StarWay-StageMaker/
├─ Blocks/                 # 블록 이미지 리소스 (*.png) — 런타임에 스캔되는 데이터 소스
├─ Stages/                 # Export 결과물 (*.json)
├─ Template.png            # 9x9 Grid 레이아웃 참고 이미지
└─ src/StageMaker/
   ├─ Models/               # 순수 데이터 모델 (뷰/서비스 의존성 없음)
   │  ├─ BlockDefinition.cs      # 블록 리소스 1건 (record)
   │  └─ StageExportModel.cs     # Stages/*.json 스키마와 1:1 대응하는 DTO
   ├─ Services/             # 파일 I/O, 캐싱, 직렬화 등 도메인 로직
   │  ├─ ProjectPaths.cs          # 레포 루트(Blocks/Stages) 탐색
   │  ├─ BlockCatalog.cs          # Blocks/ 스캔 결과 캐싱
   │  ├─ StageExportService.cs    # 그리드 상태 → Export 모델 → JSON
   │  └─ CellExportJsonConverter.cs  # 레거시 포맷 호환용 커스텀 컨버터
   ├─ Controls/             # 재사용 가능한 커스텀 WPF 컨트롤
   │  ├─ BlockPickerCell.cs       # 드롭다운 선택 UI의 공통 동작 (abstract base)
   │  ├─ StageCell.cs             # 9x9 그리드의 셀
   │  └─ PoolSlot.cs              # Random Fill 팔레트 슬롯
   └─ MainWindow.xaml(.cs)   # 레이아웃 정의 + 이벤트 오케스트레이션 (Code-behind)
```

**데이터 흐름**

```mermaid
flowchart LR
    Blocks["Blocks/*.png"] --> Catalog["BlockCatalog<br/>(Lazy 캐시)"]
    Catalog --> Picker["BlockPickerCell<br/>드롭다운"]
    Picker -->|클릭 선택| Cell["StageCell / PoolSlot"]
    Cell -->|SelectedBlockChanged| Main["MainWindow<br/>_cells[row,col]"]
    Main -->|Export 클릭| Export["StageExportService"]
    Export --> Converter["CellExportJsonConverter"]
    Converter --> Json["Stages/*.json"]
```

레이어 책임을 명확히 나눴습니다.

- **Models**: WPF/파일 I/O를 전혀 모르는 순수 데이터. `StageExportModel`은 실제 게임이 읽는 JSON 스키마와 1:1로 매핑됩니다.
- **Services**: 정적 클래스 기반 도메인 로직. 상태를 갖지 않거나(`ProjectPaths`), 한 번 계산한 결과를 캐싱만 하는(`BlockCatalog`) 단순한 책임으로 제한했습니다.
- **Controls**: MainWindow와 완전히 독립적으로 동작하는 커스텀 컨트롤. `SelectedBlockChanged` 이벤트만으로 상위와 통신합니다.
- **MainWindow**: 위 세 레이어를 조립하고 사용자 입력을 라우팅하는 오케스트레이터. 도메인 로직을 직접 갖지 않도록 의식적으로 제한했습니다.

## 설계 포인트

### 1. 템플릿 메서드로 컨트롤 두 종류를 하나의 구현에 태우기

9×9 스테이지 셀(`StageCell`)과 Random Fill 팔레트 슬롯(`PoolSlot`)은 "클릭하면 블록 썸네일 드롭다운이 뜨고, 고르면 이미지가 박힌다"는 동작이 완전히 동일합니다. 다른 것은 셀이 자신을 어떻게 설명하는지(툴팁 텍스트) 뿐입니다. 이 공통 동작을 `abstract class BlockPickerCell : Border`로 뽑아내고, 달라지는 지점만 `protected virtual`로 열어 하위 클래스가 오버라이드하도록 했습니다.

```csharp
// Controls/BlockPickerCell.cs
public abstract class BlockPickerCell : Border
{
    public BlockDefinition? SelectedBlock { get; private set; }
    public event EventHandler? SelectedBlockChanged;

    // 드롭다운 구성, 썸네일 캐싱, 선택 적용 로직은 여기 한 곳에만 존재한다.
    protected virtual string DescribeSelection(BlockDefinition block)
        => $"{block.Id} ({block.FileName})";
    ...
}

// Controls/StageCell.cs — Row/Col 정보만 추가하고 툴팁 문구만 오버라이드
public sealed class StageCell : BlockPickerCell
{
    public int Row { get; }
    public int Col { get; }

    protected override string DescribeSelection(BlockDefinition block)
        => $"({Row}, {Col}) - {block.Id} ({block.FileName})";
}

// Controls/PoolSlot.cs — 크기/배경만 다르고 로직은 그대로 재사용
public sealed class PoolSlot : BlockPickerCell
{
    public PoolSlot(BlockDefinition? defaultBlock = null)
    {
        Width = Height = 56;
        if (defaultBlock is not null) SetInitialSelection(defaultBlock);
    }
}
```

새 종류의 "선택 가능한 셀"이 추가되어도 드롭다운/캐싱/이벤트 배관을 다시 만들 필요가 없습니다.

### 2. Lazy 캐싱으로 반복 파일 I/O 제거

블록 카탈로그(`Blocks/` 폴더 스캔)와 레포 루트 경로 탐색은 매 프레임 반복할 필요가 없는 연산입니다. `Lazy<T>`로 최초 1회만 계산하고 이후 모든 셀·슬롯이 같은 인스턴스를 참조하게 했습니다.

```csharp
// Services/BlockCatalog.cs
public static class BlockCatalog
{
    private static readonly Lazy<IReadOnlyList<BlockDefinition>> AllLazy = new(Load);
    public static IReadOnlyList<BlockDefinition> All => AllLazy.Value;
    ...
}
```

썸네일 비트맵 역시 `BlockPickerCell` 내부의 정적 `Dictionary<string, BitmapImage>`에 캐싱해, 같은 블록이 그리드 곳곳에서 선택될 때마다 디코딩을 반복하지 않도록 했습니다.

### 3. 도메인 모델(UI)과 Export DTO를 분리

`StageCell`이 들고 있는 `SelectedBlock`(런타임 UI 상태)과 `Stages/*.json`에 쓰이는 `CellExport`(파일 포맷)는 의도적으로 다른 타입입니다. UI 상태가 파일 포맷에 종속되면, 나중에 파일 스키마가 바뀔 때 컨트롤 코드까지 건드려야 하기 때문입니다. 변환은 `StageExportService` 한 곳에서만 일어납니다.

```csharp
// Services/StageExportService.cs
public static StageExportModel BuildExportModel(
    StageCell[,] cells, string stageName, IReadOnlyList<BlockDefinition> randomFillPool)
{
    ...
    for (int col = 0; col < colCount; col++)
    {
        var block = cells[row, col].SelectedBlock ?? PickRandomFillBlock(randomFillPool);
        rowCells.Add(block is null ? null : new CellExport { Block = new BlockExport { Type = block.Id } });
    }
    ...
}
```

### 4. 커스텀 `JsonConverter`로 레거시 포맷 무결성 유지

기존에 게임 클라이언트가 쓰던 `Stages/*.json`은 `cells` 배열의 각 원소가 한 줄로 압축된 JSON(`{"block":{"type":"101"}}`)인 반면, 문서 전체는 들여쓰기가 적용돼 있는 혼합 포맷입니다. `System.Text.Json`의 기본 직렬화는 문서 전체를 균일하게 들여쓰거나 균일하게 압축할 수밖에 없어서, `CellExport` 하나만 예외적으로 컴팩트하게 쓰는 `JsonConverter<CellExport>`를 직접 구현해 끼워 넣었습니다. 새 도구가 만든 파일이 기존 파일들과 diff 노이즈 없이 섞일 수 있도록 하기 위한 선택입니다.

```csharp
// Services/CellExportJsonConverter.cs
public override void Write(Utf8JsonWriter writer, CellExport value, JsonSerializerOptions options)
{
    var json = JsonSerializer.Serialize(value, CompactOptions);

    // WriteRawValue는 자동 들여쓰기에 관여하지 않으므로, 줄바꿈+들여쓰기를 직접 맞춰준다.
    if (writer.Options.Indented)
    {
        var indent = new string(' ', 2 * writer.CurrentDepth);
        json = "\n" + indent + json;
    }
    writer.WriteRawValue(json, skipInputValidation: true);
}
```

### 5. 이벤트 기반 통신으로 컨트롤 ↔ 윈도우 결합도 낮추기

`BlockPickerCell`은 `MainWindow`의 존재를 전혀 모릅니다. 선택이 바뀌면 `SelectedBlockChanged` 이벤트만 올리고, `MainWindow`가 필요한 셀에만 구독해 상태바를 갱신합니다. 컨트롤을 다른 창/도구에 재사용하더라도 코드 변경이 필요 없습니다.

```csharp
// MainWindow.xaml.cs
var cell = new StageCell(row, col);
cell.SelectedBlockChanged += Cell_SelectedBlockChanged;
```

## Export 포맷

`Stages/*.json`은 실제 게임 클라이언트가 로드하는 포맷이며, `Header`/`Cells`만 이 툴에서 실질적으로 채워지고 나머지(`Clears`/`Components`/`Genesises`/`Zones`)는 기존 파일과 구조를 맞추기 위한 자리표시자로 남겨뒀습니다([StageExportModel.cs](src/StageMaker/Models/StageExportModel.cs) 주석 참고).

```json
{
  "header": { "name": "normal_1", "rowCount": 4, "colCount": 4, "...": "..." },
  "clears": [], "components": {}, "genesises": [], "zones": [],
  "cells": [
    [{"block":{"type":"101"}}, {"block":{"type":"104"}}]
  ]
}
```

## 실행 방법

```powershell
dotnet build StageMaker.sln
dotnet run --project src/StageMaker/StageMaker.csproj
```

레포 루트의 `Blocks/`, `Stages/` 폴더를 자동으로 찾아 사용합니다([ProjectPaths.cs](src/StageMaker/Services/ProjectPaths.cs)) — 빌드 출력 위치와 무관하게 항상 같은 폴더를 읽고 씁니다.

## 남은 과제

- `clears` / `components` / `genesises` / `zones` 섹션을 UI에서 편집 가능하게 확장
- 블록 `Id`(이미지 파일명)를 실제 게임 타입 코드로 매핑하는 테이블 도입
- 기존 스테이지 파일 Import(현재는 Export만 지원)
