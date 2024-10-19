APK Nprotect 2차 작업에 사용되는 변수들은 아래와 같습니다.

경로가 다른 경우 MakeNProtectAPKBuild.sh 내용을 수정해서 사용해 주세요.

ZIP_ALIGN_EXE="/c/Program Files/Unity/Hub/Editor/2021.2.16f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/30.0.2/zipalign"
APK_SIGNER_BAT="/c/Program Files/Unity/Hub/Editor/2021.2.16f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/30.0.2/apksigner.bat"
KEY_STORE_PATH="/c/snowballs.keystore"
KEY_ALIAS_NAME="sw.kimhj"

============================================================================================


AAB Nprotect 2차 작업에 사용되는 변수들은 아래와 같습니다.


경로가 다른 경우 MakeNProtectAABBuild.sh 내용을 수정해서 사용해 주세요.

JAR_SIGNER_EXE="/c/Program Files/Unity/Hub/Editor/2021.2.16f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/jarsigner.exe"
KEY_STORE_PATH="/c/snowballs.keystore"
KEY_ALIAS_NAME="sw.kimhj"
KEY_STORE_PASSWORD="SBUnity3D!$"