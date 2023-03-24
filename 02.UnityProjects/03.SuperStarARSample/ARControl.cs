using System.Collections.Generic;
using System.Linq;

using com.dalcomsoft.project.app.scene;
using com.dalcomsoft.project.client.model.account;
using com.dalcomsoft.project.client.model.asset;
using com.dalcomsoft.project.client.model.type;
using com.dalcomsoft.project.client.storage;

using Ext.Unity3D.Cdn;

namespace com.dalcomsoft.project.app.control.contents
{
    using ARDataStorage = Storage<short, ARData>;
    using AssetModel = ARData;

    using Control = ARControl;
    using LCT = LocaleControl;

    public class ARControl
    {
#if ENABLE_AR_CONTENT
        public static Dictionary<short, Data> Map { get; private set; }

        public static bool ARTrackedImageDownloaded
        {
            get
            {
                foreach (KeyValuePair<short, Data> items in Control.Map)
                {
                    Data data = items.Value;
                    if (!WWWFile.FileExists(data.ARImagePath.FilePath(), data.ARImagePath.Version()))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public static bool ARVideoFileDownloaded
        {
            get
            {
                foreach (KeyValuePair<short, Data> items in Control.Map)
                {
                    Data data = items.Value;
                    if (!WWWFile.FileExists(data.ARVideoPath.FilePath(), data.ARVideoPath.Version()))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public static void Open()
        {
            Control.Map = new Dictionary<short, Data>(ARDataStorage.Count);

            var node = ARDataStorage.First;
            while (node != null)
            {
                var pair = node.Value;
                node = node.Next;
                Data data = new Data(pair.Value);

                Control.Map.Add(pair.Key, data);
            }
        }

        public static List<ResponseData> GetAllImageData()
        {
            List<ResponseData> resData = new List<ResponseData>();
            foreach (KeyValuePair<short, Data> items in Control.Map)
            {
                ResponseData newData = new ResponseData();
                newData.code = items.Value.Code;
                newData.downloadPath = items.Value.ARImagePath;
                newData.type = Type.IMAGE;
                resData.Add(newData);
            }

            return resData;
        }

        public static void Reset() { }

        public static List<ResponseData> GetAllVideoData()
        {
            List<ResponseData> resData = new List<ResponseData>();
            foreach (KeyValuePair<short, Data> items in Control.Map)
            {
                ResponseData newData = new ResponseData();
                newData.code = items.Value.Code;
                newData.downloadPath = items.Value.ARVideoPath;
                newData.type = Type.IMAGE;
                resData.Add(newData);
            }

            return resData;
        }

        public static Data GetARData(short code)
        {
            var res = Control.Map.Values.ToList().Find(x => x.Code == code);
            return res;
        }

        private static Dictionary<int, short> trackedImageHashMap;
        public static void AddTrackedImage(int hash, short code)
        {
            if (trackedImageHashMap == null) trackedImageHashMap = new Dictionary<int, short>();

            if (!trackedImageHashMap.ContainsKey(hash)) trackedImageHashMap.Add(hash, code);
        }

        public static short GetTrackedImageCode(int hash)
        {
            if (trackedImageHashMap.ContainsKey(hash))
            {
                return trackedImageHashMap[hash];
            }
            else
            {
                return -1;
            }
        }

        public static List<WWWFile.DownloadPath> GetNeedDownPath()
        {
            List<WWWFile.DownloadPath> paths = new List<WWWFile.DownloadPath>();
            foreach (KeyValuePair<short, Data> items in Control.Map)
            {
                Data data = items.Value;
                if (!WWWFile.FileExists(data.ARImagePath.FilePath(), data.ARImagePath.Version()))
                {
                    paths.Add(data.ARImagePath);
                }

                if (!WWWFile.FileExists(data.ARVideoPath.FilePath(), data.ARVideoPath.Version()))
                {
                    paths.Add(data.ARVideoPath);
                }
            }
            return paths;
        }

        public delegate void CheckDownloadComplete(bool isOk);

        public static void CheckDownLoad(CheckDownloadComplete callback)
        {
            bool isAllDownloaded = Control.ARTrackedImageDownloaded && Control.ARVideoFileDownloaded;
            isAllDownloaded = false;
            if (!isAllDownloaded)
            {
                List<WWWFile.DownloadPath> videoDownloadPaths = (
                                             from videoData in GetAllVideoData()
                                             select (videoData.downloadPath)
                                         ).ToList();

                List<WWWFile.DownloadPath> imageDownloadPaths = (
                                             from imageData in GetAllImageData()
                                             select (imageData.downloadPath)
                                         ).ToList();

                Control.RemoveUnusedDownloadFiles(
                    AssetPathControl.PATH_FOLDER_AR_VIDEO.ToString(),
                    videoDownloadPaths,
                    "*.mp4"
                );

                Control.RemoveUnusedDownloadFiles(
                    AssetPathControl.PATH_FOLDER_AR_IMAGE.ToString(),
                    imageDownloadPaths,
                    "*.bytes"
                );

                List<WWWFile.DownloadPath> totalDownloadPaths = new List<WWWFile.DownloadPath>();
                totalDownloadPaths.AddRange(videoDownloadPaths.Where(x => !WWWFile.FileExists(x.FilePath(), x.Version())));
                totalDownloadPaths.AddRange(imageDownloadPaths.Where(x => !WWWFile.FileExists(x.FilePath(), x.Version())));
                InstallARContentAssist.Open(totalDownloadPaths, (isOk) =>
                {
                    callback(isOk);
                });
            }
            else
            {
                callback(true);
            }
        }

        /// <summary>
        /// 제거 제외 대상 외의 특정 확장자 파일들을 제거한다.
        /// </summary>
        /// <param name="downloadPaths">제거 제외 경로</param>
        /// <param name="extension">대상 확장자명</param>
        public static void RemoveUnusedDownloadFiles(string dirPath, List<WWWFile.DownloadPath> downloadPaths, string extension)
        {
            var fileNames = from downloadPath in downloadPaths
                            select (System.IO.Path.GetFileName(downloadPath.FilePath()));

            AssetControl.DeleteFileExclusive(dirPath, fileNames.ToList(), extension);
        }

        public static void OpenRewardPopup(List<RecentProvide> recentProvides, System.Action<bool, List<ProvideControl.Data>> callback)
        {
            ProvideControl.Clear();
            List<ProvideControl.Data> provideList = ProvideControl.CreateList(recentProvides);
            ProvideControl.AddRewards(provideList);

            ProvideControl.Param param = new ProvideControl.Param();
            param.type = ProvideControl.TYPE.All;
            param.icon = LCT.GetString(LocaleCodes.REWARD_POP_ICON);
            string titleText = LCT.GetString(LocaleCodes.REWARD_POP_TITLE);
            param.title = titleText;
            param.message = LCT.GetString(LocaleCodes.REWARD_OBTAINED_MESSAGE);
            param.btnOkText = LCT.GetString(LocaleCodes.REWARD_POP_BTN1);

            ProvideControl.Open(param, (use) =>
            {
                if (callback != null)
                    callback(use, provideList);
            });
        }

        public class Data
        {
            AssetModel model;
            public short Code { get { return this.model.code; } }

            public int? GroupCode
            {
                get
                {
                    return this.model.quizGroupCode;
                }
            }

            public int? TimeLimit
            {
                get
                {
                    return this.model.timeLimit;
                }
            }

            public int? AnswerScore
            {
                get
                {
                    return this.model.answerScore;
                }
            }

            public int? WronAnswerTime
            {
                get
                {
                    return this.model.wrongAnswerTime;
                }
            }

            public int? RewardScore
            {
                get
                {
                    return this.model.rewardScore;
                }
            }

            public int ContentType
            {
                get
                {
                    return this.model.contensType;
                }
            }

            public Data(AssetModel asset)
            {
                this.model = asset;

                this.model.ARTrackedImage = URLsControl.FindUrl(this.model.ARTrackedImage);
                this.model.ARVideo = URLsControl.FindUrl(this.model.ARVideo);
            }

            WWWFile.DownloadPath arImagePath = null;
            public WWWFile.DownloadPath ARImagePath
            {
                get
                {
                    if (this.arImagePath == null)
                        this.arImagePath = AssetPathControl.ToImagePath(this.model.ARTrackedImage, AssetPathControl.PATH_FOLDER_AR_IMAGE, this.Code.ToString());

                    return this.arImagePath;
                }
            }

            WWWFile.DownloadPath arVideoPath = null;
            public WWWFile.DownloadPath ARVideoPath
            {
                get
                {
                    if (this.arVideoPath == null)
                        this.arVideoPath = AssetPathControl.ToVideoPath(this.model.ARVideo, AssetPathControl.PATH_FOLDER_AR_VIDEO, this.Code.ToString());

                    return this.arVideoPath;
                }
            }
        }

        public class ResponseData
        {
            public short code;

            public Type type;
            public WWWFile.DownloadPath downloadPath;
        }

        public enum Type
        {
            IMAGE,
            VIDEO
        }
#endif  // ENABLE_AR_CONTENT
    }
}
