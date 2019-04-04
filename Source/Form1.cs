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

            UpdateExecButton();
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
            var userData = model.UserInfo.Data;

            model.Mode = (Mode)comboBox1.SelectedIndex;

            richTextBox1.Text = string.Empty;

            var builder = new StringBuilder();

            switch (model.Mode)
            {
                case Mode.Build:
                    {
                        var directory = string.Empty;

                        // MessagePack.
                        var messagePackDirectory = userData.MessagePackDirectory;
                        directory = string.IsNullOrEmpty(messagePackDirectory) ? "---" : messagePackDirectory;
                        builder.AppendLine(string.Format("[Export] MessagePack : {0}", directory));

                        // Yaml.
                        var yamlDirectory = userData.YamlDirectory;
                        directory = string.IsNullOrEmpty(yamlDirectory) ? "---" : yamlDirectory;
                        builder.AppendLine(string.Format("[Export] Yaml : {0}", directory));
                    }
                    break;
            }

            richTextBox1.Text += builder.ToString();

            UpdateExecButton();
        }

        //------ ログテキストビューコントロール制御 ------

        delegate void TextBoxDeledate(string text);

        private void SetLogText(string text)
        {
            if (richTextBox1.InvokeRequired)
            {
                // 呼び出しスレッドとスレッドIDが異なる場合
                TextBoxDeledate temp_del = new TextBoxDeledate(SetLogText);

                Invoke(temp_del, new object[] { text });
            }
            else
            {
                // 呼び出しスレッドとスレッドIDが一致している場合、直接フォームコントロールに設定.
                richTextBox1.Text = text;
            }
        }

        //------ プログレスバーコントロール制御 ------

        delegate void ProgressbarDeledate(int value);

        private void SetProgress(int value)
        {
            if (richTextBox1.InvokeRequired)
            {
                // 呼び出しスレッドとスレッドIDが異なる場合
                ProgressbarDeledate temp_del = new ProgressbarDeledate(SetProgress);

                Invoke(temp_del, new object[] { value });
            }
            else
            {
                // 呼び出しスレッドとスレッドIDが一致している場合、直接フォームコントロールに設定.
                progressBar1.Value = value;
            }
        }

        //------ リストビューコントロール制御 ------

        private void InitializeListView()
        {
            listView1.FullRowSelect = true;
            listView1.GridLines = true;
            listView1.CheckBoxes = true;
            listView1.OwnerDraw = true;
            listView1.Sorting = SortOrder.None;
            listView1.View = View.Details;
            listView1.HeaderStyle = ColumnHeaderStyle.None;

            listView1.ItemCheck += ListView1_ItemCheck1;

            // 列（コラム）ヘッダの作成.            
            var masterNameColumn = new ColumnHeader() { Text = "Master", Width = 200 };
            var masterDirectoryColumn = new ColumnHeader() { Text = "Directory", Width = -2 };

            ColumnHeader[] colHeaderRegValue = { masterNameColumn, masterDirectoryColumn };

            listView1.Columns.AddRange(colHeaderRegValue);

            SizeLastColumn(listView1);
        }

        private void listView1_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.Graphics.FillRectangle(SystemBrushes.Menu, e.Bounds);
            e.Graphics.DrawRectangle(SystemPens.GradientInactiveCaption,
                                     new Rectangle(e.Bounds.X, 0, e.Bounds.Width, e.Bounds.Height));

            string text = listView1.Columns[e.ColumnIndex].Text;
            TextFormatFlags cFlag = TextFormatFlags.HorizontalCenter
                                    | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(e.Graphics, text, listView1.Font, e.Bounds, Color.Black, cFlag);
        }

        private void listView1_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void listView1_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void listView1_Resize(object sender, System.EventArgs e)
        {
            SizeLastColumn((ListView)sender);
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

        private void SizeLastColumn(ListView listView)
        {
            listView.Columns[listView.Columns.Count - 1].Width = -2;
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
            checkBox1.Checked = model.UserInfo.Data.GenerateMessagePack;
            checkBox2.Checked = model.UserInfo.Data.GenerateYaml;
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

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            model.UserInfo.Data.GenerateMessagePack = checkBox1.Checked;
            
            UpdateExecButton();
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            model.UserInfo.Data.GenerateYaml = checkBox2.Checked;

            UpdateExecButton();
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

        //------ 実行ボタン制御 ------

        private async void ExecButton_Click(object sender, EventArgs e)
        {
            await ExecMasterConverter();
        }

        private void UpdateExecButton()
        {
            switch (model.Mode)
            {
                case Mode.Build:
                {
                    ExecButton.Enabled = model.UserInfo.Data.GenerateMessagePack || model.UserInfo.Data.GenerateYaml;
                }
                    break;

                default:
                    ExecButton.Enabled = true;
                    break;
            }
        }

        //------ マスターコンバーター実行制御 ------

        private async Task ExecMasterConverter()
        {
            var success = 0;
            var progress = 0;

            var logBuilder = new StringBuilder();

            var masterInfos = model.MasterInfos.Where(x => x.selection).ToArray();

            richTextBox1.Text = string.Empty;

            progressBar1.Minimum = 0;
            progressBar1.Maximum = masterInfos.Length;
            progressBar1.Value = 0;

            Action<bool, string> onExecFinish = (result, log) =>
            {
                if(result)
                {
                    success++;
                }

                lock (logBuilder)
                {
                    logBuilder.AppendLine(log);                    
                }

                progress++;

                SetLogText(logBuilder.ToString());
                SetProgress(progress);
            };

            await model.ConvertMaster(onExecFinish);

            progressBar1.Value = 0;

            if (success == masterInfos.Length)
            {
                var caption = "Convert Complete";
                var message = string.Format("Convert {0} masters finish.", success);

                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                var caption = "Convert Error";
                var message = string.Format("Convert Failed {0} masters.", masterInfos.Length - success);

                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }       
    }
}
