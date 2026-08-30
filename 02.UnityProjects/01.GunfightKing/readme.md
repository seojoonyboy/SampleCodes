GunfightKing Mobile FPS Game
==========================
> 모비릭스 모바일 FPS 클라이언트 개발   
> 개발 기간 : 2024.07 ~ 2025.11   
> 출시 여부 : Google PlayStore, Apple Store 정식 런칭

개발 환경
==========================
엔진 : Unity 3d Engine 6000.0.59f1   
플랫폼 : Android, iOS   
버전 관리 : Git, Github

프로젝트 소개
==========================
*총싸움의 왕 특징*
> 실시간 PVP 방식의 모바일 FPS 게임
> PVP 방에서 AI (Bot)이 섞여서 함께 플레이를 하기도 한다.

![image](https://github.com/user-attachments/assets/f62cd71f-5b4c-495b-9c65-bfd905dbfca1)

Sample Code
============================
*Bot AI 아키텍쳐*
> Bot AI 엄폐 지점(CoverPoint) 배치 및 관리 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/01.%20CoverPoint)   
> Bot AI 이동 경로(WayPoint) 그래프 및 팀/개인전 분기 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/02.%20WayPoint)   
> Bot AI 행동 패턴(FSM) 설계 - 탐색/공격/폭탄모드/회피 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/04.%20StateMachine)   
> Bot AI 전체를 관리하는 매니저 (문서 미작성, 코드 참고용) [폴더 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/07.%20MasterClientBotManaging)   

*전투/네트워크*
> 무기별 탄퍼짐 로직 설계 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/06.%20BulletSpread)   
> Photon PUN2 기반 Bot 위치/상태 압축 동기화 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/05.%20PhotonNetwork)   

*싱글플레이 콘텐츠*
> 연습모드 / 폭탄해체 트레이닝 설계 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/03.%20Practice)   
> UniTask 기반 튜토리얼 태스크 큐 설계 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/03.%20Tutorial)   

*라이브 서비스 안정화/보안*
> 해킹방지를 위한 코드 난독화 작업 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/08.%20Obfuscator)   
> Firebase Crashlytics를 연계한 라이브 서비스 모니터링 및 안정화 작업 [코드 샘플 링크](https://github.com/seojoonyboy/SampleCodes/tree/main/02.UnityProjects/01.GunfightKing/09.%20Firebase)   
