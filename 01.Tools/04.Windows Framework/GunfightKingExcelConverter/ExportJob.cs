// ============================================================================
// ExportJob 일부 샘플 코드
// ----------------------------------------------------------------------------
// (....중략....)
// ============================================================================

using System;
using System.Collections.Generic;

namespace CDBExport
{
    public class ExportJob
    {
        /// <summary>
        /// 엑셀 파일 하나를 열어서 JSON/CSV/Bin으로 변환하는 전체 파이프라인.
        /// 각 단계는 이전 단계가 성공했을 때만 실행되며, 하나라도 실패하면
        /// 이후 단계를 진행하지 않고 중단한다.
        /// </summary>
        public bool ExportExcelFile(string strPath, List<string> targetConfigs = null, string overrideDestDir = null)
        {
            // ... (중략: OnBegin 초기화, try-catch 예외 처리, OnEnd 정리 등) ...

            bRet = OpenExcelFile(strPath);

            if (bRet) bRet = CollectExportInfo();   // [Export] 탭에서 설정 영역을 찾아 Json/Csv/Bin Writer 생성
            if (bRet) bRet = ReadConstDics();       // [Enum] 탭에서 상수사전(문자열 → 정수 ID)을 로드
            if (bRet) bRet = PrepareFieldDefs();    // 필드별로 상수사전을 연결
            if (bRet) bRet = ExportForAllTargets(); // 레코드를 순회하며 실제 파일 출력

            // ... (중략: 결과 반환, 에러 처리) ...
        }

        /// <summary>
        /// [Export] 탭에 정의된 설정 영역들을 찾아 순회하며, 각 설정에 지정된
        /// 출력 형식(Json/Csv/Bin)에 맞는 Writer(FilePacker)를 생성
        /// </summary>
        bool CollectExportInfo()
        {
            // ... (중략: [Export] 탭 시트 확인, 설정 영역 검색, ExportConfig 빌드,
            //      ExportSource 중복 체크 등) ...

            if (config.JsonOutputFileName != string.Empty)
            {
                FilePacker writer = null;

                // 설정된 JsonOutputType에 따라 서로 다른 Writer 구현체를 선택한다.
                if (config.JsonOutputType == JsonOutputType.Normal)
                    writer = new JsonFilePacker(configNames[iConfig], config, config.JsonOutputFileName);
                else if (config.JsonOutputType == JsonOutputType.UniformedJsonObject)
                    writer = new Json2FilePacker(configNames[iConfig], config, config.JsonOutputFileName);

                writer.FileName = config.JsonOutputFileName;
                target.DataWriters.Add(writer);
            }

            // ... (중략: Csv/Bin Writer 생성, 나머지 설정 영역 순회) ...
        }
    }
}
