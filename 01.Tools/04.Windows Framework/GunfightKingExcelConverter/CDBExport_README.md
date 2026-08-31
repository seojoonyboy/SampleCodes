# CDBExport — Excel → JSON 게임 데이터 익스포트 툴 (WPF)

기획자가 작성한 Excel 데이터 시트를 클라이언트/서버가 바로 사용할 수 있는 JSON(및 CSV, Bin) 파일로 자동 변환해주는 Windows 데스크톱 툴입니다. .NET / WPF 기반으로 직접 설계·개발했으며, 반복되는 수작업 데이터 변환 업무를 자동화해 기획자가 코드 수정 없이 데이터를 즉시 게임에 반영할 수 있도록 하는 것이 목적입니다.

## 왜 필요했는가

게임 데이터(무기 스펙, AI 파라미터, 아이템 테이블 등)는 기획자가 Excel로 관리하지만, 실제 클라이언트/서버 코드는 JSON을 읽습니다. 이 변환을 매번 수작업으로 하면 시간이 오래 걸리고 사람이 직접 옮기는 과정에서 오타·누락 같은 실수가 생기기 쉽습니다. CDBExport는 Excel 파일 안에 변환 규칙(`[Export]` 탭)을 함께 정의해두고, 툴에서 파일만 선택해 실행하면 규칙에 따라 자동으로 JSON을 뽑아내도록 설계했습니다.

## 처리 흐름

### 1) WPF 화면에서 대상 Excel 파일을 선택하고 실행

여러 시트 파일을 체크박스로 선택한 뒤 실행하면, 파일 단위로 병렬 처리됩니다.

```csharp
// MainViewModel.cs — OnExportBt
Parallel.ForEach(exportList, opt, path =>
{
    ExportJob job = new ExportJob();
    string fileName = Path.GetFileName(path);

    if (job.ExportExcelFile(path))
    {
        AddReport(job);
        Interlocked.Increment(ref exportCounter);
    }
    else
    {
        errorString = job.ErrorString;
        errorFileName = fileName;
        cts.Cancel();
    }
});
```

### 2) 엑셀 파일 하나당 정해진 순서로 파이프라인을 수행

`ExportJob`이 파일 하나를 열어서 설정 읽기 → 상수사전 로드 → 필드 준비 → 실제 출력까지 순서대로 처리합니다.

```csharp
// ExportJob.cs — ExportExcelFile
bRet = OpenExcelFile(strPath);

if (bRet) bRet = CollectExportInfo();   // [Export] 탭에서 설정 영역을 찾아 Json/Csv/Bin Writer 생성
if (bRet) bRet = ReadConstDics();       // [Enum] 탭에서 상수사전(문자열 → 정수 ID)을 로드
if (bRet) bRet = PrepareFieldDefs();    // 필드별로 상수사전을 연결
if (bRet) bRet = ExportForAllTargets(); // 레코드를 순회하며 실제 파일 출력
```

`CollectExportInfo` 단계에서는 `[Export]` 탭에 정의된 설정 이름과 출력 형식(Json/Csv/Bin)에 따라 해당하는 Writer를 만들어둡니다.

```csharp
// ExportJob.cs — CollectExportInfo
if (config.JsonOutputFileName != string.Empty)
{
    FilePacker writer = null;

    if (config.JsonOutputType == JsonOutputType.Normal)
        writer = new JsonFilePacker(configNames[iConfig], config, config.JsonOutputFileName);
    else if (config.JsonOutputType == JsonOutputType.UniformedJsonObject)
        writer = new Json2FilePacker(configNames[iConfig], config, config.JsonOutputFileName);

    writer.FileName = config.JsonOutputFileName;
    target.DataWriters.Add(writer);
}
```

### 3) 셀 하나하나를 필드 타입에 맞게 변환

각 필드는 Excel 설정 탭에서 문자 하나로 타입을 지정합니다.

| 문자 | 의미 |
|---|---|
| `T` | 텍스트 (UTF-8 문자열) |
| `I` | 정수 |
| `R` | 실수 |
| `a` / `A` | 실수를 100배 / 10000배한 정수로 저장(고정소수점) |
| `H` | 문자열을 CRC32 해시값으로 변환 |
| `X` | 16진수 |
| `j` / `J` | JSON 배열 / JSON 오브젝트 |
| `D` | 날짜/시간 |

이 타입 문자에 따라 셀 값을 공용 구조체 `CellData`로 변환합니다.

```csharp
// CellDataUtil.cs — ExtractCellData
switch (fieldType)
{
    case FieldTypeChars.Int:
        SetCellDataValue(cellObj, fieldDef, ref cellData);
        break;

    case FieldTypeChars.Hash:
        SetCellDataValueCustom(cellObj, fieldDef, ref cellData, (text) => Util.CRC32(text));
        break;

    case FieldTypeChars.Text:
    case FieldTypeChars.JsonArray:
    case FieldTypeChars.JsonObject:
        cellData.TextVal = cellObj.GetSafeText();
        cellData.TextVal = fixText(fieldType, cellData.TextVal, fieldDef.CheckJson, config.ReplacePipeToNewLine, out cellData.IsJson);
        cellData.BinType = CellDataBinType.Text;
        break;

    // ... Real, Amp100/Amp10000, DateTime 등 나머지 타입 처리
}
```

### 4) 레코드 단위로 JSON 문자열을 조립해 파일로 저장

`CellData`로 변환된 값은 `JsonFilePacker`가 필드 이름과 함께 JSON 문자열로 조립하고, 레코드 처리가 끝나면 UTF-8 BOM을 붙여 파일로 씁니다.

```csharp
// JsonFilePacker.cs — WriteFieldValue
public override void WriteFieldValue(FieldDef fieldDef, ref CellData cellData)
{
    string lineFeed = _LineFeeds[(fieldDef == Config.FieldDefs.Last()) ? 0 : 1];

    switch (cellData.BinType)
    {
        case CellDataBinType.Text:
            string s = TextUtil.TryReplaceLineFeedSymbols(cellData.TextVal, Config.ReplacePipeToNewLine);
            if (TextUtil.IsExpectedJson(s))
                s = TextUtil.JsonStringify(s);
            _builder.Append($"\t\"{fieldDef.FinalName}\":\"{s}\"{lineFeed}");
            break;

        case CellDataBinType.Int32:
            _builder.Append($"\t\"{fieldDef.FinalName}\":{cellData.Int32Val}{lineFeed}");
            break;

        // ... Int8/16/64, Real32/64 등 나머지 타입도 동일한 방식으로 처리
    }
}
```

## 설계 포인트

- **출력 포맷과 무관한 공용 변환 구조**: 셀 값을 먼저 `CellData`라는 중간 구조체로 변환해두고, 이후 JSON/CSV/Bin 세 종류의 `FilePacker`가 이 구조체를 각자의 방식으로 소비합니다. 덕분에 "셀을 어떻게 읽어서 타입 변환할 것인가"와 "결과를 어떤 포맷으로 쓸 것인가"를 분리해, 새 출력 포맷이 필요해도 셀 읽기 로직을 건드리지 않아도 됩니다.
- **Enum 탭 기반 상수사전**: 기획자가 셀에 정수 ID 대신 사람이 읽기 쉬운 문자열(예: 무기 종류 이름)을 입력해도, `[Enum]` 탭에 정의된 상수사전을 통해 자동으로 정수 ID로 매핑되도록 했습니다. 기획자 입장에서는 ID 체계를 몰라도 데이터를 채울 수 있습니다.
- **ID 중복 자동 검증**: 레코드를 출력하기 전에 ID(정수 또는 해시값) 중복 여부를 검사해, 중복 시 어느 엑셀 행과 충돌했는지까지 에러 메시지로 알려줍니다. 데이터 오류를 빌드 이후가 아니라 변환 시점에 바로 잡아낼 수 있습니다.

## 관련

이 툴은 실제 서비스 중인 모바일 FPS 프로젝트에서 무기 스펙, AI 파라미터 등 기획 데이터를 코드 수정 없이 반영하기 위한 용도로 사용되었습니다.
