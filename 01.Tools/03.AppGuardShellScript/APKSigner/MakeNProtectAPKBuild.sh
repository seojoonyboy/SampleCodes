#!/bin/sh

# zipalign, apksigner은 SDK_BUILD_TOOL 경로에 있음
# jarsigner은 OPEN_JDK_BIN 경로에 있음



# 경로가 다른 경우 수정해 주세요
ZIP_ALIGN_EXE="/c/Program Files/Unity/Hub/Editor/2021.2.16f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/30.0.2/zipalign"
APK_SIGNER_BAT="/c/Program Files/Unity/Hub/Editor/2021.2.16f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/30.0.2/apksigner.bat"
KEY_STORE_PATH="/d/snowballs.keystore"
KEY_ALIAS_NAME="sw.kangdaniel"



echo "ZIP_ALIGN_EXE path : ${ZIP_ALIGN_EXE}"
echo "APK_SIGNER_BAT path : ${APK_SIGNER_BAT}"



for apkFile in `find ./Input -name '*.apk'`
do
	# echo $apkFile
	TMP=`basename $apkFile`
	"$ZIP_ALIGN_EXE" -f -v -p 4 $apkFile ./Output/${TMP%%.apk}_aligned.apk
	"$APK_SIGNER_BAT" sign -v --out ./Output/${TMP%%.apk}_aligned_signer.apk --ks "$KEY_STORE_PATH" --ks-key-alias "$KEY_ALIAS_NAME" ./Output/${TMP%%.apk}_aligned.apk
done
