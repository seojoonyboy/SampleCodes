STARWAY 3Match Puzzle Game
==========================
> 스노우볼스 3Match 퍼즐 클라이언트 개발   
> 개발 기간 : 2022.05 ~ 2024.01   
> 출시 여부 : Google PlayStore, Apple Store 정식 런칭 [24.05 서비스 종료 상태]   

![STARWAY](https://github.com/user-attachments/assets/070fc810-cd7a-4f4b-8b77-510fc06b2e5b)


개발 환경
==========================
엔진 : Unity 3d Engine 2021.3.23f1   
플랫폼 : Android, iOS   
버전 관리 : Git, Github

프로젝트 소개
==========================
*스타웨이 특징*   
> 아티스트 포토 카드 수집 요소를 포함한 IP 기반 3Match Puzzle Game.   
> 카드마다 고유 카드 스킬을 갖고 있어, 3Match 퍼즐 내에서 활용할 수 있는 특징을 갖고 있다.   

![image](https://github.com/user-attachments/assets/57f91be9-7342-4f8c-9eec-ed82ead10fec)   

*시즌별 랭킹 경쟁*   
> 스타웨이 ATEEZ, 권은비, iKON의 경우, 랭킹 시스템이 추가됨
> 스코어 모드라는 신규 모드가 추가 되어 시즌마다 진행
> 다른 이용자와 점수 경쟁을 하여, 시즌 종료시 상위 랭커 보상 수령

*유저 재접속율을 높이기 위한 추가 이벤트 시스템*     
> 일일 출석, 일일 이벤트 미션, 주간 이벤트 미션, 패스 시스템   

*다양한 플레이 모드*   
> 스타웨이 iKON에서 리듬게임 신규 추가   

총 6개의 STARWAY 시리즈를 런칭. (김호중, 장민호, 강다니엘, 에이티즈, 권은비, 아이콘)

리뷰 가이드 (10분 안에 보려면)
============================
> 코드가 많은 저장소라서, 이력서에 적은 항목별로 **어디를 보면 되는지 / 무엇이 코드와 커밋으로 확인되는지**를 먼저 정리한다.
> "수치"는 운영 지표(CS 접수 집계, 실기기 측정)에서 나온 값이라 코드만으로는 검증되지 않고, 코드로 확인되는 것은 구조와 커밋 이력이다.

| 이력서 항목 | 볼 문서 | 코드/커밋으로 확인되는 것 |
|-------------|---------|---------------------------|
| 통신 계층 안정화 — 타임아웃·재시도 튜닝, `isRetry` 확장, Invoke 디스패치 수정 | [05.Network/03.ApiException](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/05.Network/03.ApiException/readme.md) | `7f52e7a40`(기본값 파라미터로 기존 호출부 유지), `40b738302`(CP 분기), 2023-02-22/23 재시도·타임아웃 4연속 조정 |
| IAP 결제에 Ack(3-way handshake 착안) 도입 | [IAPProcess.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/IAPProcess.md) | `Pending` 유지 → 서버 확인 후 `ConfirmPendingPurchase` → `Ack`, 로비 `AckCheck` 복구 (`e581a7ddd`) |
| 오브젝트 풀링 · Coroutine → UniTask 전환 | [100.Docs/01.최적화](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/100.Docs/01.%EC%B5%9C%EC%A0%81%ED%99%94/readme.md) | 2023-07-18 하루 3연속 커밋: 구축(+409줄) → 반납 버그 수정 → 특수블록 풀링 되돌림 |
| Android 저지연 오디오(Oboe) | [08.AudioController](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/08.AudioController/readme.md) | `OboeAudioSource(48000, LowLatency, Shared, AAudio ...)`, 시트 기반 재생기 사전 캐시, `#if` 플랫폼 분기 |
| 부팅 시퀀스 · 리소스 다운로드 네트워크 대응 | [02. TitleSequence](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/05.Network/02.%20TitleSequence/readme.md) · [AdditionalResourceDownload.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/AdditionalResourceDownload.md) | 다단계 게이트 코루틴 체인, 진행 정체 감시(`CheckPosition`), 재시도 상한 |
| 팝업 아키텍처(Template Method + Factory) | [PopupUIPattern.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/PopupUIPattern.md) | `Popup.Load` 단일 진입점, `OnResultCallback` 결과 전달 |
| 코드베이스 설계 구조 이해와 확장 | [100.Docs/02.설계패턴](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/100.Docs/02.%EC%84%A4%EA%B3%84%ED%8C%A8%ED%84%B4/readme.md) | View 이벤트 계층, `BroadcastTunnel<K,V>`, 제네릭 FSM, Factory/Memento/전략 배열 (**직접 설계 vs 확장 vs 이해를 구분해서 표기**) |
| 이벤트 시스템 — 패스 · 일일 출석 · 하트 자동 충전 | [01.Pass](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/01.Pass) · [02.Attendance](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/02.Attendance) · [03.HeartAutoCharge](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/03.HeartAutoCharge) | 서버가 보상을 결정하고 클라이언트는 응답 후 반영(Ack 프로토콜), 서버 시각 기준 카운트다운, 요청 지연을 연출 뒤에 숨기는 구조, 백그라운드 복귀 보정 |
| 퍼즐 코어 — 힌트 · 스테이지 JSON · 스킬/미션 블록 | [07.BlockControl](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/07.BlockControl) · [CardSkillBlockLogic.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/CardSkillBlockLogic.md) · [BlockMatchLogic.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/BlockMatchLogic.md) | 스테이지를 데이터로 정의, 복사본에서 백그라운드 힌트 계산, 스킬 블록 배치 골격 (**"구조 이해" 범주**) |
| 데이터 시트 기반 튜토리얼 | [TutorialPattern.md](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/TutorialPattern.md) | 시트 한 행 = 한 스텝, UniTask + LINQ 로 순차 소비 |
| 협업 — Git 브랜치 전략 / Submodule | [06.GitBranchStrategy](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/06.GitBranchStrategy) | 6개 앱 공통 코드 분리 관리 |

*이 저장소의 문서 구성 방식*
> 각 문서는 **문제 → 접근 → 코드(실제 프로젝트에서 발췌) → 근거(커밋) → 한계와 개선 방향** 순서로 쓴다. 실서비스 코드라서 남아있는 아쉬운 부분도 숨기지 않고
> "한계와 개선 방향"에 적었고, 코어 퍼즐 알고리즘처럼 직접 설계하지 않은 부분은 "구조 이해"로 구분했다.

Sample Code
============================
*네트워크 처리 관련*
> 교차 검증을 통한 Server-Client 인앱 구매 처리 (Pending 유지 · Ack · 미수령 보상 로비 복구) [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/IAPProcess.md)   
> 추가 리소스 다운로드와 네트워크 불안정 환경 대응 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/AdditionalResourceDownload.md)   
> 통신 계층 예외 처리(3층 분류)와 서버 Push(Invoke) 디스패치, `isRetry` 확장 [코드 샘플 링크](<https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/05.Network/03.ApiException>)   
> 앱 최초 실행시 버전체크/공지/리소스서버 확인 등 부팅 시퀀스 설계 [코드 샘플 링크](<https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/05.Network/02. TitleSequence>)   

*퍼즐 로직 구현*
> 퍼즐 스테이지 최초 초기화 과정 설명 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/PuzzleInit.md)   
> 미션 블록 처리과정 설명 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/BlockMatchLogic.md)   
> 스킬 블록 구현 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/CardSkillBlockLogic.md)   
> 힌트 계산·막힌 판 재배치, 스테이지 JSON 데이터 구조 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/07.BlockControl)   
> 오브젝트 풀링 및 Coroutine → UniTask 전환을 통한 인게임 최적화 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/100.Docs/01.%EC%B5%9C%EC%A0%81%ED%99%94/readme.md)   

*이벤트 · 라이브 서비스 화면*
> 시즌 패스 (보상 트랙 UI, 서버 시각 카운트다운, 모두 받기, Ack) [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/01.Pass)   
> 일일 출석 (카드 뒤집기 연출과 서버 요청 병행, 자정 날짜 변경 처리) [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/02.Attendance)   
> 하트 자동 충전 (서버 기준 시각 계산, 백그라운드 복귀 보정) [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/03.HeartAutoCharge)   

*플랫폼/오디오*
> Android 저지연 오디오(Oboe/AAudio) 연동 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/08.AudioController)   

*패턴 설계*
> MVC 패턴을 활용한 팝업 UI 설계 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/PopupUIPattern.md)   
> 코드베이스 설계 구조(View 이벤트 계층, 제네릭 이벤트 버스/FSM, Factory·Memento·전략 배열)와 내가 맡은 범위 [문서 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/100.Docs/02.%EC%84%A4%EA%B3%84%ED%8C%A8%ED%84%B4)   
> 기획자 편의를 제공한 UniTask, Linq 기반 데이터 시트 튜토리얼 시스템 설계 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/02.StarwaySeries/TutorialPattern.md)   

*협업/버전 관리*
> Git 브랜치 전략 및 서브모듈 전환 배경 [문서 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/02.StarwaySeries/06.GitBranchStrategy)   

***
