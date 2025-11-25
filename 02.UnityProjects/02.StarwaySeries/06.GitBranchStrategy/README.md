## 설명
  - SWKHJ Branch가 Main Branch로 계속 합쳐지고 있다. 때문에 이후 파생 프로젝트(예: SWKD) Branch는 Main Branch로 Merge가 불가합니다. 
    더 자세히 설명하면, 파생 Branch의 디자인 리소스, 폰트 등이 Main Branch에 덮어씌워지기 때문 (이 부분에 대해서는 서브모듈로 전환을 검토중이고, 그 이전까지는...)

  - 마찬가지 이유로 SWKHJ <--- SWKD Merge는 문제가 발생할 수 있습니다. KHJ에 SWKD의 디자인 리소스가 덮어씌워지니까...
  
  - 결론적으로, 서브 모듈화 적용 이전까지는 다음의 규칙을 지켜야 합니다.
  
  1. SWKD는 SWKHJ의 내용을 가져오는 것은 허용
  2. SWKHJ의 Release 브랜치를 Main Branch로 Merge는 허용
  3. SWKD의 내용을 SWKHJ 혹은 Main Branch로 Merge는 금지
  
  ![git_graph2](https://user-images.githubusercontent.com/110382516/187827024-ebe892ee-dfd5-4e42-a49e-a00abcbd38c4.PNG)
  
## 환경

  - Unity 2021.2.16f1 LTS
  - SVN 버전 관리 -> Git 이전
  - (Origin SVN Repository : svn://snowballsnas.local/SB_ArtistarPuzzle)

## 버전 관리
  - master 브랜치
    - develop
    - release
    - feature
    - hotfix

  - **feature, hotfix -> develop -> release** 순으로 병합됩니다.
  - 예를 들어, 최초 작업은 **feature** 혹은 **hotfix** 브랜치를 새로 만들어 진행하고, 개발자들간의 공유 수준까지 올라온 뒤, **develop** 브랜치에 병합합니다.
  - 병합 이후 이슈가 발생한 경우 새롭게 **feature** 브랜치를 만들어 해당 이슈를 대응합니다. 대응 이후 다시 **develop** 브랜치에 병합 (반복)
  - 어느정도 안정성이 확보된 이후 **develop** 브랜치를 다시 **release** 브랜치에 병합하여 빌드를 공유합니다.
  - main 브랜치는 release 브랜치를 주기적으로 병합합니다. (안정성 최우선)

![git_graph](https://user-images.githubusercontent.com/110382516/182572212-a39c47f8-a690-4514-9c4e-d98dc8c8238c.PNG)
  
  - 주요 브랜치 2개는 항상 유지됩니다.
    - master (main)
    - develop
      + develop/버전
      + 예) develop/1.0.1.4 데브 빌드 1.0.1 의 4번째 빌드

  - 보조 브랜치
  - feature, hotfix의 경우
    - feature : 새로운 기능을 추가할 때
      + feature/주제
      + 예) feature/addPassEvent 패스이벤트 시스템을 추가하는 feature 브랜치
    - release : 배포용 빌드 (안정성 검증이 어느정도 끝난 브랜치)
      + release/버전
      + 예) release/1.0.2 릴리즈용 빌드 1.0.2
    - hotfix : 긴급 수정 (버그 수정)
      + /세부 내용
      + 예) hotfix/issue03 깃헙 이슈 3번에 대한 긴급 수정

## 패턴
MVP 패턴

  - Model (GameStorage.cs)
  - View ({...}View.cs)
  - Presenter ({...}Controller.cs)

Observer 패턴

## 코드 컨벤션

  - 들여쓰기 : space와 tab을 섞어서 사용하지 않는다. (tap = space * 4)
  - 명명 규칙 : 카멜 케이스
  - 전역 변수 : 앞에 this를 붙인다. (예 : this.mainTexture)
