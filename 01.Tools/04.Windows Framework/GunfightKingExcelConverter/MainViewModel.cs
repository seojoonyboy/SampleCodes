// ============================================================================
// MainViewModel 일부 샘플 코드
// ----------------------------------------------------------------------------
// (....중략....)
// ============================================================================

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CDBExportWPF.ViewModels
{
    class MainViewModel
    {
        /// <summary>
        /// "익스포트 실행" 버튼 클릭 핸들러.
        /// 체크된 엑셀 파일 목록(exportList)을 병렬로 순회하며, 파일 하나당
        /// ExportJob을 생성해 실제 변환 작업을 위임한다.
        /// </summary>
        private void OnExportBt(object obj)
        {
            // ... (중략: 선택 파일 수 체크, 세션 프로필 저장, 작업 디렉토리 설정,
            //      CancellationTokenSource/ParallelOptions 준비, exportList 수집 등) ...

            Task t = new Task(() =>
            {
                try
                {
                    // 엑셀 파일들을 병렬로 순회하며 각각 ExportJob에 변환을 위임한다.
                    // 파일 수가 많아도 CPU 코어 수만큼 동시에 처리해 전체 처리 시간을 줄인다.
                    Parallel.ForEach(exportList, opt, path =>
                    {
                        // 엑셀 파일 1개 = ExportJob 1개. 파일 간에 상태를 공유하지 않는
                        // 독립적인 작업 단위로 설계되어 있어 병렬 처리가 안전하다.
                        ExportJob job = new ExportJob();

                        string fileName = Path.GetFileName(path);
                        string dir = Path.GetDirectoryName(path);

                        if (job.ExportExcelFile(path))
                        {
                            // 성공: 출력된 파일 목록/경고를 리포트 목록에 반영
                            AddReport(job);
                            Interlocked.Increment(ref exportCounter);
                        }
                        else
                        {
                            // 실패: 에러 내용을 기록하고 나머지 작업을 취소
                            errorString = job.ErrorString;
                            errorFileName = fileName;
                            cts.Cancel();
                        }
                    });
                }
                catch (System.OperationCanceledException)
                {
                    System.Console.WriteLine("Job Cancelled.");
                }
                finally
                {
                    cts.Dispose();
                }
            });

            t.Start();

            // ... (중략: 나머지 UI 상태 갱신 로직) ...
        }
    }
}
