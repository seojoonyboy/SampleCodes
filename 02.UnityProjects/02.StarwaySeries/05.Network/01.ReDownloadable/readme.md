리다운로드 가능한 리소스 다운로드 — 폴더 안내
==============================================
> 앱 실행 직후(Title 화면) 서버에서 **테이블 데이터(Asset)와 UI/스테이지 리소스(Resource)** 를 내려받는 구간의 코드다.
> 모바일에서는 와이파이 ↔ 셀룰러 전환, 지하철 구간 진입 등으로 다운로드가 언제든 끊기므로, 이 구간은 "실패를 어떻게 감지하고 어디까지 되돌아가 다시 받을 것인가"가 핵심이다.

| 파일 | 역할 |
|------|------|
| [`TitleScene.cs`](./TitleScene.cs) | 타이틀 상태 머신(`STATE`), 리소스 zip 스트리밍 다운로드, 정체 감시 코루틴(`CheckPosition`) |
| [`NetworkManager.cs`](./NetworkManager.cs) | 테이블 파일 단위 diff 다운로드(`TestAssetLoader`)와 재시도 상한, 부팅 게이트 체인 (이 폴더는 초기 버전이고, 게이트 체인 최신본은 [02. TitleSequence](../02.%20TitleSequence) 폴더의 `NetworkManager.cs`) |

* 다운로드 파이프라인의 단계별 설명, 재시도 전략, 한계: **[AdditionalResourceDownload.md](../../AdditionalResourceDownload.md)**
* 부팅 게이트 체인(버전 체크 → 강제 업데이트 → 공지 → 약관): **[02. TitleSequence](../02.%20TitleSequence/readme.md)**
* HTTP 통신 실패의 공통 처리와 서버 Push 디스패치: **[03.ApiException](../03.ApiException/readme.md)**
