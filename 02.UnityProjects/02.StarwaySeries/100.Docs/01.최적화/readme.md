### 코드 최적화
Coroutine을 UniTask로 전환하여 메모리 부하를 줄이는 효과를 얻을 수 있었음 [관련 코드 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/01.StarwaySeries/07.BlockControl/StageController.cs)   
Object Pooling을 활용한 블록 제거와 추가 로직 구현 [관련 코드 링크](https://github.com/seojoonyboy/SampleCodes/blob/main/02.UnityProjects/01.StarwaySeries/07.BlockControl/IngameBlockPoolController.cs)   


### 그래픽 최적화
그래픽팀과 논의한 그래픽 리소스 최적화 메뉴얼 내용 일부

Max Particles 수 최대한 적게
•	60 ~ 100 적정 수준 [최대 1000]   
 ![image](https://github.com/user-attachments/assets/4b041825-a77f-4865-ae73-072858f064d2)

Cast Shadows off 처리   
![image](https://github.com/user-attachments/assets/8e367ead-5a8f-4b0f-898e-b9b393543fe2)

![image](https://github.com/user-attachments/assets/d51be65c-1920-46fe-bc08-4e556cec1f50)

Sprite Packer를 통한 Texture 그룹별 압축   
![image](https://github.com/user-attachments/assets/690d931e-c971-4e85-946f-f212b3cb3862)

압축이 불가능한 Texture의 경우 별도로 ASTC 압축   
![image](https://github.com/user-attachments/assets/0f8e2f97-6e0e-4fcc-8407-7f3e324f7f76)
![image](https://github.com/user-attachments/assets/c4c346aa-a750-44ff-963c-c65a08f61f8f)


Memory Profiler와 
