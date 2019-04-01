using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MasterConverterGUI
{
    public partial class Form1 : Form
    {
        //----- params -----

        //----- field -----

        private Model model = null;

        //----- property -----

        //----- method -----        

        public Form1()
        {
            InitializeComponent();

            model = new Model();

            //------ イベント購読 ------

            // マスター一覧更新イベント.
            model.OnUpdateMastersAsObservable().Subscribe(x => RefreshListView(x));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //------ 初期化 ------

            InitializeListView();
            InitializeComboBox();
            InitializeOptions();

            progressBar1.Hide();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            model.UserInfo.Save();
        }

        //------ コンボボックスコントロール制御 ------

        private void InitializeComboBox()
        {
            comboBox1.Items.AddRange(Enum.GetNames(typeof(Mode)));

            comboBox1.SelectedIndex = (int)Mode.Import;
        }

        private void comboBox1_SelectionChangeCommitted(object sender, EventArgs e)
        {
            model.Mode = (Mode)comboBox1.SelectedIndex;

            textBox1.Text = string.Empty;

            if (model.Mode == Mode.Build)
            {
                var builder = new StringBuilder();

                var messagePackDirectory = model.UserInfo.Data.MessagePackDirectory;
                var text = string.IsNullOrEmpty(messagePackDirectory) ? "---" : messagePackDirectory;
                builder.AppendLine(string.Format("[Export] MessagePack : {0}", text));

                // Yamlはディレクトリ指定がない場合は出力されない.
                var yamlDirectory = model.UserInfo.Data.YamlDirectory;

                if (!string.IsNullOrEmpty(yamlDirectory))
                {
                    builder.AppendLine(string.Format("[Export] Yaml : {0}", yamlDirectory));
                }

                textBox1.Text += builder.ToString();
            }
        }

        //------ リストビューコントロール制御 ------

        private void InitializeListView()
        {
            listView1.FullRowSelect = true;
            listView1.GridLines = true;
            listView1.CheckBoxes = true;
            listView1.Sorting = SortOrder.Ascending;
            listView1.View = View.Details;

            listView1.ItemCheck += ListView1_ItemCheck1;

            // 列（コラム）ヘッダの作成.
            var masterNameColumn = new ColumnHeader() { Text = "Master", Width = 200 };
            var masterDirectoryColumn = new ColumnHeader() { Text = "Directory", Width = -2 };

            ColumnHeader[] colHeaderRegValue = { masterNameColumn, masterDirectoryColumn };

            listView1.Columns.AddRange(colHeaderRegValue);
        }
        
        private void RefreshListView(Model.MasterInfo[] masters)
        {
            listView1.Items.Clear();
            
            foreach (var master in masters)
            {
                var item = new ListViewItem();

                item.Text = master.masterName;
                item.SubItems.Add(master.directory);
                item.Checked = master.selection;

                listView1.Items.Add(item);
            }
        }

        private void ListView1_ItemCheck1(object sender, ItemCheckEventArgs e)
        {
            if (e.CurrentValue == CheckState.Unchecked)
            {
                model.MasterInfos[e.Index].selection = true;
            }
            else if (e.CurrentValue == CheckState.Checked)
            {
                model.MasterInfos[e.Index].selection = false;
            }
        }

        //------ ドラッグアンドドロップ制御 ------

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            model.Register(files);
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.All;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        //------ オプション制御 ------

        private void InitializeOptions()
        {
            textBox2.Text = model.UserInfo.Data.Tags;
        }

        // タグ設定テキストボックス.
        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            model.UserInfo.Data.Tags = textBox2.Text;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var selectionDirectory = model.UserInfo.Data.MessagePackDirectory;

            model.UserInfo.Data.MessagePackDirectory = OpenSaveFolderBrowserDialog(selectionDirectory);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var selectionDirectory = model.UserInfo.Data.YamlDirectory;

            model.UserInfo.Data.YamlDirectory = OpenSaveFolderBrowserDialog(selectionDirectory);
        }

        // 保存先フォルダ選択ダイアログ.
        private string OpenSaveFolderBrowserDialog(string selectionDirectory)
        {
            var path = string.Empty;

            var fbd = new FolderBrowserDialog();

            fbd.Description = "フォルダを指定してください。";

            if(string.IsNullOrEmpty(selectionDirectory) && Directory.Exists(selectionDirectory))
            {
                fbd.SelectedPath = selectionDirectory;
            }
            
            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                path = fbd.SelectedPath;
            }

            return path;
        }

        //------ マスターコンバーター実行制御 ------

        private void ExecButton_Click(object sender, EventArgs e)
        {
            var logBuilder = new StringBuilder();

            DataReceivedEventHandler logReceive = (x, y) =>
            {
                if (y.Data != null) { logBuilder.AppendLine(y.Data); }
            };

            var masterInfos = model.MasterInfos.Where(x => x.selection).ToArray();

            progressBar1.Minimum = 0;
            progressBar1.Maximum = masterInfos.Length;
            progressBar1.Value = 0;

            textBox1.Text = string.Empty;

            var success = 0;

            for (var i = 0; i < masterInfos.Length; i++)
            {
                var masterInfo = masterInfos[i];

                progressBar1.Value = i;

                logBuilder.Clear();

                textBox1.Text += string.Format("{0}\n", masterInfo.masterName);

                var hasError = false;

                using (var process = new Process())
                {
                    var arguments = new StringBuilder();

                    arguments.AppendFormat("-input {0} ", masterInfo.directory);
                    arguments.AppendFormat("-mode {0} ", Constants.GetArgumentText(model.Mode));
                    arguments.AppendFormat("-messagepack {0} ", model.UserInfo.Data.MessagePackDirectory);
                    arguments.AppendFormat("-yaml {0} ", model.UserInfo.Data.YamlDirectory);

                    var tags = model.UserInfo.Data.Tags.Trim().Split(' ').Where(x => !string.IsNullOrEmpty(x)).ToArray();

                    if (tags.Any())
                    {
                        arguments.AppendFormat("-tag {0} ", string.Join(",", tags));
                    }

                    process.StartInfo = new ProcessStartInfo()
                    {
                        FileName = @"./MasterConverter.exe",

                        UseShellExecute = false,
                        CreateNoWindow = true,

                        RedirectStandardOutput = true,
                        RedirectStandardError = true,

                        Arguments = arguments.ToString(),
                    };

                    process.OutputDataReceived += logReceive;
                    process.ErrorDataReceived += logReceive;

                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();

                    process.CancelOutputRead();
                    process.CancelErrorRead();

                    if (process.ExitCode == 0)
                    {
                        success++;
                    }
                    else
                    {
                        hasError = true;
                    }
                }

                if (hasError)
                {
                    var separator = "------------------------------------------------------";

                    logBuilder.Insert(0, separator);
                    logBuilder.AppendLine(separator);
                }

                textBox1.Text += logBuilder.ToString();
            }

            progressBar1.Value = 0;

            if (success != masterInfos.Length)
            {
                var caption = "Convert Error";
                var message = string.Format("Convert Failed {0} masters.", masterInfos.Length - success);

                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
