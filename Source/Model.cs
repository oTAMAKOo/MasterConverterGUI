
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
            public string directory;
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
                        directory = folderInfo.FullName,
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

        private MasterInfo[] GetMatchOfList()
        {
            if (string.IsNullOrEmpty(searchText)) { return MasterInfos; }

            var keywords = searchText.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < keywords.Length; ++i)
            {
                keywords[i] = keywords[i].ToLower();
            }
            
            return MasterInfos.Where(x => x.masterName.IsMatch(keywords)).ToArray();
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
                FileName = @"./MasterConverter.exe",

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

            arguments.AppendFormat("--input {0} ", masterInfo.directory);
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
