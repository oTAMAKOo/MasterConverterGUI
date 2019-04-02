
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;

namespace MasterConverterGUI
{
    public class Model
    {
        //----- params -----

        public class MasterInfo
        {
            public bool selection;
            public string masterName;
            public string directory;
        }

        //----- field -----
        
        private Subject<MasterInfo[]> onUpdateMasters = null;

        //----- property -----

        public Mode Mode { get; set; }
        public MasterInfo[] MasterInfos { get; private set; }
        public UserInfo UserInfo { get; private set; }

        //----- method -----   

        public Model()
        {
            Mode = Mode.Import;
            MasterInfos = new MasterInfo[0];
            UserInfo = new UserInfo();

            UserInfo.Load();
        }

        public void Register(string[] paths)
        {
            var directory = string.Empty;

            var unSelected = MasterInfos.Where(x => !x.selection)
                .Select(x => x.masterName)
                .ToArray();

            var list = new List<MasterInfo>();

            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];

                if (File.Exists(path))
                {
                    var info = Directory.GetParent(path);

                    directory = info.FullName;
                }
                else
                {
                    directory = path;
                }

                var filePaths = Directory.EnumerateFiles(directory, "ClassSchema.xlsx", SearchOption.AllDirectories);

                foreach (var filePath in filePaths)
                {
                    var folderInfo = Directory.GetParent(filePath);

                    var masterName = folderInfo.Name;

                    if (list.All(x => x.masterName != masterName))
                    {
                        var info = new MasterInfo()
                        {
                            selection = !unSelected.Contains(masterName),
                            masterName = masterName,
                            directory = folderInfo.FullName,
                        };

                        list.Add(info);
                    }
                }
            }

            MasterInfos = list.ToArray();

            if (onUpdateMasters != null)
            {
                onUpdateMasters.OnNext(MasterInfos);
            }
        }

        public IObservable<MasterInfo[]> OnUpdateMastersAsObservable()
        {
            return onUpdateMasters ?? (onUpdateMasters = new Subject<MasterInfo[]>());
        }
    }
}
