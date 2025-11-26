연습장에서 레벨 단위의 연습을 수행하는 경우, 레벨별 주어지는 탄알, 제한시간, 과녁의 움직이는 패턴 등을 PracticeMode 테이블과 연동하여 처리하였다.

아래는 PracticeMode 테이블 구조 일부이다.
<img width="1672" height="467" alt="image" src="https://github.com/user-attachments/assets/d40a9ae9-f985-4989-a205-8cbb630ac7cf" />


Mark1 : 고유 식별 번호
Mark1G : 과녁의 그룹 번호 [이 그룹이 모두 총알에 맞아야 다음 그룹으로 넘어간다.]
Mark1P : 과녁이 어떻게 움직일지 Pattern에 대한 참조키
