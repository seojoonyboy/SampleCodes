using com.dalcomsoft.project.client.model.asset;
using com.dalcomsoft.project.client.storage;

using System.Collections.Generic;
using System.Linq;

namespace com.dalcomsoft.project.app.control.contents
{
    using ARQuizDataStorage = Storage<short, ARQuizData>;
    using AssetModel = ARQuizData;
    using Control = ARQuizControl;
    using LCT = LocaleControl;

    public class ARQuizControl
    {
#if ENABLE_AR_CONTENT
        public static Dictionary<short, Data> Map { get; private set; }

        public static void Open()
        {
            Control.Map = new Dictionary<short, Data>(ARQuizDataStorage.Count);

            var node = ARQuizDataStorage.First;
            while (node != null)
            {
                var pair = node.Value;
                node = node.Next;
                Data data = new Data(pair.Value);

                Control.Map.Add(pair.Key, data);
            }
        }

        public static List<Data> GetARQuizData(int groupCode)
        {
            var res = Control.Map.Values.ToList().FindAll(x => x.GroupCode == groupCode);
            return res;
        }

        public class Data
        {
            AssetModel model;

            public AssetModel AssetModel { get { return this.model; } }

            public short Code { get { return this.model.code; } }

            public int GroupCode { get { return this.model.group; } }

            public int Question { get { return this.model.question; } }

            public int Answer { get { return this.model.answer; } }

            public string Context { get { return LCT.GetString(this.Question); } }

            public Data(AssetModel asset)
            {
                this.model = asset;
            }
        }
#endif  // ENABLE_AR_CONTENT
    }
}
