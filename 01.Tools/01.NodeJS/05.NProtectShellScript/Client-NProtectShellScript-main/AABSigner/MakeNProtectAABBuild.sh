#!/bin/sh

# zipalign, apksigner은 SDK_BUILD_TOOL 경로에 있음
# jarsigner은 OPEN_JDK_BIN 경로에 있음

# 경로가 다른 경우 수정해 주세요
JAR_SIGNER_EXE="/c/Program Files/Unity/Hub/Editor/2021.2.16f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/jarsigner.exe"
KEY_STORE_PATH="/d/snowballs.keystore"
KEY_ALIAS_NAME="sw.kimhj"
KEY_STORE_PASSWORD="SBUnity3D!$"



echo "JAR_SIGNER_EXE path : ${JAR_SIGNER_EXE}"



for aabFile in `find . -name '*.aab'`
do
	TMP=`basename $aabFile`
	echo $TMP
	"$JAR_SIGNER_EXE" -verbose -sigalg SHA256withRSA -digestalg SHA-256 -tsa http://timestamp.digicert.com -keystore $KEY_STORE_PATH ${TMP} $KEY_ALIAS_NAME -storepass $KEY_STORE_PASSWORD
done
