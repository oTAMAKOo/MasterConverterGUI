
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Modules.Threading;

namespace MasterConverterGUI
{
    public class Model
    {
        //----- params -----

        public class MasterInfo
        {
            public bool selection;
            public string masterName;
            public string localPath;
        }

        //----- field -----

        public string searchText = null;

        private Subject<MasterInfo[]> onUpdateMasters = null;

        //----- property -----

        public Mode Mode { get; set; }
        public MasterInfo[] MasterInfos { get; private set; }
        public MasterInfo[] CurrentMasterInfos { get; private set; }

        /// <summary> マスター検索ディレクトリ </summary>
        public string SearchDirectory { get; set; }
        /// <summary> タグ </summary>
        public string Tags { get; set; }
        /// <summary> MessagePackを出力するか </summary>
        public bool GenerateMessagePack { get; set; }
        /// <summary> MessagePack出力ディレクトリ </summary>
        public string MessagePackDirectory { get; set; }
        /// <summary> Yamlを出力するか </summary>
        public bool GenerateYaml { get; set; }
        /// <summary> Yaml出力ディレクトリ </summary>
        public string YamlDirectory { get; set; }

        //----- method -----   

        public void Initialize()
        {
            Mode = Mode.Import;
            MasterInfos = new MasterInfo[0];

            if (!string.IsNullOrEmpty(SearchDirectory))
            {
                Register(SearchDirectory);
            }
        }

        public void Register(string path)
        {
            var directory = string.Empty;

            var unSelected = MasterInfos.Where(x => !x.selection)
                .Select(x => x.masterName)
                .ToArray();

            var list = new List<MasterInfo>();

            if (File.Exists(path))
            {
                var info = Directory.GetParent(path);

                directory = info.FullName;
            }
            else
            {
                directory = path;
            }

            SearchDirectory = directory;

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
                        localPath = folderInfo.FullName.Substring(SearchDirectory.Length),
                    };

                    list.Add(info);
                }
            }

            MasterInfos = list.ToArray();

            CurrentMasterInfos = MasterInfos;

            if (onUpdateMasters != null)
            {
                onUpdateMasters.OnNext(MasterInfos);
            }
        }

        public void OpenMasterXlsx(MasterInfo info)
        {
            var fileName = info.masterName + Constants.MasterFileExtension;
            var path = SearchDirectory + info.localPath + Path.DirectorySeparatorChar + fileName;

            if (File.Exists(path))
            {
                Process.Start(path);
            }
        }

        public void OpenMasterDirectory(MasterInfo info)
        {
            var path = SearchDirectory + info.localPath + Path.DirectorySeparatorChar;

            if (Directory.Exists(path))
            {
                Process.Start(path);
            }
        }

        public void UpdateMasterInfo(MasterInfo masterInfo)
        {
            if (masterInfo == null) { return; }
            
            for (var i = 0; i < MasterInfos.Length; i++)
            {
                if (MasterInfos[i] != masterInfo) { continue; }

                MasterInfos[i] = masterInfo;
            }
        }

        private MasterInfo[] GetMatchOfList()
        {
            if (string.IsNullOrEmpty(searchText)) { return MasterInfos; }

            var keywords = searchText.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < keywords.Length; ++i)
            {
                keywords[i] = keywords[i].ToLower();
            }

            Func<MasterInfo, bool> filter = info =>
            {
                var result = false;

                result |= info.masterName.IsMatch(keywords);
                result |= info.localPath.IsMatch(keywords);

                return result;
            };
            
            return MasterInfos.Where(x => filter(x)).ToArray();
        }

        public void UpdateSearchText(string text)
        {
            if (searchText == text) { return; }

            searchText = text;

            CurrentMasterInfos = GetMatchOfList();

            if (onUpdateMasters != null)
            {
                onUpdateMasters.OnNext(CurrentMasterInfos);
            }
        }

        public async Task ConvertMaster(Action<bool, string> onExecFinish)
        {
            if (!File.Exists(Constants.MasterConverterPath))
            {
                var errorMessage = string.Format("{0} not found.", Constants.MasterConverterPath);

                onExecFinish(false, errorMessage);

                return;
            }

            var masterInfos = MasterInfos.Where(x => x.selection).ToArray();

            var taskQueue = new TaskQueue(5);

            for (var i = 0; i < masterInfos.Length; i++)
            {
                var masterInfo = masterInfos[i];

                taskQueue.Queue(async () => await ExecuteConvertMaster(masterInfo, onExecFinish));
            }

            await taskQueue.Process();
        }

        private Task ExecuteConvertMaster(MasterInfo masterInfo, Action<bool, string> onExecFinish)
        {
            var masterName = masterInfo.masterName;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            var tcs = new TaskCompletionSource<bool>();

            var process = new Process();

            var arguments = BuildConverterArgument(masterInfo);

            process.StartInfo = new ProcessStartInfo()
            {
                FileName = Constants.MasterConverterPath,

                UseShellExecute = false,
                CreateNoWindow = true,

                RedirectStandardOutput = true,
                RedirectStandardError = true,

                Arguments = arguments,
            };

            var log = new StringBuilder();

            DataReceivedEventHandler logReceive = (x, y) =>
            {
                if (y.Data != null) { log.AppendLine(y.Data); }
            };

            EventHandler eventHandler = (sender, args) =>
            {
                sw.Stop();

                if (process.ExitCode == 0)
                {
                    var time = sw.Elapsed.TotalMilliseconds.ToString("F2");

                    onExecFinish(true, string.Format("[Success] {0} ({1}ms)", masterName, time));
                }
                else
                {
                    var separator = "------------------------------------------------------";

                    var logBuilder = new StringBuilder();

                    logBuilder.AppendLine(separator);
                    logBuilder.AppendLine(string.Format("[Error] {0}", masterName));
                    logBuilder.AppendLine(separator);
                    logBuilder.AppendLine(log.ToString());
                    logBuilder.AppendLine(separator);

                    onExecFinish(false, logBuilder.ToString());
                }

                tcs.SetResult(true);
            };

            process.OutputDataReceived += logReceive;
            process.ErrorDataReceived += logReceive;

            process.Start();

            process.Exited += eventHandler;
            process.EnableRaisingEvents = true;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
    
            return tcs.Task;
        }

        private string BuildConverterArgument(MasterInfo masterInfo)
        {
            var arguments = new StringBuilder();

            var modeText = Constants.GetArgumentText(Mode);
            var tags = string.Empty;
            var export = string.Empty;

            switch (Mode)
            {
                case Mode.Build:
                    {
                        if (GenerateMessagePack && GenerateYaml)
                        {
                            export = "both";
                        }
                        else if (GenerateMessagePack)
                        {
                            export = "messagepack";
                        }
                        else if (GenerateYaml)
                        {
                            export = "yaml";
                        }

                        if (!string.IsNullOrEmpty(Tags))
                        {
                            tags = string.Join(",", Tags.Trim().Split(' ').Where(x => !string.IsNullOrEmpty(x)).ToArray());
                        }
                    }
                    break;
            }

            var path = SearchDirectory + masterInfo.localPath;

            arguments.AppendFormat("--input {0} ", path);
            arguments.AppendFormat("--mode {0} ", modeText);

            if (!string.IsNullOrEmpty(MessagePackDirectory))
            {
                arguments.AppendFormat("--messagepack {0} ", MessagePackDirectory);
            }

            if (!string.IsNullOrEmpty(YamlDirectory))
            {
                arguments.AppendFormat("--yaml {0} ", YamlDirectory);
            }

            if (!string.IsNullOrEmpty(tags))
            {
                arguments.AppendFormat("--tag {0} ", tags);
            }

            if (!string.IsNullOrEmpty(export))
            {
                arguments.AppendFormat("--export {0} ", export);
            }

            return arguments.ToString();
        }

        public IObservable<MasterInfo[]> OnUpdateMastersAsObservable()
        {
            return onUpdateMasters ?? (onUpdateMasters = new Subject<MasterInfo[]>());
        }
    }
}
